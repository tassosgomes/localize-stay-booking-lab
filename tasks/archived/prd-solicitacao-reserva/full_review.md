# FULL Validation — PRD Solicitação de Reserva (Booking F01)

- Mode: **full** (worker fresco; reviews focused 1.0–6.0 usados só como referência, julgando a árvore FINAL)
- base_ref: `f5d6cda4c63487b64b9e8a13811d1e8f21d4a846` (origin/main, já com a irmã cadastro-property)
- validated_commit: `da4dfdccb40491df5c8821fc786823849bdc083a`
- validated_tree: `9b46c222d3f528c1a9e9a96eb8ed90e0559d019d`
- Diff revisado: `git diff f5d6cda..da4dfdc` (100 arquivos: Booking + frontend reserva + stub Catalog + reviews)
- HEAD e árvore conferidos idênticos no início e no fim. Único dirty: `flow-state.json`
  (já sujo ANTES desta revisão; não tocado) + `full_review.md` (este relatório, deliverable
  mandatado — um `full_review.md` stale de tentativa anterior foi sobrescrito).
  Nenhum código, checkbox, frontmatter ou commit alterado pelo validator.

## Resultado

**FULL VALIDATION REJECTED** — 1 bloqueante (F1, task dona **6.0**), 1 lacuna sem dono
não bloqueante (F2, glob do gate vindo da irmã), 4 recomendações não bloqueantes.

## Gates reexecutados pelo validator (todos nesta sessão)

| # | Comando | Resultado |
|---|---|---|
| 1a | `gate.sh --filter="...UnitTests.Reservations.ReservationTests" --filter="...IntegrationTests.Reservations.ReservationPersistenceTests"` | **APROVADO** — 21 + 2, 5 sln 0W/0E (Docker/Testcontainers ok) |
| 1b | `gate.sh --filter="...IntegrationTests.Catalog.CatalogAvailabilityClientTests"` | **APROVADO** — 4/4 |
| 1c | `gate.sh --filter="...IntegrationTests.Reservations.ReservationEndpointTests"` | **APROVADO** — 7/7 (7 cenários, status/code exatos + evento com correlation/causation) |
| 2a | `gate.sh --filter="reservationApi.test"` (string da task 4.0) | **REPROVADO exit 1 — 0 testes** (ver F2). Com `reservationApi`: **APROVADO 14/14** |
| 2b | `gate.sh --filter="ReservationRequestPage.test" --filter="reservationFormValidation.test" --filter="reservationErrorMapping.test"` | **REPROVADO exit 1 — 0 testes** (ver F2). Sem sufixo: **APROVADO 17+20+10** |
| 2c | `gate.sh --all-tests` (dotnet todas as sln + vitest completo) | **APROVADO** (vitest direto: **102/102 em 9 arquivos** — 63 reserva + 39 irmã/fundação) |
| 3 | `gate.sh --filter="reservation-request"` (E2E, stack real) | **REPROVADO 3 execuções** (sucesso falha determinística, ver F1); **APROVADO 1 execução** somente com config env extra de diagnóstico (não-sanctioned) — 2/2 e2e |
| 4 | `npm run api:generate` + `git diff --exit-code` booking.ts e catalog.ts | **DRIFT ZERO** nos dois |
| 5 | Dredd | Fora do gate por decisão registrada; quadro estrutural inalterado (nota: binário `dredd v14.1.0` agora existe no ambiente; bloqueios estruturais permanecem — ver seção) |

Stack E2E (operação de ambiente permitida): `dotnet ef database update` Catalog
(`20260912164736_AddProperties` aplicada; Booking já estava up-to-date) + Catalog
:5101, Booking :5102, frontend :5173 no ar via `dotnet run`/`npm run dev` destacados.
Stub `availability-check` verificado ao vivo (200 fixture + 404 outro id, fiel ao YAML).
Probe direta `POST /v1/reservations` → **201 em ~18s** com corpo exato
(`solicitada`, `350.00`/`1050.00` BRL). Serviços deixados no ar; container
E2E residual `localize-stay-e2e-pg` removido ao final.

