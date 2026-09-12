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
