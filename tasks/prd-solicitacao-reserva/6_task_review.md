# Revisão da Task 6.0 — Provar a jornada full-stack com Playwright (V-FE-02)

- mode: focused (primeira revisão)
- task: 6.0 — `tasks/prd-solicitacao-reserva/6_task.md` (behavioral)
- gate contratual: `scripts/ai-flow/gate.sh --filter="reservation-request"`
- HEAD revisado: `3989729` (inalterado durante a revisão; nenhum commit do validator)
- Resultado: **VALIDATION APPROVED** (0 bloqueios; 3 recomendações não bloqueantes)

## Gate reexecutado pelo validator (exit 0, tudo verde)

1. `scripts/ai-flow/gate.sh --filter="reservation-request"` → `GATE: APROVADO`,
   `testes: ok (reservation-request=2)`. Incluiu: `dotnet format ok`
   (Catalog sln, 2 arquivos), 5 builds `dotnet build ok` (0 warnings/errors),
   `tsc ok` (frontend). Stack real no ar: Catalog :5101, Booking :5102,
   frontend :5173 (verificado via portas + `frontend:200`).
2. `npm exec -- vitest run` (regressão 4.0/5.0) → 5 files, **63 passed**.
3. `npm run test:coverage` → 97.35% stmts / 94.21% branch / 95.23% funcs /
   97.35% lines (piso 70% ✅, com folga).
4. `npm run api:generate` + `git diff --exit-code -- src/services/api/generated/booking.ts`
   → **DRIFT:ZERO** (reexecutado pelo validator; nenhum diff).

## Escopo revisado (diff desde o checkpoint 5.0 + untracked)

- Criados: `frontend/localize-stay-frontend/e2e/reservation-request.spec.ts`,
  `playwright.config.ts`, `services/catalog/.../Endpoints/E2EAvailabilityStubEndpoints.cs`.
- Modificados: `package.json`/`package-lock.json` (`test:e2e`, `@playwright/test`),
  `vite.config.ts` (só `test.exclude: e2e/**`), `MiddlewarePipelineExtensions.cs`
  (só wiring do stub), `.gitignore` (`test-results/`, `playwright-report/`),
  `gate.sh` + `gate.contract.md` (bloco de despacho Playwright),
  `6_task.md` (status) + `flow-state.json` (bookkeeping).
- **Nenhum arquivo sob `services/booking/` ou `frontend/.../src/` foi tocado** —
  comportamento de 1.0–5.0 preservado; vitest 63/63 confirma.

## Verificações (a)–(h)

- (a) Browser real, sem mock no caminho E2E: spec importa só `@playwright/test`;
  o único `page.route` usa `route.continue()` (observação, não interceptação).
  Sucesso valida UUID real (regex + ≠ fixture), status `solicitada` e
  `350.00`/`1050.00 BRL` vindos do Booking real. ✅
- (b) Rejeição `PERIODO_INVALIDO` prova 0 POSTs a `/v1/reservations`, erro
  `O check-out deve ser posterior ao check-in.` e ausência do resumo. A via é a
  validação local (comportamento 5.0, por desenho); autoridade backend provada
  em 3.0. ✅
- (c) Stub fiel ao `api-contract.yaml` (§availability-check, ex. 200:
  active/maxGuests 4/available true/`350.00`/`BRL`; 404 `ACCOMMODATION_NOT_FOUND`
  com shape ProblemDetails) e 404 p/ qualquer outro id. Marcado removível no
  cabeçalho do arquivo, no wiring e referendado pelo próprio contrato
  (linhas 199–202: substituir quando Catalog F04 chegar). ✅
- (d) Timeout 60s escopado só à asserção do heading de sucesso; `retries: 0`;
  latência ~9s vem do publish best-effort do backend 3.0 (inalterado aqui).
  Margem ~6x, sem retry que mascare flake. Aceitável. ✅ (ver R1)
- (e) Drift zero, reexecutado pelo validator. ✅
- (f) Gate contratual + vitest + coverage + drift, todos reexecutados verdes
  pelo validator. `--all-tests` (suítes dotnet 1.0–3.0) não rodado: fora do
  contrato desta task e sem arquivos .NET de Booking tocados. ✅
- (g) Dredd residual: `grep dredd` vazio em `scripts/ai-flow/`, `6_task.md` e
  `frontend-techspec.md` — fora do contrato de verificação desta task;
  inviabilidade sem alterar fonte contratual já aceita nas reviews de 3.0.
  Não bloqueante. ✅