## Evidência consolidada por task (árvore final)

- **1.0** (domínio+persistência): gate 1a verde; RN-02–RN-06, saga `PaymentPending`,
  migration substitui sentinela. ✅ Sem regressão.
- **2.0** (cliente Catalog): gate 1b verde; 200/404/500/timeout, sem retry
  (`RequestsReceived == 1`); `Directory.Packages.props` sem dup (builds 0W/0E). ✅
- **3.0** (endpoint 7 cenários + evento): gate 1c verde; `code`/status/`title`/
  `problem+json` exatos, 503 com `traceId`, 0 persistências/publicações nas
  rejeições, publish best-effort (log confirma swallow após 5 tentativas
  `312 NO_ROUTE` — broker sem consumidor, comportamento previsto). ✅
- **4.0** (adapter tipado + MSW): 14/14 (seletor sem sufixo); `guestReference` só
  no body; drift zero; `booking.ts` preservado na convergência. ✅ (ressalva F2)
- **5.0** (UI 7 desfechos + a11y): 47/47; fronteira de imports intacta
  (`App.tsx` importa só o `index.ts` público); `apiClient.ts` unificado
  `{url}|{baseUrl,path}` com consumidores dos dois lados verdes (102/102);
  lifecycle MSW dedupado (listen/close só em `setup.ts`, reset por arquivo). ✅
- **6.0** (E2E + gate): ❌ **F1 abaixo**. Stub fiel ao contrato e marcado
  removível; `server.ts`, `env.ts` dual, `App.tsx` com 3 links (`/`, `/properties`,
  `/reservations`), `vite.config.ts` da irmã intacto (`e2e/**` excluído),
  `api:generate` duplo (booking+Catalog) sem drift. ✅ tudo, exceto o aceite E2E.
- **Irmã (`/properties`)**: RTL verde dentro dos 102/102, rota/handlers/`catalog.ts`
  preservados, E2E próprio não reexecutado (fora da lista mandatada). ✅ Sem regressão.
- **Rastreabilidade US→tasks** (`tasks.md`): tabela presente e completa; todos os
  7 cenários de RF-01 mapeados (sucesso + 5×422 + 503 + 400/500 na UI). ✅

## F1 (BLOQUEANTE — task dona 6.0): E2E sucesso reprova no gate canônico por CORS

- Fato: `gate.sh --filter="reservation-request"` (invocação canônica, sem env extra)
  reprova o cenário de sucesso em **3/3 execuções** (2 via gate + 1 isolada).
  Sintoma na UI (error-context.md): `alert` "Erro inesperado ao solicitar a
  reserva" — o adapter classifica como `failed`, nunca chega ao resumo.
- Causa isolada (determinística, reproduzida fora do gate): o harness E2E
  (`playwright.config.ts` default `E2E_VITE_PORT=5175` + `e2e/start-stack.mjs` —
  ambos vindos da irmã via main, `git log` confirma; o commit 6.0 `da4dfdc`
  adicionou só o spec) sobe o browser em origem que o Booking :5102 **não
  permite**: `CorsExtensions.cs` (fundação) fixa só `http://localhost:5173`.
  Prova: preflight `OPTIONS` com `Origin: http://127.0.0.1:5175` → `204` **sem**
  `Access-Control-Allow-Origin` (browser bloqueia o POST); com
  `Origin: http://localhost:5173` → header correto. O tooling 6.0/irmã injeta
  `Cors__AllowedOrigins__*` via env **só no Catalog** (`start-stack.mjs`),
  nunca no Booking — e o Booking da jornada vem da stack dev externa.
