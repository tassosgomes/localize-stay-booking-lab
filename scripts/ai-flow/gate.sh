#!/usr/bin/env bash
#
# Gate deterministico do TSG Flow — localize-stay-booking-lab (monorepo poliglota).
#
# Stacks: .NET (services/*, multi-sln) + Node/Vite+TS (frontend/*).
# Sem CI no repo: comandos seguem o padrao das skills de stack do projeto
# (dotnet-architecture/dependency-config/testing, react-architecture/testing)
# e os gate_command declarados em tasks/prd-fundacao-fase0/*_task.md.
# Cada stack vira bloco condicional disparado por arquivos daquela extensao em CHANGED.
#
# Uso:
#   scripts/ai-flow/gate.sh [--filter=<expr>]... [--base=<ref>] [--all-tests | --static | --skip-tests] [--sln=<path>]
#
# Saida: bloco compacto (<= ~60 linhas). Exit 0 = APROVADO, 1 = REPROVADO, 2 = erro de uso/ambiente.

set -uo pipefail

MAX_OUTPUT_LINES=40
SLN_OVERRIDE=""
SKIP_TESTS=0
STATIC=0
ALL_TESTS=0
BASE_REF="HEAD"
FILTERS=()

for arg in "$@"; do
  case "$arg" in
    --filter=*)    FILTERS+=("${arg#*=}") ;;
    --base=*)      BASE_REF="${arg#*=}" ;;
    --all-tests)   ALL_TESTS=1 ;;
    --skip-tests)  SKIP_TESTS=1 ;;
    --static)      STATIC=1 ;;
    --sln=*)       SLN_OVERRIDE="${arg#*=}" ;;
    -h|--help)     sed -n '2,14p' "$0"; exit 0 ;;
    *) echo "GATE: ERRO"; echo "argumento desconhecido: $arg"; exit 2 ;;
  esac
done

