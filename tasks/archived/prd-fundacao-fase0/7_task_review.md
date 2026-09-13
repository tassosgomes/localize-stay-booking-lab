# Revisão — Task 7.0: Frontend exibe status dos 3 serviços via CORS (V-04)

- Modo: focused (primeira revisão)
- Task: `tasks/prd-fundacao-fase0/7_task.md` (lida integralmente, 158 linhas; vertical/behavioral frontend)
- Checkpoint base: `67f8bac` (= HEAD atual; diff = worktree + untracked)
- HEAD revisado: `67f8baca787e9d694b9305cb8efaef26c52b6fc3` (sem commits novos)
- Escopo revisado: diff worktree (7 arquivos: `.gitignore`, `README.md`, 3×
  `CorsExtensions.cs`, `7_task.md`, `flow-state.json`) + untracked
  `frontend/localize-stay-frontend/**` (17 arquivos fonte, sem `node_modules/`/`dist/`).
  `scripts/` untracked é infra do gate, fora do escopo da task. `7_task.md`
  (`status: validating`) e `flow-state.json` são operacionais do flow, sem valor semântico.
- Skills consultadas: `react-architecture` (estrutura Base), `react-testing` (MSW + feliz/erro).

## Gate (antes da semântica)

- Contrato: `scripts/ai-flow/gate.sh --filter="ServiceStatus.test"` — seletor
  `ServiceStatus.test.tsx`; esperado: 3 `fetch` mockados (MSW) retornando 200 →
  3 serviços "Healthy"; 1 falha → status de erro correspondente.
- Resultado do gate (executado pelo validator, antes de qualquer leitura semântica):
  `GATE: APROVADO` — dotnet builds ok (4 solutions, 0 warnings/errors), `tsc ok`,
  `testes: ok (ServiceStatus.test=2)`, `EXIT_CODE=0`.
- Reexecução independente de evidência (validator, Testcontainers n/a nesta task):
  - `npm run typecheck` em `frontend/localize-stay-frontend` → `tsc --noEmit` limpo (`EXIT 0`).
  - `npm run test -- ServiceStatus.test` → `2 passed (2)` em
    `src/components/ServiceStatus/ServiceStatus.test.tsx` (`EXIT 0`).
  - `npm run build` → `tsc --noEmit && vite build`, 31 módulos, `✓ built in 1.25s` (`EXIT 0`).
  - Critério "o seletor encontra pelo menos um teste e não executa casos sem relação":
    atendido — só o arquivo focalizado executou (1 test file, 2 tests).

## Verificações do escopo (a–g)

- (a) estrutura Base sem `features/*`: APROVADO. `src/` contém `components/`,
  `config/`, `hooks/` (`.gitkeep`), `services/`, `utils/` (`.gitkeep`), `App.tsx`,
  `main.tsx`, `vite-env.d.ts` — sem diretório `features/` (verificado: `NO features
  dir`). Correto para a única tela de fundação sem domínio de negócio, conforme
  `react-architecture` (nível Base por necessidade real) e TechSpec.
- (b) sem gateway/BFF (fetch direto às 3 URLs): APROVADO.
  `src/services/healthService.ts:18` faz `fetch(`${baseUrl}/health/ready`)` por serviço,
  agregados em `checkAllServices` (`:35-41`); `ServiceStatus.tsx:12` chama direto.
  Nenhuma dependência de proxy/roteador (`package.json` sem `react-router`/`axios`/
  proxy; grep `gateway|/bff|BrowserRouter` zero ocorrências). Conforme ADR-003.
- (c) sem runtime-config (decisão techspec): APROVADO. Grep recursivo por
  `runtime-env|envsubst|RUNTIME_ENV|window.__ENV` retorna 0 matches; `vite.config.ts:4-5`
  documenta a decisão ("Sem pipeline de runtime-config nesta fase"). Env só via
  `import.meta.env` + `.env.development` (dev local) — desvio deliberado já fechado,
  não reaberto.
- (d) teste cobre feliz + erro; MSW mockando fetch: APROVADO.
  `ServiceStatus.test.tsx`: `setupServer` com 3 handlers `Healthy` (`:22-26`); caso feliz
  asserta `Catalog/Booking/Payment: Healthy` (`:43-50`); caso de falha sobrescreve Payment
  com 503 via `server.use` e asserta `Payment: Unhealthy (HTTP 503)` (`:52-62`).
  Isolamento por teste (`resetHandlers`, `cleanup`, `unstubAllEnvs` em `:32-36`), queries
  semânticas (`findByText`), matchers `jest-dom`, sem chamadas reais — conforme
  `react-testing` (mínimo para componente com API: integração com MSW, sucesso + erro).