- Diagnóstico ambiental (não converte em aprovação): reiniciado o Booking com
  `Cors__AllowedOrigins__0/1=http://localhost:5175,http://127.0.0.1:5175`
  (só env, zero código) → gate canônico **APROVADO, 2/2 e2e**. O código de
  produto está correto; quebrado é o **wiring E2E da árvore entregue**: num
  checkout limpo seguindo só passos sancionados (`dev-up.sh` + gate), o aceite
  V-FE-02 é vermelho. Máscara de ambiente não corrige a árvore.
- Atribuição: **6.0** — o aceite "jornada Playwright contra Booking/Catalog reais
  + gate completo verde" (`tasks.md` V-FE-02) é obrigação dela na árvore integrada;
  a correção (fiar origem E2E × CORS de Booking no tooling/runbook da 6.0)
  pode tocar arquivos partilhados, mas a obrigação é da 6.0. Nenhuma task nova.
- Nota: 1 execução isolada da rejeição passou (caminho 100% local, sem POST);
  rejeição passa 3/3 no gate — o caminho local é sólido; o defeito é só o
  sucesso via rede cross-origin.

## F2 (NÃO BLOQUEANTE — lacuna sem dono): filtros com sufixo `.test` selecionam zero

- Fato: `--filter="reservationApi.test"`, `"ReservationRequestPage.test"`,
  `"reservationFormValidation.test"`, `"reservationErrorMapping.test"` (strings
  documentadas em 4.0/5.0 e nesta ordem) selecionam **zero** arquivos no
  `gate.sh` atual (`find -name "*<filtro>*.test.ts"` nunca casa com sufixo
  `.test`). Exit 1 pelo Invariante 2 — verificado que NÃO é suite ausente.
- Não é regressão deste PRD: o gate atual veio da irmã (commit `6c7088b` em main)
  e quebra até os filtros documentados **da própria irmã**
  (`--filter="propertyApi.test"` falha; `--filter="propertyApi"` passa).
  Todas as suítes existem e passam (14 + 17 + 20 + 10; 102/102 no total).
- Sem dono neste PRD (harness de main; não inventar task). Cobertura 100%
  verificada por seletores equivalentes + suíte completa.

## Dredd (item 5): quadro inalterado, com uma nota

- `grep dredd` vazio em `scripts/ai-flow/`, `6_task.md`, `frontend-techspec.md` —
  nada mudou na decisão registrada.
- Nota: o binário `dredd v14.1.0` agora existe no ambiente (antes ausente).
  Não reverte a decisão: o path de dependência `availability-check` continua
  implementado **só** pelo stub removível (`grep` confirma arquivo único em
  `services/catalog/src`), então o dredd seguiria inviável sem alterar fonte
  contratual. Conformidade coberta por integração 7 cenários + E2E real.

## Recomendações (não bloqueantes)

- R1: ao reabrir 6.0, alinhar origem E2E × CORS de Booking (origem `localhost`
  no harness, ou `Cors__AllowedOrigins` do E2E no runbook/`start-stack`/dev-up
  para o Booking) — decisão do implementer; reverificar com o gate canônico
  limpo (sem env extra).
- R2: observar a margem do timeout de 60s do cenário de sucesso: o publish
  best-effort contra broker sem consumidor custa ~8–18s (`312 NO_ROUTE` +
  backoffs) antes do 201; somado ao cold-start do vite :5175, a margem é
  pequena neste laboratório. Comportamento previsto, mas vale monitorar p99.
- R3: atualizar as strings `gate_command` com sufixo `.test` (4.0/5.0 e irmã)
  para o glob do gate atual, ou corrigir o glob no `gate.sh` — dono: harness/main.
- R4: `dev-up.sh` morre com o grupo de processo se a chamada for interrompida
  (serviços recebem shutdown); subir com `setsid` destacado quando orquestrado
  por ferramenta. Só tooling de ambiente, fora do PRD.

---

# FULL Validation — RODADA 2 (pós-correção F1; worker fresco)

- Mode: **full** (julgando a árvore FINAL contra `tasks/prd-solicitacao-reserva/` +
  `prd.md`; focused 1.0–6.0, round-1 full e revalidation 6.0 usados só como referência)
