# Resumo de Tarefas de Implementação — Cadastro de Property

> **PRD de origem:** [`prd.md`](./prd.md) — aprovado em 2026-09-12.
> **TechSpecs consumidas:** [`techspec.md`](./techspec.md) e
> [`frontend-techspec.md`](./frontend-techspec.md) — revisões aprovadas em 2026-09-12.
> **Contrato consumido:** [`api-contract.yaml`](./api-contract.yaml) — OpenAPI 3.1, versão 1.0.0,
> aprovado em 2026-09-12; [`api-contract.md`](./api-contract.md) contém o racional aprovado.
> **ADRs pertinentes:** [ADR-001 — backend .NET](../../docs/adr/adr-001-backend-stack-dotnet.md) e
> [ADR-003 — frontend React sem gateway](../../docs/adr/adr-003-frontend-teste-react.md).
> **Status do plano:** Em revisão — completo; a task 1.0 é `high` e requer revisão explícita antes da implementação.
> **Regra de entrega:** cada task de comportamento é uma fatia vertical validável isoladamente.

## Visão Geral

O plano entrega F01 ponta a ponta: o Catalog cria e edita `Property` com PostgreSQL, contrato e
erros observáveis; o frontend gera tipos do OpenAPI, oferece cadastro acessível e permite editar a
`Property` criada na mesma sessão. A última fatia prova a jornada real create→edit no browser.

As pré-condições permanecem no plano da Fundação. Backend 1.0 depende das tasks 1.0, 2.0, 3.0 e 8.0
da Fundação; frontend 3.0 depende da Fundação 7.0; backend 2.0 reutiliza a catalogação da Fundação
9.0. `scripts/ai-flow/gate.sh` ainda deve ser produzido por `tsg-flow-gate-creator` antes do despacho.

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|---|---|---|
| `dotnet-architecture` | `.agents/skills/dotnet-architecture` | Clean Architecture, Service Pattern, portas e ProblemDetails |
| `dotnet-dependency-config` | `.agents/skills/dotnet-dependency-config` | EF Core/PostgreSQL, migration, Unit of Work, pacotes e DI |
| `dotnet-program-setup` | `.agents/skills/dotnet-program-setup` | `Program.cs` apenas como orquestrador |
| `dotnet-testing` | `.agents/skills/dotnet-testing` | xUnit, WebApplicationFactory e Testcontainers |
| `dotnet-observability` | `.agents/skills/dotnet-observability` | logs estruturados, traceId e health checks |
| `react-architecture` | `.agents/skills/react-architecture` | estrutura intermediária, API pública e aliases |
| `react-testing` | `.agents/skills/react-testing` | Vitest/RTL/MSW, acessibilidade e Playwright |
| `test-guide` | `.agents/skills/test-guide` | testes por fronteira e sem duplicação de wiring |

## Fases de Implementação

### Fase 0 — Pré-condições externas

Concluir as dependências da Fundação e criar o gate determinístico. F01 não recria solution,
schemas, frontend base, export inicial de contratos ou configuração do OpenMetadata.

### Fase 1 — Backend de cadastro e integração frontend

1.0 e 3.0 podem avançar em paralelo: 1.0 entrega o POST real; 3.0 fixa tipos, cliente e mocks.

### Fase 2 — Comportamentos de criação e edição

2.0 entrega PATCH e ownership. 4.0 entrega cadastro acessível na UI com MSW, em paralelo ao backend.

### Fase 3 — Fechamento full-stack

5.0 entrega edição na UI e executa E2E create→edit e ownership negado contra o Catalog real,
confirmando também que os tipos gerados permanecem sem drift.

## Mapa de Entrega e Feedback

| Slice | Task | Comportamento observável | Gate executável | Seletor focalizado | Bloqueado por |
|---|---|---|---|---|---|
| V-BE-01 | 1.0 | POST persiste Property ativa; inválidos não persistem e duplicatas são aceitas | `gate.sh --filter="FeatureSlice=PropertyCreate"` | trait xUnit `PropertyCreate` | Fundação 1/2/3/8 |
| V-BE-02 | 2.0 | PATCH altera somente dados permitidos; falhas preservam estado | `gate.sh --filter="FeatureSlice=PropertyUpdate"` | trait xUnit `PropertyUpdate` | 1.0, Fundação 9 |
| EN-FE-01 | 3.0 | Cliente tipado envia POST/PATCH e interpreta ProblemDetails via MSW | `gate.sh --filter="propertyApi.test"` | `propertyApi.test.ts` | Fundação 7 |
| V-FE-01 | 4.0 | Host cadastra pela UI e recebe resumo/erros acessíveis | `gate.sh --filter="PropertyCreate.test"` | testes create | 3.0 |
| V-FE-02 | 5.0 | Host edita pela UI; browser prova create→edit real e 403 | `gate.sh --filter="PropertyUpdate"` | testes Vitest/Playwright update | 1.0, 2.0, 4.0 |