- (e) env.ts tipado + `.env.development`: APROVADO. `src/config/env.ts` expõe
  `FrontendEnv` (`:4-8`) e `getEnv()` com `required()` que lança erro nomeando a variável
  ausente (`:10-16`); leitura em tempo de chamada permite `vi.stubEnv` nos testes.
  `.env.development` define `VITE_CATALOG_URL=http://localhost:5101`,
  `VITE_BOOKING_URL=http://localhost:5102`, `VITE_PAYMENT_URL=http://localhost:5103` —
  consistentes com `appsettings.json` (`"urls"` dos 3 serviços) e com o README.
- (f) CORS inclui origem do Vite sem abrir demais: APROVADO. Os 3 `CorsExtensions.cs`
  ganham `FrontendDevOrigin = "http://localhost:5173"` (`:12`) mesclado de forma
  idempotente ao `Cors:AllowedOrigins` da configuração (`:17`), aplicado via
  `WithOrigins(origins)` (`:23`) — sem `AllowAnyOrigin`/wildcard de origem, sem
  `AllowCredentials`/`SetIsOriginAllowed` (grep zero). `AllowAnyMethod` + `AllowAnyHeader`
  seguem o padrão pré-existente do repo e são aceitáveis para o dev server local.
  Porta confere com `vite.config.ts:9` (`port: 5173`). Nota "sem wildcard em produção":
  origem fixa de dev, nada global.
- (g) node_modules/dist fora do versionamento: APROVADO. `.gitignore:7-9` adiciona
  `node_modules/` e `dist/`; `git ls-files` não lista nenhum dos dois (existem só em
  disco, instalados para executar a evidência). `dist/` gerado pelo `npm run build` do
  validator permanece ignorado (status pós-build idêntico).

## Checkpoint manual (fora do gate, não reprova)

- Subtarefa 7.x / critério "feedback checkpoint": `npm run dev` + 3 serviços reais →
  3 status "Healthy" sem erro de CORS no console. Evidência manual NÃO executada pelo
  validator (exigiria 3 backends + navegador) e, conforme a convocação, a ausência do
  `npm run dev` manual NÃO reprova. Registrado como limitação: a prova automatizada
  (MSW) cobre contrato/renderização, mas não o handshake CORS real navegador→Kestrel.
  README.md ("Como rodar o frontend localmente", `:132-153`) documenta o procedimento
  com portas, origem CORS e comando de teste — suficiente para reprodução futura.

## Bloqueantes

Nenhum.

## Recomendações (não bloqueantes, 3)

1. Cobrir o caminho `catch` do `checkHealth` (falha de rede/CORS — `healthService.ts:26-32`)
   com um terceiro caso no mesmo arquivo (ex. `server.use(... network error)` ou
   `HttpResponse.error()`): hoje o 503 exercita `!response.ok`, mas o `detail` de exceção
   (que é exatamente o que um bloqueio de CORS produziria no navegador) não tem asserção.
   A task exige "pelo menos um caso de falha" — atendido — isto é só endurecimento.
2. Imports com extensão `.ts` explícita (`ServiceStatus.tsx:2-3`: `../../config/env.ts`):
   funciona (`allowImportingTsExtensions` + bundler, `tsc` verde), mas destoa da convenção
   Vite/TS de omitir a extensão; uniformizar em revisão futura de higiene evita ruído em
   tooling que não resolve extensões explícitas.
3. `FrontendDevOrigin` hardcoded nos 3 serviços: correto para esta fase dev-only, mas se a
   porta do Vite mudar, são 4 lugares para atualizar (3× C# + `vite.config.ts`); considerar,
   quando houver deploy real do frontend, mover a origem para `Cors:AllowedOrigins` de
   configuração/ambiente em vez de constante — sem ação agora.

## Imutabilidade

- HEAD antes e depois da revisão: `67f8baca787e9d694b9305cb8efaef26c52b6fc3` (inalterado);
  `git status --short` idêntico (7 modificados + `?? frontend/` + `?? scripts/`).
- Nenhum arquivo de código, status, task ou commit foi editado pelo validator; apenas
  leitura, `gate.sh`, `npm run typecheck/test/build` (artefatos `node_modules/` e `dist/`
  regenerados, ambos ignorados) e a escrita deste relatório. Nova validação exigida se o
  código mudar.

## Resultado

VALIDAÇÃO APROVADA (3 recs)