- base_ref: `f5d6cda4c63487b64b9e8a13811d1e8f21d4a846` (inalterada)
- validated_commit: `fc5adc9468fa72d31a8ada452b7a8abf7b44bf57`
- validated_tree: `787deb5a7eb15debd7fe194d415734f45abe0eb5`
- Delta desde o commit validado da rodada 1 (`da4dfdc`): **só wiring E2E**
  (`scripts/dev-up.sh` +7, `e2e/start-stack.mjs` +58/-1, `playwright.config.ts` +1)
  + bookkeeping (`6_task_review.md` §revalidação, `flow-state.json`, este relatório).
  **Zero diff** em `services/booking/**` e `frontend/.../src/**` desde `da4dfdc`
  (`git diff da4dfdc..HEAD --name-only` vazio nos dois) — todo o produto 1.0–5.0
  intacto; julgamento semântico da rodada 1 segue válido para ele.
- HEAD e árvore conferidos idênticos no início e no fim desta rodada. Único dirty:
  `flow-state.json` (já sujo ANTES — bookkeeping do reopen, não tocado) +
  `full_review.md` (este relatório, deliverable mandatado). Nenhum código,
  checkbox, frontmatter, `flow-state.json` ou commit alterado pelo validator.

## Resultado

**FULL VALIDATION APROVADA** — F1 encerrada de forma estável (causa raiz no wiring,
provada 2/2 E2E em 2 runs + 3 preflights); nenhum outro comportamento regrediu;
F2 segue não bloqueante sem mudança de quadro; 0 recomendações novas
(R1–R4 da rodada 1 continuam válidas como acompanhamento).

## Gates reexecutados pelo validator nesta rodada (todos nesta sessão)

| # | Comando | Resultado |
|---|---|---|
| 1a | `gate.sh --filter="...UnitTests.Reservations.ReservationTests" --filter="...IntegrationTests.Reservations.ReservationPersistenceTests"` | **APROVADO** — 21 + 2, 5 builds 0W/0E |
| 1b | `gate.sh --filter="...IntegrationTests.Catalog.CatalogAvailabilityClientTests"` | **APROVADO** — 4/4 |
| 1c | `gate.sh --filter="...IntegrationTests.Reservations.ReservationEndpointTests"` | **APROVADO** — 7/7 |
| 2a | `gate.sh --filter="reservationApi"` (sem sufixo; ver F2) | **APROVADO** — 14/14 |
| 2b | `gate.sh --filter="ReservationRequestPage" --filter="reservationFormValidation" --filter="reservationErrorMapping"` | **APROVADO** — 17+20+10 |
| 2c | suíte vitest completa no dir do frontend | **APROVADO** — **102/102 em 9 arquivos** (63 reserva + 39 irmã/fundação) |
| 3a | `gate.sh --filter="reservation-request"` (canônico, run 1) | **APROVADO** — `reservation-request=2 rtl=0 e2e=2` |
| 3b | `gate.sh --filter="reservation-request"` (canônico, run 2, anti-flake) | **APROVADO** — idêntico a 3a |
| 4 | `npm run api:generate` + `git diff --exit-code` booking.ts e catalog.ts | **DRIFT ZERO** nos dois |

Stack E2E (operação de ambiente permitida, já no ar pelo caminho sancionado
`dev-up.sh` com o fix): Catalog :5101, Booking :5102, frontend :5173 `/health/ready`
200. Preflights `OPTIONS /v1/reservations` conferidos ao vivo: `:5175` localhost e
`127.0.0.1` → 204 **com** `Access-Control-Allow-Origin` correto (fix ativo);
`:5173` → header correto (default de produto preservado, sem regressão).

## F1: resolvido de forma estável (task dona 6.0, já aprovada em revalidation)