### Habilitadores inevitáveis

| Enabler | Task | Justificativa de horizontalidade | Menor validação | Desbloqueia |
|---|---|---|---|---|
| EN-FE-01 | 3.0 | Tipos gerados, parser ProblemDetails e MSW são um contrato compartilhado por create/update; duplicá-los criaria DTOs e mocks concorrentes | teste MSW do adapter prova headers, bodies, PATCH parcial e parsing | V-FE-01, V-FE-02 |

Não há habilitador backend novo. A Fundação é dependência externa, não copiada.

## Tarefas

- [ ] 1.0 Cadastrar Property ativa por HTTP
- [ ] 2.0 Editar dados cadastrais preservando ownership e estado
- [ ] 3.0 Preparar integração frontend tipada com o contrato Catalog
- [ ] 4.0 Cadastrar Property pela interface acessível
- [ ] 5.0 Editar Property na interface e provar a jornada full-stack

## Rastreabilidade US → Tasks

| User Story | Tasks relacionadas | Tipo de cobertura |
|---|---|---|
| Host cadastra hospedagem | 1.0, 3.0, 4.0, 5.0 | Backend, UI e E2E |
| Host corrige nome/localização | 2.0, 3.0, 5.0 | Backend, UI e E2E |
| Autor observa respostas inequívocas | 1.0–5.0 | HTTP, UI acessível e browser real |

## Validação de Cobertura

### Requisitos Funcionais

| Requisito | Task(s) | Status |
|---|---|---|
| RF-01 — criar, validar e aceitar duplicata | 1.0, 4.0, 5.0 | ✅ Coberto |
| RF-02 — editar e preservar identidade/Host/status | 2.0, 5.0 | ✅ Coberto |
| Experiência, preservação de valores e acessibilidade | 4.0, 5.0 | ✅ Coberto |
| Contrato versionado e sem drift | 1.0, 2.0, 3.0, 5.0 | ✅ Coberto |

### Artefatos das TechSpecs

| Inventário | Primeira produtora / modificação | Status |
|---|---|---|
| Aggregate, portas, persistência, migration, POST, erros e testes backend | 1.0 | ✅ |
| Ownership/not-found, PATCH, contrato final e testes backend | 2.0 | ✅ |
| Tipo OpenAPI, `propertyApi`, env/apiClient e MSW | 3.0 | ✅ |
| API pública, página, create form, resumo, feedback, validação e testes | 4.0 | ✅ |
| Edit form, update state, handlers, Playwright e drift check | 5.0 | ✅ |
| Base da Fundação: Program, app Vite, Swagger, DbContext e fixtures | dependências externas | ✅ |

### Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill relacionada | Status |
|---|---|---|---|---|
| 1 | Setup / Configuração | 1.0, 3.0; base Fundação | dependency/program setup, react architecture | ✅ |
| 2 | Modelos de Dados | 1.0, 2.0; transporte 3.0 | dotnet/react architecture | ✅ |
| 3 | Lógica de Negócio | 1.0, 2.0 | dotnet architecture | ✅ |
| 4 | Endpoints / Interfaces | 1.0, 2.0, 4.0, 5.0 | dotnet/react architecture | ✅ |
| 5 | Integrações Externas | PostgreSQL, OpenMetadata e browser/API | dependency config, react testing | ✅ |
| 6 | Validações e Erros | 1.0–5.0 | architecture/testing | ✅ |
| 7 | Testes | todas as tasks | dotnet/react testing, test-guide | ✅ |
| 8 | Observabilidade | 1.0/2.0 logs+traceId; 4.0/5.0 feedback | dotnet observability | ✅ |
| 9 | Documentação | contrato/export 1.0/2.0; env 3.0 | — | ✅ |
| 10 | Segurança | ownership 2.0/5.0; auth real N/A pelo baseline | architecture/testing | ✅ |
| 11 | Acessibilidade | 4.0, 5.0 | react testing | ✅ |
| 12 | Eventos/cache/consulta | N/A — fora de F01 | — | ✅ |