# Selecao explicita: ausencia de filtro nunca significa aprovacao comportamental.
MODES=$((ALL_TESTS + STATIC + SKIP_TESTS))
if ((MODES > 1)) || ((MODES > 0 && ${#FILTERS[@]} > 0)) ||
   ((MODES == 0 && ${#FILTERS[@]} == 0)); then
  echo "GATE: ERRO"; echo "use filtros OU --all-tests OU --static OU --skip-tests"; exit 2
fi
for f in "${FILTERS[@]}"; do
  if [[ -z "${f//[[:space:]]/}" ]]; then
    echo "GATE: ERRO"; echo "filtro vazio"; exit 2
  fi
done
if [[ ! "${GATE_TIMEOUT_SECONDS:-600}" =~ ^[1-9][0-9]*$ ]]; then
  echo "GATE: ERRO"; echo "GATE_TIMEOUT_SECONDS deve ser inteiro positivo"; exit 2
fi
command -v timeout >/dev/null 2>&1 || {
  echo "GATE: ERRO"; echo "timeout indisponivel; configure equivalente no gate"; exit 2
}
export CI=true TERM=dumb NO_COLOR=1 LC_ALL=C
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export DOTNET_CLI_UI_LANGUAGE=en-US VSLANG=1033
REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || {
  echo "GATE: ERRO"; echo "nao esta em um repositorio git"; exit 2
}
cd "$REPO_ROOT" || exit 2
BASE_REF="$(git rev-parse --verify "$BASE_REF^{commit}" 2>/dev/null)" || {
  echo "GATE: ERRO"; echo "base Git invalida"; exit 2
}

# ---------------------------------------------------------------------------
# Escopo: arquivos alterados desde BASE_REF (= checkpoint focused ou base do PRD)
# INVARIANTE 1 — nunca rode format/lint sobre o projeto inteiro.
# ---------------------------------------------------------------------------
DOTNET_RE='\.cs$|\.csproj$|\.sln$|^Directory\.Build\.props$|^Directory\.Packages\.props$|^\.config/dotnet-tools\.json$|^\.editorconfig$|^global\.json$'
NODE_RE='\.(ts|tsx|js|jsx|mjs|cjs)$|package\.json$|package-lock\.json$|pnpm-lock\.yaml|yarn\.lock$|^frontend/|vitest\.config\.|vite\.config\.|tsconfig.*\.json$'

mapfile -d '' -t CHANGED < <(
  { git diff --name-only -z "$BASE_REF" --
    git ls-files --others --exclude-standard -z
  } | sort -zu
)
CHANGED_CS=(); CHANGED_TS=()
N_DOTNET=0; N_NODE=0
# Artefatos derivados nunca entram no escopo de format/lint (ex.: node_modules
# sem regra de .gitignore ainda, pastas bin/obj): sao gerados, nao entrega.
DERIVED_RE='(^|/)(node_modules|bin|obj|dist|\.venv|__pycache__|\.next|coverage)/'
for file in "${CHANGED[@]}"; do
  # Deletados contam no diff, mas nao sao passados a formatadores.
  if [[ -f "$file" ]]; then
    # shellcheck disable=SC2076
    if [[ ! "$file" =~ $DERIVED_RE ]]; then
      [[ "$file" =~ \.cs$ ]] && CHANGED_CS+=("$file")
      # shellcheck disable=SC2076
      [[ "$file" =~ \.(ts|tsx|js|jsx|mjs|cjs)$ ]] && CHANGED_TS+=("$file")
    fi
  fi
  # shellcheck disable=SC2076
  [[ "$file" =~ $DOTNET_RE ]] && N_DOTNET=$((N_DOTNET + 1))
  # shellcheck disable=SC2076
  [[ "$file" =~ $NODE_RE ]] && N_NODE=$((N_NODE + 1))
done

# Descoberta de projetos (multi-sln .NET + 1..n frontends sob frontend/).
SOLUTIONS=()
if [[ -n "$SLN_OVERRIDE" ]]; then
  [[ -f "$SLN_OVERRIDE" ]] || { echo "GATE: ERRO"; echo "solution inexistente: $SLN_OVERRIDE"; exit 2; }
  SOLUTIONS=("$SLN_OVERRIDE")
else
  while IFS= read -r s; do SOLUTIONS+=("$s"); done < <(
    find . -maxdepth 4 -name '*.sln' -not -path '*/bin/*' -not -path '*/obj/*' -not -path '*/node_modules/*' 2>/dev/null | sed 's|^\./||' | sort
  )
fi
FRONTEND_DIRS=()
while IFS= read -r p; do FRONTEND_DIRS+=("$(dirname "$p")"); done < <(
  find . -maxdepth 3 -name 'package.json' -not -path '*/node_modules/*' 2>/dev/null | sed 's|^\./||' | sort
)

fail() { # fail <etapa> <comando> <output>
  echo "GATE: REPROVADO"
  echo "etapa: $1"
  echo "comando: $2"
  echo "--- output (ultimas ${MAX_OUTPUT_LINES} linhas) ---"
  printf '%s\n' "$3" | tail -n "$MAX_OUTPUT_LINES"   # INVARIANTE 3
  exit 1
}

run() {
  local rc=0
  OUT="$(timeout "${GATE_TIMEOUT_SECONDS:-600}" "$@" 2>&1)" || rc=$?
  if ((rc == 124 || rc == 137 || rc == 126 || rc == 127)); then
    echo "GATE: ERRO"
    echo "ambiente/timeout ao executar: $1"
    printf '%s\n' "$OUT" | tail -n "$MAX_OUTPUT_LINES"
    exit 2
  fi
  return "$rc"
}

docker_hint() { # acrescenta dica quando Testcontainers/Docker falha sem daemon
  if printf '%s' "$1" | grep -qiE 'docker|testcontainers|ryuk|cannot connect|permission denied.*docker'; then
    if ! docker info >/dev/null 2>&1; then
      printf '%s\n[gate] daemon Docker indisponivel: testes Testcontainers nao podem rodar aqui.' "$1"
      return 0
    fi
  fi
  printf '%s' "$1"
}

is_frontend_only_filter() { # filtros do frontend (task 7.0: ServiceStatus.test)
  local f="$1"
  [[ "$f" == *".test"* || "$f" == *"ServiceStatus"* || "$f" == *"vitest"* ]]
}

frontend_unit_files_for() { # $1 filter, $2 frontend dir
  local filter="$1" fdir="$2"
  find "$fdir" \( -path '*/node_modules/*' -o -path '*/dist/*' -o -path '*/e2e/*' \) -prune -o \
    -type f \( -name "*${filter}*.test.ts" -o -name "*${filter}*.test.tsx" \) -print 2>/dev/null
}

frontend_e2e_files_for() { # $1 filter, $2 frontend dir
  local filter="$1" fdir="$2"
  [[ -d "$fdir/e2e" ]] || return 0
  find "$fdir/e2e" -type f \( -name "*${filter}*.spec.ts" -o -name "*${filter}*.spec.tsx" \) -print 2>/dev/null
}

filter_has_frontend_suite() {
  local f="$1" fdir files
  is_frontend_only_filter "$f" && return 0
  for fdir in "${FRONTEND_DIRS[@]:-}"; do
    files="$(frontend_unit_files_for "$f" "$fdir")"
    [[ -n "${files//[[:space:]]/}" ]] && return 0
    files="$(frontend_e2e_files_for "$f" "$fdir")"
    [[ -n "${files//[[:space:]]/}" ]] && return 0
  done
  return 1
}

playwright_passed_count() {
  printf '%s\n' "$1" | grep -oE '[0-9]+ passed' | tail -n 1 | grep -oE '^[0-9]+' | awk '{print $1+0}'
}

# Necessidade por stack: arquivos da stack no diff, suite completa, static/skip
# com projetos presentes, ou filtro que a stack pode atender.
ALL_FRONTEND_ONLY=1
for f in "${FILTERS[@]:-}"; do
  is_frontend_only_filter "$f" || filter_has_frontend_suite "$f" || ALL_FRONTEND_ONLY=0
done
NEED_DOTNET=0; NEED_NODE=0
if ((N_DOTNET > 0 || ALL_TESTS == 1 || STATIC == 1 || SKIP_TESTS == 1)); then NEED_DOTNET=1; fi
if ((N_NODE > 0 || ALL_TESTS == 1 || STATIC == 1 || SKIP_TESTS == 1)); then NEED_NODE=1; fi
if ((${#FILTERS[@]} > 0)); then
  ((ALL_FRONTEND_ONLY == 0)) && NEED_DOTNET=1
  if ((ALL_FRONTEND_ONLY == 1)) || ((${#SOLUTIONS[@]} == 0)); then NEED_NODE=1; fi
fi
(( ${#SOLUTIONS[@]} == 0 )) && NEED_DOTNET=0
(( ${#FRONTEND_DIRS[@]} == 0 )) && NEED_NODE=0
# Sem nenhum projeto na stack, o build daquela stack e pulado (repo verde / pre-scaffold).

# ---------------------------------------------------------------------------
# 1. Formatacao / lint — ESCOPADO nos arquivos alterados (INVARIANTE 1)
# ---------------------------------------------------------------------------
FORMAT_STATUS="pulado (nenhum fonte alterado)"
FORMAT_NOTES=()
if ((${#CHANGED_CS[@]} > 0)); then
  if ((${#SOLUTIONS[@]} == 0)); then
    FORMAT_NOTES+=("format .NET pulado (nenhuma .sln no repo ainda)")
  else
    command -v dotnet >/dev/null 2>&1 || { echo "GATE: ERRO"; echo "dotnet indisponivel"; exit 2; }
    for sln in "${SOLUTIONS[@]}"; do
      sdir="$(dirname "$sln")"; subset=()
      for f in "${CHANGED_CS[@]}"; do [[ "$f" == "$sdir"/* ]] && subset+=("$f"); done
      # Solution na raiz cobre arquivos fora de subdiretorio proprio.
      [[ "$sdir" == "." ]] && subset=("${CHANGED_CS[@]}")
      if ((${#subset[@]} > 0)); then
        CMD=(dotnet format "$sln" --verify-no-changes --no-restore --include "${subset[@]}")
        if ! run "${CMD[@]}"; then
          fail "format" "dotnet format $sln --verify-no-changes --no-restore --include <${#subset[@]} arquivos da task>" "$OUT"
        fi
        FORMAT_NOTES+=("dotnet format ok ($sln: ${#subset[@]} arquivos)")
      fi
    done
    [[ ${#FORMAT_NOTES[@]} -eq 0 ]] && FORMAT_NOTES+=("nenhum .cs alterado pertence a uma solution (ignorado)")
  fi
fi
if ((${#CHANGED_TS[@]} > 0)); then
  if ((${#FRONTEND_DIRS[@]} == 0)); then
    FORMAT_NOTES+=("lint frontend pulado (nenhum package.json ainda)")
  else
    for fdir in "${FRONTEND_DIRS[@]}"; do
      subset=()
      for f in "${CHANGED_TS[@]}"; do [[ "$f" == "$fdir"/* ]] && subset+=("$f"); done
      ((${#subset[@]} == 0)) && continue
      if [[ -f "$fdir/eslint.config.js" || -f "$fdir/eslint.config.mjs" || -f "$fdir/.eslintrc.json" || -f "$fdir/.eslintrc.js" || -f "$REPO_ROOT/eslint.config.js" ]] \
         && [[ -x "$fdir/node_modules/.bin/eslint" ]]; then
        if ! run "$fdir/node_modules/.bin/eslint" --no-color "${subset[@]}"; then
          fail "format" "eslint <${#subset[@]} arquivos da task em $fdir>" "$OUT"
        fi
        FORMAT_NOTES+=("eslint ok ($fdir: ${#subset[@]} arquivos)")
      elif [[ -x "$fdir/node_modules/.bin/prettier" ]]; then
        if ! run "$fdir/node_modules/.bin/prettier" --check "${subset[@]}"; then
          fail "format" "prettier --check <${#subset[@]} arquivos da task em $fdir>" "$OUT"
        fi
        FORMAT_NOTES+=("prettier ok ($fdir: ${#subset[@]} arquivos)")
      else
        FORMAT_NOTES+=("lint $fdir pulado (sem eslint/prettier local)")
      fi
    done
  fi
fi
if ((${#FORMAT_NOTES[@]} > 0)); then FORMAT_STATUS="$(IFS='; '; echo "${FORMAT_NOTES[*]}")"; fi

# ---------------------------------------------------------------------------
# 2. Build / typecheck (por stack tocada)
# ---------------------------------------------------------------------------
BUILD_NOTES=()
if ((NEED_DOTNET == 1)); then
  command -v dotnet >/dev/null 2>&1 || { echo "GATE: ERRO"; echo "dotnet indisponivel"; exit 2; }
  for sln in "${SOLUTIONS[@]}"; do
    CMD=(dotnet build "$sln" --nologo -v minimal)
    if ! run "${CMD[@]}"; then
      fail "build" "dotnet build $sln --nologo" "$OUT"
    fi
    SUMMARY="$(printf '%s\n' "$OUT" | grep -oE '[0-9]+ (Error|Warning)\(s\)' | tr '\n' ' ')"
    BUILD_NOTES+=("dotnet build ok ($sln ${SUMMARY:-})")
  done
fi
if ((NEED_NODE == 1)); then
  for fdir in "${FRONTEND_DIRS[@]}"; do
    if [[ -f "$fdir/tsconfig.json" && -x "$fdir/node_modules/.bin/tsc" ]]; then
      if ! run "$fdir/node_modules/.bin/tsc" --noEmit -p "$fdir/tsconfig.json"; then
        fail "build" "tsc --noEmit -p $fdir/tsconfig.json" "$OUT"
      fi
      BUILD_NOTES+=("tsc ok ($fdir)")
    else
      BUILD_NOTES+=("typecheck $fdir pulado (sem tsc local)")
    fi
  done
fi
BUILD_STATUS="ok (nada a compilar)"
(( ${#BUILD_NOTES[@]} > 0 )) && BUILD_STATUS="$(IFS='; '; echo "${BUILD_NOTES[*]}")"

# ---------------------------------------------------------------------------
# 3. Testes — com DETECCAO DE FILTRO VAZIO (INVARIANTE 2)
# ---------------------------------------------------------------------------
TEST_STATUS="nao aplicavel (static)"
((SKIP_TESTS == 1)) && TEST_STATUS="pulado (diagnostico; nao aprova task)"
OUT=""
if ((SKIP_TESTS == 0 && STATIC == 0)) && ((ALL_TESTS == 1)); then
  if ((NEED_DOTNET == 0 && NEED_NODE == 0)); then
    TEST_STATUS="ok (nenhuma suite no repo ainda)"
  else
    if ((NEED_DOTNET == 1)); then
      command -v dotnet >/dev/null 2>&1 || { echo "GATE: ERRO"; echo "dotnet indisponivel"; exit 2; }
      for sln in "${SOLUTIONS[@]}"; do
        CMD=(dotnet test "$sln" --nologo)
        RC=0; run "${CMD[@]}" || RC=$?
        ((RC != 0)) && fail "testes" "dotnet test $sln --nologo" "$(docker_hint "$OUT")"
      done
      TEST_STATUS="ok (suite .NET completa)"
    fi
    if ((NEED_NODE == 1)); then
      for fdir in "${FRONTEND_DIRS[@]}"; do
        if [[ -x "$fdir/node_modules/.bin/vitest" ]]; then
          CMD=(npm --prefix "$fdir" exec --no -- vitest run --reporter=basic --no-color)
          RC=0; run "${CMD[@]}" || RC=$?
          ((RC != 0)) && fail "testes" "vitest run ($fdir)" "$OUT"
          TEST_STATUS="ok (suite frontend completa: $fdir)"
        fi
      done
    fi
  fi
elif ((SKIP_TESTS == 0 && STATIC == 0)) && ((${#FILTERS[@]} > 0)); then
  RESULTS=()
  for f in "${FILTERS[@]}"; do
    SUM=0
    UNIT_SUM=0
    E2E_SUM=0
    HAS_UNIT=0
    HAS_E2E=0
    # Nunca passe flags do tipo --passWithNoTests: suite ausente deve reprovar.
    if ((${#FRONTEND_DIRS[@]} > 0)); then
      for fdir in "${FRONTEND_DIRS[@]}"; do
        mapfile -t UNIT_FILES < <(frontend_unit_files_for "$f" "$fdir")
        mapfile -t E2E_FILES < <(frontend_e2e_files_for "$f" "$fdir")
        ((${#UNIT_FILES[@]} > 0)) && HAS_UNIT=1
        ((${#E2E_FILES[@]} > 0)) && HAS_E2E=1
        if ((${#UNIT_FILES[@]} > 0)) && [[ -x "$fdir/node_modules/.bin/vitest" ]]; then
          CMD=(env --chdir="$fdir" ./node_modules/.bin/vitest run "$f" --reporter=basic --no-color --passWithNoTests=false)
          RC=0; run "${CMD[@]}" || RC=$?
          if ((RC != 0)); then
            printf '%s\n' "$OUT" | grep -qiE 'No test files found|No test matches|No tests found' || \
              fail "testes" "vitest run \"$f\" ($fdir)" "$OUT"
          else
            PART="$(printf '%s\n' "$OUT" | grep -oE 'Tests[[:space:]]+[0-9]+ passed' | grep -oE '[0-9]+' | awk '{s+=$1} END {print s+0}')"
            UNIT_SUM=$((UNIT_SUM + PART))
          fi
        fi
        if ((${#E2E_FILES[@]} > 0)); then
          if [[ ! -x "$fdir/node_modules/.bin/playwright" ]]; then
            echo "GATE: ERRO"
            echo "Playwright indisponivel em $fdir para o filtro \"$f\""
            exit 2
          fi
          if ! "$fdir/node_modules/.bin/playwright" install chromium >/dev/null 2>&1; then
            echo "GATE: ERRO"
            echo "falha ao instalar o browser Chromium do Playwright"
            exit 2
          fi
          E2E_REL=()
          for e2e_file in "${E2E_FILES[@]}"; do
            E2E_REL+=("${e2e_file#"$fdir"/}")
          done
          CMD=(env --chdir="$fdir" ./node_modules/.bin/playwright test --reporter=line "${E2E_REL[@]}")
          RC=0; run "${CMD[@]}" || RC=$?
          if printf '%s\n' "$OUT" | grep -q 'E2E_INFRA_UNAVAILABLE'; then
            echo "GATE: ERRO"
            echo "infra E2E indisponivel (Catalog/Postgres) para o filtro \"$f\""
            printf '%s\n' "$OUT" | tail -n "$MAX_OUTPUT_LINES"
            exit 2
          fi
          if ((RC != 0)); then
            fail "testes" "playwright test ${E2E_FILES[*]} ($fdir)" "$OUT"
          fi
          PART="$(playwright_passed_count "$OUT")"
          E2E_SUM=$((E2E_SUM + PART))
        fi
      done
    fi
    if ((HAS_UNIT == 1 && UNIT_SUM == 0)); then
      fail "testes" "filtro \"$f\" (RTL/MSW)" \
        "Filtro nao selecionou PropertyUpdate.test.tsx / testes RTL exigidos.
$OUT"
    fi
    if ((HAS_E2E == 1 && E2E_SUM == 0)); then
      fail "testes" "filtro \"$f\" (Playwright)" \
        "Filtro nao selecionou e2e/PropertyUpdate.spec.ts / testes Playwright exigidos.
$OUT"
    fi
    SUM=$((UNIT_SUM + E2E_SUM))
    if [[ "$SUM" == "0" ]] && ((${#SOLUTIONS[@]} > 0)) && ! filter_has_frontend_suite "$f"; then
      command -v dotnet >/dev/null 2>&1 || { echo "GATE: ERRO"; echo "dotnet indisponivel"; exit 2; }
      for sln in "${SOLUTIONS[@]}"; do
        CMD=(dotnet test "$sln" --nologo --filter "$f")
        RC=0; run "${CMD[@]}" || RC=$?
        ((RC != 0)) && fail "testes" "dotnet test $sln --nologo --filter \"$f\"" "$(docker_hint "$OUT")"
        PART="$(printf '%s\n' "$OUT" | grep -oE 'Passed:[[:space:]]+[0-9]+' | grep -oE '[0-9]+' | awk '{s+=$1} END {print s+0}')"
        SUM=$((SUM + PART))
      done
    fi
    if [[ "$SUM" == "0" ]]; then
      fail "testes" "filtro \"$f\" (dotnet/vitest/playwright conforme stacks presentes)" \
        "Filtro nao selecionou nenhum teste. A suite exigida pela task provavelmente nao existe.
$OUT"
    fi
    RESULTS+=("$f=${SUM} rtl=${UNIT_SUM} e2e=${E2E_SUM}")
  done
  TEST_STATUS="ok (${RESULTS[*]})"
fi

# ---------------------------------------------------------------------------
# 4. Higiene do diff (agnostico de stack)
# ---------------------------------------------------------------------------
if ! run git diff --check "$BASE_REF" --; then
  fail "diff-check" "git diff --check $BASE_REF" "$OUT"
fi

echo "GATE: APROVADO"
echo "arquivos alterados: ${#CHANGED[@]} (.NET: $N_DOTNET, node: $N_NODE)"
echo "format: $FORMAT_STATUS"
echo "build: $BUILD_STATUS"
echo "testes: $TEST_STATUS"
exit 0