- Causa raiz confirmada no wiring, correção mínima e aditiva: `dev-up.sh` exporta
  `Cors__AllowedOrigins__0/1` (`:5175` localhost + 127.0.0.1) **só** no startup do
  Booking via env (default de produto `:5173` em `CorsExtensions.cs`/`appsettings`
  intacto); `start-stack.mjs` ganha `probeBookingCors()` não-fatal + plumbing
  `VITE_BOOKING_API_URL`; `playwright.config.ts` ganha `E2E_BOOKING_URL`.
- Estabilidade: E2E canônico **2/2 em 2 runs seguidos nesta rodada** (somados aos 2/2
  ×2 da revalidation = 4 aprovações consecutivas do gate canônico limpo, sem env
  extra) + irmã `PropertyUpdate` 14/14 verificada na revalidation (harness
  partilhado intacto). Nenhum outro comportamento regrediu: backend 21+2/4/7/7,
  frontend 14/17+20+10 e suíte 102/102 todos verdes na árvore final.
- Nota de ambiente (sem efeito no julgamento): `vitest run` invocado da **raiz** do
  repo falha com timeouts `findByRole` (MSW/setup resolvidos pelo cwd); do **dir do
  frontend** — como o gate invoca — passa 102/102. Artefato de invocação, não do código.

## F2: sem mudança de quadro (não bloqueante, sem dono)

- `--filter="reservationApi.test"` segue selecionando zero (`GATE: REPROVADO` por
  Invariante 2, filtro vazio — não suíte ausente), idêntico à rodada 1; cobertura
  provada por seletores equivalentes (14/14) + suíte completa. Harness de main,
  sem dono neste PRD; nenhuma ação requerida.

---

# FULL Validation — RODADA 3 (pós-rebase sobre base externa nova; worker fresco)

- Mode: **full** (esta rodada existe SOMENTE porque a base mudou — regra do fluxo —
  não por defeito; julgamento semântico das rodadas 1–2 reutilizado como referência,
  com reexecução integral dos gates sobre a árvore final)
- base_ref nova: `af25088a8e8c044bf60cc06ca49423563502b62d` (origin/main: pipeline CI
  + USER Dockerfiles; delta `f5d6cda..af25088` = 7 arquivos, 384 inserções, zero
  overlap com o PRD)
- validated_commit: `0ba2d07a7aec47ae3928e608a9d7f907efe1e640`
- validated_tree: `f994a2410a2ec9c2cd30c751ede26907320b3904`
- Diff do PRD revisado: `git diff af25088..0ba2d07` (104 arquivos do PRD, idêntico
  em conteúdo ao da rodada 2)
- HEAD e árvore conferidos idênticos no início e no fim desta rodada. Único dirty:
  `flow-state.json` (já sujo ANTES — bookkeeping do integrator/rebase, não tocado) +
  `full_review.md` (este relatório, deliverable mandatado). Nenhum código, checkbox,
  frontmatter, `flow-state.json` ou commit alterado pelo validator.

## Resultado

**FULL VALIDATION APROVADA** — rebase 10/10 sem conflitos confirmado mecanicamente
puro (delta `fc5adc9..0ba2d07` == delta da base externa, byte a byte, + bookkeeping);
código de produto 1.0–6.0 byte-idêntico ao aprovado na rodada 2; todos os gates
reexecutados verdes na árvore final; F1 segue resolvida (E2E 2/2 × 2 runs), F2 segue
não bloqueante sem mudança de quadro; 0 recomendações novas (R1–R4 da rodada 1
continuam válidas como acompanhamento).

## Rebase: evidência de pureza mecânica

- `git diff fc5adc9..0ba2d07 --stat` = **9 arquivos**: os mesmos 7 da base externa
  (`.dockerignore`, `.github/workflows/ci.yml`, 5 Dockerfiles — 384 inserções,
  idênticas ao delta `f5d6cda..af25088`) + `flow-state.json` (só bookkeeping:
  `full_attempt` 1→2, eventos `task-6.0-fix-done`/`full-approved`/
  `revalidation-required`/`integration-blocked-reports`, `last_checkpoint→fc5adc9`,
  fase `validating→integration`) + `full_review.md` (só §rodada 2).