### Coesão e Faixa de Tamanho

| Task | slice_type | Criar | Modificar | Subtarefas | Fatias | Faixa | Justificativa |
|---|---|---:|---:|---:|---:|---|---|
| 1.0 | vertical | 24+ | 10 | 6 | 1 | ⚠️ | `high`: primeira fatia backend; separar destruiria o gate |
| 2.0 | vertical | 6 | 10 | 6 | 1 | ⚠️ | jornada PATCH única; alterações são inseparáveis da atomicidade |
| 3.0 | enabling | 8 | 7 | 5 | N/A | ✅ | contrato/infra compartilhados mínimos para duas fatias |
| 4.0 | vertical | 10 | 2 | 6 | 1 | ⚠️ | componentes pequenos formam um único fluxo create acessível |
| 5.0 | vertical | 4 | 7 | 6 | 1 | ✅ | edição e E2E fecham a mesma jornada crítica |

Somente 1.0 é `high`. As exceções numéricas preservam compilação e checkpoint coeso.

### Integridade dos Gates

| Task | Gate | Teste/fixture disponível | Filtro isolado | Repo compilável | Dependência futura | Status |
|---|---|---|---|---|---|---|
| 1.0 | `FeatureSlice=PropertyCreate` | criado na task; fixture Fundação 3 | Sim | Sim | Não | ✅ Planejado |
| 2.0 | `FeatureSlice=PropertyUpdate` | criado/modificado na task | Sim | Sim | Não | ✅ Planejado |
| 3.0 | `propertyApi.test` | teste/handlers criados na task | Sim | Sim | Não | ✅ Planejado |
| 4.0 | `PropertyCreate.test` | teste criado; MSW da 3.0 | Sim | Sim | Não | ✅ Planejado |
| 5.0 | `PropertyUpdate` | Vitest/Playwright criados; backend anterior | Sim | Sim | Não | ✅ Planejado |

Os comandos são planejados, não executados. O gate deve existir antes do despacho e reprovar se o
filtro encontrar zero testes.

### Ciclo de Vida de Artefatos Compartilhados

| Artefato | Primeira produtora | Consumidoras | Dependências consistentes | Status |
|---|---|---|---|---|
| solution, DbContext, Swagger e fixture Catalog | Fundação 3.0 | 1.0, 2.0, 5.0 | Sim | ✅ |
| schema/role `catalog` | Fundação 2.0 | 1.0, 2.0 | Sim | ✅ |
| frontend Vite, env/apiClient e MSW base | Fundação 7.0 | 3.0–5.0 | Sim | ✅ |
| `contracts/openapi/catalog.json` | Fundação 8.0 | 1.0, 2.0, Fundação 9.0 | Sim | ✅ |
| aggregate/service/repository/controller/error handler | 1.0 | 2.0, 5.0 | Sim | ✅ |
| tipos gerados, `propertyApi` e mocks | 3.0 | 4.0, 5.0 | Sim | ✅ |
| página, resumo e estado de criação | 4.0 | 5.0 | Sim | ✅ |

Nenhuma task valida com teste, fixture ou arquivo produzido por task futura.

## Análise de Paralelização

### Lanes de Execução Paralela

| Lane | Tarefas | Descrição |
|---|---|---|
| Backend | 1.0 → 2.0 | API, domínio e persistência |
| Frontend | 3.0 → 4.0 | adapter e cadastro UI com MSW |
| Integração | 5.0 | converge backend e frontend |

O executor standard continua sequencial; lanes são apenas informação de planejamento.

### Caminho Crítico

`Fundação → (1.0 → 2.0) + (3.0 → 4.0) → 5.0`. Cada fatia anterior possui feedback próprio.

### Diagrama de Dependências

```text
Fundação 1/2/3/8 ─▶ 1.0 POST ─▶ 2.0 PATCH ─────────────┐
                                                       ├─▶ 5.0 edição UI + E2E real
Fundação 7 ─────────▶ 3.0 adapter ─▶ 4.0 cadastro UI ──┘
Fundação 9 ────────────────────────▶ 2.0 catalogação
gate.sh ──────────────────────────▶ todas as tasks
```
