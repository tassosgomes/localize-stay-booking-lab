# PRD Review — FULL — fundação-fase0

- base_ref: `7deaec0f44da4e492b7dad0e6bbf2c03694b97ef`
- validated_commit (HEAD revisado): `84a5d9a8efe517f4b168cd3c60da0c19d27a50b3`
- validated_tree: `f3e61b323c664429e6a54bb3f1d3c144c35a4bfa`
- branch: `feature/prd-fundacao-fase0` (worktree `...-prd-fundacao-fase0`)
- Specs selecionadas: `tasks/prd-fundacao-fase0/techspec.md` (Status Aprovado, 2026-09-11) + ADRs 001/002/003 (referência)
- Estabilidade: HEAD e árvore idênticos antes e depois da revisão. Único modificado no worktree é
  `tasks/prd-fundacao-fase0/flow-state.json` (estado operacional do integrator, pré-existente);
  únicos untracked são `scripts/ai-flow/gate.sh` + `gate.contract.md` (harness do fluxo, sem código de produto).
- design-patterns Review: não presente no repo — não consultado; nenhuma pressão concreta por refatoração encontrada.

## Estágio 1 — Gate (antes de qualquer material semântico)

Comando: `scripts/ai-flow/gate.sh --base=7deaec0f44da4e492b7dad0e6bbf2c03694b97ef --all-tests`
Resultado: `GATE: APROVADO`, exit 0 (executado 2x; segunda execução confirma exit 0).

- Arquivos alterados: 160 (.NET: 98, node: 16).
- Format: `dotnet format` ok nas 5 slns; lint frontend pulado (sem eslint/prettier local — registrado pelo gate).
- Build: 5 slns `0 Warning(s) 0 Error(s)` + `tsc ok` no frontend.
- Testes (contagens apuradas em execução própria, somando ao veredito do gate):
  - `RegisterRabbitMq.Tests`: 9 passed.
  - Booking sln: 4 (HealthCheck) + 2 (`LocalizeStay.Messaging.IntegrationTests`, V-03, Testcontainers — Docker funcionou).
  - Catalog sln: 4. Payment sln: 4. Vitest: 1 arquivo, 2 testes passed.
  - Notification sln: **sem projeto de teste** — `dotnet test` só restaura, exit 0. Cobertura de V-03
    mora nos 2 testes de mensageria da sln Booking (conforme gate contract da 6.0). Ver recomendação R1.

## Escopo revisado

Diff completo `7deaec0..84a5d9a`: 158 arquivos, +10327/−20. As 20 deleções são só flips
`pending→done`/`[ ]→[x]` (9 tasks + `tasks.md`) e 1 linha do `.gitignore` (ganha 9 linhas de hardening
`bin/obj/TestResults/node_modules/dist`). Nenhuma deleção de código de produto — sem regressão visível.

Rastreabilidade (todas done, todas com focused **VALIDAÇÃO APROVADA**):