- (h) Sem regressão 1.0–5.0 (arquivos intactos + suite verde). ✅

## Bloqueios

Nenhum.

## Recomendações não bloqueantes

- R1: registrar a duração do cenário de sucesso no CI; se o p99 do 201 se
  aproximar de 30s, revisitar o timeout 60s ou o publish best-effort (3.0).
- R2: na full validation do PRD, cobrir o bloco Playwright de `gate.sh` /
  `gate.contract.md`, que viaja neste diff (artefato gate-creator, documentado
  no anexo do contrato).
- R3: ao chegar Catalog F04, remover `E2EAvailabilityStubEndpoints.cs` + wiring
  (já sinalizado no código) e revalidar esta task contra o Catalog oficial.

---

## Revalidação — F1 (attempt 2, worker fresco)

- mode: revalidation — SOMENTE F1 + regressão mínima do entorno E2E.
  F2/dredd fora de escopo (já julgados não bloqueantes no full).
- HEAD revisado: `d2db5a3` (reopen 6.0; fix F1 em unstaged; inalterado durante
  esta revisão — nenhum commit, código, checkbox ou frontmatter tocado pelo
  validator; `flow-state.json` já sujo antes, só bookkeeping do reopen).
- Fix sob revisão (diff unstaged, só wiring — zero código de produto):
  `scripts/dev-up.sh` (+7: exporta `Cors__AllowedOrigins__0/1` de `:5175`
  só no startup do Booking + unset), `e2e/start-stack.mjs` (+58/-1:
  `probeBookingCors()` não-fatal + `VITE_BOOKING_API_URL` explícito),
  `frontend/.../playwright.config.ts` (+1: plumbing `E2E_BOOKING_URL`).
  `services/booking/**` e `frontend/.../src/**` intactos
  (`git diff --name-only` vazio nos dois) — `CorsExtensions.cs`/`appsettings`
  preservados, nada de 1.0–5.0 alterado.
- Stack pelo caminho sancionado: `dev-down.sh` (Booking antigo sem o fix) →
  `scripts/dev-up.sh` destacado via `setsid` (workaround R4 do full: o tooling
  mata o grupo de processo no timeout), sem env extra. Catalog :5101,
  Booking :5102, Payment :5103, frontend :5173 — todos `/health/ready` 200.

### Evidências executadas pelo validator (todas nesta sessão)

| # | Comando | Resultado |
|---|---|---|
| (b1) | preflight `OPTIONS /v1/reservations` `Origin: http://localhost:5175` | **204 + `Access-Control-Allow-Origin: http://localhost:5175`** ✅ |
| (b2) | preflight `OPTIONS` `Origin: http://127.0.0.1:5175` | **204 + `Access-Control-Allow-Origin: http://127.0.0.1:5175`** ✅ |
| (b3) | preflight `OPTIONS` `Origin: http://localhost:5173` (regressão) | **204 + header correto** — dev :5173 continua permitido ✅ |
| (a1) | `gate.sh --filter="reservation-request"` (canônico, sem env extra) | **GATE: APROVADO** — `reservation-request=2 rtl=0 e2e=2`, tsc ok ✅ |
| (a2) | repetição 1× contra flake, mesmo comando | **GATE: APROVADO** — idêntico a (a1) ✅ |
| (c) | `gate.sh --filter="PropertyUpdate"` (jornada da irmã) | **GATE: APROVADO** — `PropertyUpdate=14 rtl=12 e2e=2` ✅ |

- F1 resolvida na causa raiz do wiring: o Booking subido pelo `dev-up.sh`
  agora permite as duas origens do harness E2E (`:5175` localhost +
  127.0.0.1) de forma aditiva; o default de produto (`:5173`) continua
  permitido (b3). O `probeBookingCors()` do harness é só diagnóstico
  (não-fatal) — a irmã, que partilha o harness mas não chama o Booking,
  segue verde (c).
- Bloqueios: nenhum. F1 encerrada; F2/dredd seguem não bloqueantes por
  julgamento do full, sem mudança relevante nesta árvore.

### Resultado

**VALIDATION APPROVED** (F1 corrigida e provada 2/2 pelo caminho sancionado;
irmã intacta; diff restrito ao wiring).