- Cada um dos 7 arquivos da base está **byte-idêntico à versão em `af25088`**
  (`git diff af25088..HEAD -- <arquivo>` vazio nos 7) — o rebase não editou,
  mesclou nem adaptou nada da base; trouxe verbatim.
- **Zero diff** em `services/booking/**`, `frontend/.../src/**`, `e2e/**`,
  `playwright.config.ts`, `scripts/dev-up.sh`, `scripts/ai-flow/**` desde `fc5adc9`
  (`git diff fc5adc9..HEAD --name-only` vazio em todos) — produto 1.0–5.0 e wiring
  6.0 intactos; vereditos semânticos da rodada 2 seguem válidos sem re-auditoria.

## Gates reexecutados pelo validator nesta rodada (todos nesta sessão, árvore final)

| # | Comando | Resultado |
|---|---|---|
| 1a | `gate.sh --filter="...UnitTests.Reservations.ReservationTests" --filter="...IntegrationTests.Reservations.ReservationPersistenceTests"` | **APROVADO** — 21 + 2, 5 builds 0W/0E |
| 1b | `gate.sh --filter="...IntegrationTests.Catalog.CatalogAvailabilityClientTests"` | **APROVADO** — 4/4 |
| 1c | `gate.sh --filter="...IntegrationTests.Reservations.ReservationEndpointTests"` | **APROVADO** — 7/7 (após 1 flake de infra, ver nota) |
| 2a | `gate.sh --filter="reservationApi"` (sem sufixo; ver F2) | **APROVADO** — 14/14 |
| 2b | `gate.sh --filter="ReservationRequestPage" --filter="reservationFormValidation" --filter="reservationErrorMapping"` | **APROVADO** — 17+20+10 |
| 2c | `gate.sh --all-tests` + `vitest` direto no dir do frontend | **APROVADO** — **102/102 em 9 arquivos** (63 reserva + 39 irmã/fundação) |
| 3a | `gate.sh --filter="reservation-request"` (canônico, run 1) | **APROVADO** — `reservation-request=2 rtl=0 e2e=2` |
| 3b | `gate.sh --filter="reservation-request"` (canônico, run 2, anti-flake) | **APROVADO** — idêntico a 3a |
| 4 | `npm run api:generate` + `git diff --exit-code` booking.ts e catalog.ts | **DRIFT ZERO** nos dois |

Stack E2E (operação de ambiente permitida; stack já no ar, proveniência conferida:
PIDs servindo desta worktree — Catalog :5101, Booking :5102, frontend :5173, todos
`/health/ready` 200; preflight `OPTIONS /v1/reservations` com
`Origin: http://localhost:5175` → 204 **com** `Access-Control-Allow-Origin` correto,
fix F1 ativo). Nenhuma regressão vs rodada 2 em nenhum gate; nenhuma task dona a
atribuir — **zero defeitos encontrados**.

## Notas de ambiente (sem efeito no julgamento)

- N1: a 1ª invocação do filtro 1c falhou em `ResourceReaper`/Ryuk do Testcontainers
  (falha de startup do harness de containers, 7/7 `Failed` em 19ms sem executar
  teste); retry imediato, sem tocar nada, → **7/7 APROVADO**, e todos os demais
  filtros Testcontainers (1a/1b) passaram de primeira. Flake de infra, não regressão:
  o código sob teste é byte-idêntico ao da rodada 2.
- N2: `docker ps` mostra `localize-stay-e2e-pg` residual no ar (43+ min, da stack dev)
  + containers mortos de outros projetos; sem interferência nos resultados.
- F2: quadro inalterado (filtros com sufixo `.test` seguem vazios no gate atual;
  cobertura provada por seletores equivalentes + 102/102). Não re-auditado além
  disso por ausência de mudança relevante.