| Fatia | Task | Focused | Evidência full |
|-------|------|---------|----------------|
| EN-01 | 1.0 | APROVADA (2 recs) | props/editorconfig/.config presentes; gate --static passou na época |
| EN-02 | 2.0 | APROVADA (2 recs) | 001–004.sql + verify-grants + README; senhas só via `-v`/env; grants mínimos + revogação cruzada (linhas 25–60 de 004-grants.sql) |
| V-01 | 3.0 | APROVADA (3 recs) | Catalog sobe :5101, `/health/ready` real (4 testes Testcontainers) |
| V-02 | 4.0 | APROVADA (2 recs) | Booking :5102, mesmo padrão |
| V-02 | 5.0 | APROVADA (2 recs) | Payment :5103, mesmo padrão |
| V-03 | 6.0 | APROVADA (3 recs) | `DiagnosticsTopology` idêntica nos dois lados (exchange `diagnostics.topic`, routing `diagnostics.ping`, fila `notification.diagnostics`, CE type igual); Worker acrescenta DLX/DLQ; consumer com ACK/NACK+DLQ/quorum/correlationId-causationId; vhost `/localize-stay` pinado nos dois `RabbitMqSettings` (fail-fast fora do vhost) |
| V-04 | 7.0 | APROVADA (3 recs) | Frontend chama `GET /health/ready` direto nas 3 `VITE_*_URL`; sem proxy/gateway no `vite.config.ts`; CORS `http://localhost:5173` nas 3 APIs; portas 5101–5104/5173 consistentes em appsettings, `.env.development`, README |
| V-05 | 8.0 | APROVADA (3 recs) | `export-openapi.sh` extrai Swagger real (com credenciais dummy, sem tocar infra); AsyncAPI espelha a topologia (nomes conferidos); `ConnectionStrings` vazias nos `appsettings.json` (segredo via env/user-secrets) |
| V-06 | 9.0 | APROVADA (3 recs) | `PayloadBuilder` → `CustomMessaging` + tags; PAT só via `--pat`/`OPENMETADATA_PAT`, mascarado no log (9 testes) |

Contratos entre tasks: props→serviços (5 slns buildam contra os mesmos props), bootstrap→schemas
(healthchecks Testcontainers consomem roles/schemas do EN-02), Booking→Worker (topologia espelhada,
AsyncAPI como testemunha), serviços→frontend (CORS + `/health/ready` texto `Healthy`), 6.0→8.0/9.0
(topologia documentada e registrada). Segurança: sem segredo no diff (grep de
password/secret/token/PAT no diff, fora lockfiles, retorna vazio); `002-roles.sql` recusa rodar sem
variáveis; `004-grants.sql` sem `CREATE` cruzado e sem `CREATE ON DATABASE`. Arquitetura: Clean
Architecture por serviço, Worker de projeto único enxuto (10 arquivos, sem Domain/Application —
decisão documentada no próprio consumer), frontend Base sem gateway (ADR-003).

Divergência conhecida (cosmética, já registrada no review da 9.0): frontmatter da 9.0 diz
`verification_type: static`, mas `gate_command`/`gate_test_selector` são o filtro
`RegisterRabbitMq.PayloadBuilderTests` — o plano (tasks.md) prevê filtro. Não reaberta: sem defeito real.

## Bloqueantes

Nenhum.

## Recomendações (não bloqueantes): 3

- R1 — Cobertura mora na sln vizinha: V-03 é provado por `LocalizeStay.Messaging.IntegrationTests`
  na sln Booking; a sln Notification não tem projeto de teste (`dotnet test` nela é no-op verde).
  Gatilho futuro: se o Worker ganhar lógica própria além de transporte, um projeto de teste na sln
  dele passa a ser necessário; hoje, documentar essa decisão onde a task 6.0 declara o gate basta.
- R2 — Duplicação espelhada de `DiagnosticsTopology`/`RabbitMqSettings` (Booking × Worker) é
  intencional (slns independentes), mas divergência silenciosa futura é o risco. Gatilho: quando a
  topologia sair do diagnóstico, extrair pacote compartilhado ou teste de contrato cruzado
  (AsyncAPI como oráculo) — hoje os nomes foram conferidos manualmente e estão iguais.
- R3 — `catalog.json`/`payment.json` exportados com `paths: {}` (skeleton sem endpoints de negócio).
  Esperado nesta fase; o primeiro PRD de domínio deve incluir re-export + diff de contrato no gate
  para impedir regressão silenciosa do `export-openapi.sh`.

## Veredito

**FULL VALIDATION APROVADA** — validated_commit `84a5d9a8efe517f4b168cd3c60da0c19d27a50b3`,
validated_tree `f3e61b323c664429e6a54bb3f1d3c144c35a4bfa`, base
`7deaec0f44da4e492b7dad0e6bbf2c03694b97ef`; 3 recomendações não bloqueantes, 0 bloqueantes.
