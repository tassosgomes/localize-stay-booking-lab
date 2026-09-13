# Resumo de Tarefas de Implementação — Consulta de Reserva (Booking F02)

> **PRD de origem:** [`prd.md`](./prd.md)
> **TechSpec backend de origem:** [`techspec.md`](./techspec.md) — Status: Aprovado — revisão de
> 2026-09-12
> **TechSpec frontend de origem:** [`frontend-techspec.md`](./frontend-techspec.md) — Status:
> Aprovado — revisão de 2026-09-12 (o PRD exige os dois lados: três das quatro User Stories são de
> "Guest"/"frontend de teste" e a seção "Experiência do Usuário" descreve a UI; nenhum plano parcial
> só de backend seria completo)
> **API Contract:** [`api-contract.yaml`](./api-contract.yaml) (v1.0.0) — consumido por referência,
> fonte única de schemas/erros; não duplicado nas tasks. `api-contract.md` (registro de decisões do
> contrato) permanece "Em Revisão" mas não bloqueia — as duas TechSpecs já foram aprovadas consumindo
> este YAML como está.
> **ADRs pertinentes:** [ADR-001](../../docs/adr/adr-001-backend-stack-dotnet.md) (stack .NET),
> [ADR-003](../../docs/adr/adr-003-frontend-teste-react.md) (frontend de teste React, cliente fino,
> CORS direto). Nenhuma ADR nova (techspec.md §Architecture Decision Records).
> **Status do plano:** Confirmado para implementação
> **Regra de entrega:** cada task de comportamento é uma fatia vertical validável isoladamente

> **Pré-requisito externo (fora do escopo destas tasks):** Booking F01 (`tasks/prd-solicitacao-reserva`)
> já está em `main` (PR #4, commit `fbeea05`) — `Reservation`, `ReservationSaga`, `SagaState`,
> `Dispatcher`/`IDispatcher` (lado de comando), `IReservationRepository`, `ReservationRepository`,
> `GlobalExceptionHandler`, `ApplicationExtensions`, `ReservationEndpoints`, `CustomWebApplicationFactory`
> (Testcontainers Postgres/RabbitMQ), `BookingIntegrationTestCollection` e a estrutura intermediária do
> frontend de teste (`src/features`, `src/services/apiClient.ts`, `src/config/env.ts`,
> `FormErrorSummary`, `OperationFeedback`, `src/test/mocks/{handlers,server}.ts`) — tudo isso é
> reaproveitado sem recriação nas tasks abaixo. `scripts/ai-flow/gate.sh` já existe e suporta
> `--filter="<selector>"`, `--static` e `--all-tests`, mesmo padrão de `prd-solicitacao-reserva`.

## Visão Geral

Implementa `GET /v1/reservations/{reservationId}` (Booking F02) de ponta a ponta, backend e
frontend: qualquer solicitante que conheça o identificador de uma Reservation consulta seus dados
congelados por F01, o estado atual do ciclo de vida (`solicitada`/`confirmada`/`cancelada`) e a
situação observável da saga de pagamento (`pendente`/`autorizado`/`rejeitado`), incluindo o motivo
quando cancelada. É a segunda feature de negócio de Booking, a primeira *query* do serviço (CQRS
nativo estendido com o lado de leitura) e a terceira feature de negócio do frontend de teste. Como o
domínio hoje só produz `Solicitada`/`PaymentPending` (F03/F04 ainda não existem), esta entrega também
completa o vocabulário de leitura (`SagaState.Authorized`/`Rejected`,
`ReservationSaga.CancellationReason`) como habilitador, sem introduzir nenhuma lógica de transição —
essa continua sendo responsabilidade de uma feature futura (F04).

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|-------|---------|------------|
| `dotnet-architecture` | `.claude/skills/dotnet-architecture` | Extensão do CQRS nativo com o lado de query (`IQuery`/`IQueryHandler`), exceção de domínio específica (`ReservationNotFoundException`), reaproveito do `IExceptionHandler` global |
| `dotnet-dependency-config` | `.claude/skills/dotnet-dependency-config` | Migration EF Core aditiva (`cancellation_reason`), sem pacote novo |
| `dotnet-testing` | `.claude/skills/dotnet-testing` | Unitário (xUnit/Moq) para o query handler; integração (`WebApplicationFactory` + Testcontainers Postgres) reaproveitando `CustomWebApplicationFactory`/`BookingIntegrationTestCollection` |
| `restful-api` | `.claude/skills/restful-api` | Já aplicada em `api-contract.yaml`; referenciada para o novo `code` do `GlobalExceptionHandler` |
| `react-architecture` | `.claude/skills/react-architecture` | Estrutura `src/features/reservation-lookup`, API pública via `index.ts`, fronteira de imports |
| `react-testing` | `.claude/skills/react-testing` | Vitest + RTL + MSW para os 6 cenários de contrato, Playwright para a jornada crítica |

## Fases de Implementação

### Fase 1 — Completar o vocabulário de leitura da saga (EN-01 backend)
`SagaState.Authorized`/`Rejected` e `ReservationSaga.CancellationReason` passam a existir no domínio
e no schema `booking` (migration aditiva), sem nenhum método de transição — puro vocabulário que a
consulta poderá projetar.

### Fase 2 — Endpoint completo de RF-01 (V-01 backend)
`GET /v1/reservations/{reservationId}` responde exatamente os 5 cenários do AC de RF-01 (3 combinações
de estado/saga + 404 + 400), reaproveitando o `Dispatcher` estendido com o lado de query.

### Fase 3 — Integração frontend tipada com o contrato desta feature (EN-01 frontend)
Tipos gerados de `api-contract.yaml` desta feature (`reservationDetail.ts`, arquivo próprio — distinto
de `booking.ts` de F01), `reservationDetailApi.ts` e handlers MSW dos 6 cenários; pode avançar em
paralelo à Fase 1/2 (nenhum arquivo compartilhado com o backend).

### Fase 4 — Jornada completa de consulta na UI (V-01 frontend)
`/reservations/consultar` cobre formulário de um campo, validação local de UUID, loading e os 5
cenários do AC de RF-01 (200×3 + 400 + 404) mais 500, num único incremento acessível — mesma
indivisibilidade que o PRD exige (Plano de Rollout Faseado).

### Fase 5 — Fechamento full-stack com E2E real (V-02 frontend)
Jornada Playwright (encontrada + não encontrada) contra Booking real, contrato gerado sem drift e gate
completo — fecha RF-01 nos dois lados.

## Mapa de Entrega e Feedback

| Slice | Task | Comportamento observável | Gate executável | Seletor focalizado | Bloqueado por |
|-------|------|---------------------------|--------------------|------------------------|----------------|
| EN-01 (backend) | 1.0 | `SagaState` ganha `Authorized`/`Rejected`; `ReservationSaga` ganha `CancellationReason`; migration aditiva aplica sem afetar linhas existentes | `gate.sh --static` | N/A (static) | Nenhum |
| V-01 (backend) | 2.0 | `GET /v1/reservations/{id}` retorna 200 com todos os campos (3 estados × 3 situações de saga) e distingue 400/404 exatamente como o `api-contract.yaml` | `gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.GetReservationByIdQueryHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationQueryEndpointTests"` | `GetReservationByIdQueryHandlerTests` (unit) + `ReservationQueryEndpointTests` (integration) | 1.0 |
| EN-01 (frontend) | 3.0 | `reservationDetailApi.getById` envia `GET /v1/reservations/{id}` tipado e classifica os 6 cenários de resposta (200×3, 400, 404, 500) via MSW, sem rede real | `gate.sh --filter="reservationDetailApi.test"` | `reservationDetailApi.test.ts` | Nenhum (independente de 1.0/2.0) |
| V-01 (frontend) | 4.0 | Solicitante preenche `/reservations/consultar` com um identificador e recebe, no mesmo incremento, os 5 cenários do AC de RF-01 (200×3 + 400 + 404) mais 500, acessível | `gate.sh --filter="ReservationLookupPage.test" --filter="reservationIdValidation.test"` | `ReservationLookupPage.test.tsx` (+ `reservationIdValidation.test.ts`) | 3.0 |
| V-02 (frontend) | 5.0 | Jornada Playwright encontrada + não encontrada contra Booking real; tipos gerados sem drift; gate completo (lint/type-check/coverage/build) verde | `gate.sh --filter="reservation-lookup"` | `e2e/reservation-lookup.spec.ts` | 2.0, 4.0 |

### Habilitadores inevitáveis

| Enabler | Task | Justificativa de horizontalidade | Menor validação | Desbloqueia |
|---------|------|----------------------------------|------------------|-------------|
| EN-01 backend | 1.0 | O contrato e o PRD exigem representar `autorizado`/`rejeitado` e o motivo de cancelamento, mas F01 só implementou `PaymentPending`; sem esses valores no domínio/schema, a projeção de leitura de 2.0 não tem de onde ler — vocabulário já documentado em `domains/booking/domain.md` §3, não uma decisão nova (techspec.md §Habilitadores inevitáveis) | `dotnet build` da solution Booking sem erros; migration gerada contém a coluna nova (evidência estática, ver 1.0) | V-01 backend (2.0) |
| EN-01 frontend | 3.0 | Tipos gerados do OpenAPI desta feature, `reservationDetailApi.ts` e handlers MSW são o único ponto de contato do frontend com o contrato de F02; tanto a fatia de UI (4.0) quanto o E2E/drift-check (5.0) os consomem — duplicá-los criaria parsing de `ProblemDetails` e mocks divergentes entre as duas fatias (frontend-techspec.md §Sequenciamento) | `reservationDetailApi.test.ts` prova os 6 cenários via MSW sem componente algum | V-01 frontend (4.0), V-02 frontend (5.0) |

## Tarefas

- [x] 1.0 Completar vocabulário de `SagaState`/`CancellationReason` no schema `booking` (EN-01 backend)
- [ ] 2.0 Endpoint completo `GET /v1/reservations/{reservationId}`: 5 cenários do AC de RF-01 (V-01 backend)
- [ ] 3.0 Preparar integração frontend tipada com o contrato desta feature (EN-01 frontend)
- [ ] 4.0 Consultar reserva pela interface acessível: 5 cenários + 500 (V-01 frontend)
- [ ] 5.0 Provar a jornada full-stack com Playwright e fechar o gate (V-02 frontend)

## Rastreabilidade US → Tasks

| User Story | Tasks Relacionadas | Tipo de Cobertura |
|------------|---------------------|------------------------|
| Guest consulta sua Reservation pelo identificador para saber se foi confirmada, cancelada ou aguarda pagamento | 1.0, 2.0, 3.0, 4.0, 5.0 | Direta (backend + UI + E2E) |
| Autor/arquiteto em estudo consulta `sagaStatus` e `correlationId` para observar a jornada entre Booking e Payment | 1.0, 2.0, 4.0 | Direta |
| Frontend de teste busca por identificador e exibe dados/estado da saga sem acesso ao banco | 3.0, 4.0, 5.0 | Direta |
| Guest recebe resposta clara quando o identificador não corresponde a nenhuma Reservation | 2.0 (404 HTTP), 4.0 (UI tom neutro), 5.0 (cenário E2E) | Direta |

## Validação de Cobertura

### Requisitos Funcionais

| Requisito | Task(s) | Status |
|-----------|---------|--------|
| RF-01 — `solicitada` + saga pendente (sem motivo de cancelamento) | 1.0 (vocabulário base já existe), 2.0, 3.0, 4.0, 5.0 | ✅ Coberto |
| RF-01 — `confirmada` + saga autorizada | 1.0 (vocabulário novo), 2.0, 3.0, 4.0 | ✅ Coberto |
| RF-01 — `cancelada` + saga rejeitada + motivo de cancelamento | 1.0 (vocabulário novo), 2.0, 3.0, 4.0 | ✅ Coberto |
| RF-01 — identificador bem formado sem Reservation correspondente → 404 `RESERVATION_NOT_FOUND` | 2.0, 3.0, 4.0, 5.0 | ✅ Coberto |
| RF-01 — identificador em formato inválido → 400 `VALIDATION_ERROR`, distinto de 404 | 2.0, 3.0, 4.0 | ✅ Coberto |
| Experiência do Usuário — jornada `/reservations/consultar`, acessibilidade WCAG 2.1 AA da fatia entregue | 4.0 | ✅ Coberto |
| Jornada full-stack real (browser → Booking) e ausência de drift entre contrato e tipos gerados | 5.0 | ✅ Coberto |

### Artefatos da TechSpec

| Artefato | Task | Status |
|----------|------|--------|
| `Domain/Reservations/SagaState.cs` (+`Authorized`,+`Rejected`), `ReservationSaga.cs` (+`CancellationReason`), `Infra/Persistence/Configurations/ReservationSagaConfiguration.cs` (+coluna), migration `AddSagaCancellationReason` | 1.0 | ✅ |
| `Domain/Reservations/Exceptions/ReservationNotFoundException.cs`, `Application/Reservations/{GetReservationByIdQuery,GetReservationByIdQueryValidator,GetReservationByIdQueryHandler}.cs` | 2.0 | ✅ |
| `Application/Reservations/IReservationRepository.cs` (+`GetByIdAsync`), `Infra/Persistence/ReservationRepository.cs` (+impl), `Application/Cqrs/Dispatcher.cs` (+`IQuery`/`IQueryHandler`) | 2.0 | ✅ |
| `Api/Contracts/ReservationDetailResponseDto.cs`, `Api/Endpoints/ReservationEndpoints.cs` (+GET), `Api/ErrorHandling/GlobalExceptionHandler.cs` (+case), `Api/Extensions/ApplicationExtensions.cs` (+DI) | 2.0 | ✅ |
| `src/services/api/generated/reservationDetail.ts` (gerado), `src/features/reservation-lookup/api/reservationDetailApi.ts`, handlers MSW desta feature em `src/test/mocks/handlers.ts` | 3.0 | ✅ |
| `.../reservation-lookup/{index.ts,pages/ReservationLookupPage.tsx,components/ReservationDetailView.tsx,validation/reservationIdValidation.ts}` | 4.0 | ✅ |
| `src/App.tsx` (rota `/reservations/consultar`) | 4.0 | ✅ |
| `e2e/reservation-lookup.spec.ts` | 5.0 | ✅ |
| `package.json` (`api:generate`, terceira chamada) | 3.0 | ✅ |

### Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill Relacionada | Status |
|---|-----------|----------------|----------------------|--------|
| 1 | Setup / Configuração | 1.0 (migration EF) | `dotnet-dependency-config` | ✅ |
| 2 | Modelos de Dados | 1.0 (vocabulário domínio/schema), 3.0 (tipos gerados) | `dotnet-architecture`, `react-architecture` | ✅ |
| 3 | Lógica de Negócio | 2.0 (query handler, projeção `sagaStatus`) | `dotnet-architecture` | ✅ |
| 4 | Endpoints / Interfaces | 2.0 (HTTP GET), 4.0 (rota `/reservations/consultar`, formulário) | `restful-api`, `react-architecture` | ✅ |
| 5 | Integrações Externas | 3.0 (Booking HTTP via `reservationDetailApi`) — sem integração externa nova no backend (leitura pura do próprio schema `booking`) | `react-testing` | ✅ |
| 6 | Validações e Erros | 2.0 (`GetReservationByIdQueryValidator`, `ReservationNotFoundException`, `GlobalExceptionHandler`), 4.0 (`reservationIdValidation`, mapeamento de tom/erro) | `dotnet-code-quality`, `react-testing` | ✅ |
| 7 | Testes | Subtarefas em 1.0–5.0 (unit/integration .NET; Vitest/RTL/MSW; Playwright em 5.0) | `dotnet-testing`, `react-testing` | ✅ |
| 8 | Observabilidade | 2.0 (log estruturado por ponto de decisão: recebida/encontrada/não encontrada/rejeição de formato) | — | ✅ (nível básico herdado do baseline; sem métricas/tracing nesta fase) |
| 9 | Documentação | N/A — `api-contract.yaml` já é a fonte única de contrato; check de drift em 3.0/5.0 substitui doc adicional | — | ✅ (justificado) |
| 10 | Segurança | N/A — Fase 0 não implementa autenticação/autorização (PD-002); nenhuma PII real exposta | — | ✅ (justificado) |

### Coesão e Faixa de Tamanho

| Task | slice_type | Criar | Modificar | Subtarefas | Fatias | Faixa | Justificativa |
|------|------------|-------|-----------|------------|--------|-------|----------------|
| 1.0 | enabling | 1 | 3 | 4 | N/A | ✅ | Dentro da faixa budget; habilitador justificado (ver Habilitadores inevitáveis) |
| 2.0 | vertical | 7 | 6 | 6 | 1 | ⚠️ | Dentro da contagem de criação budget, mas 6 modificações e complexidade `high`: os 5 cenários do AC de RF-01 só formam um comportamento verificável quando a extensão do `Dispatcher` (lado de query), o repositório, o endpoint e o `GlobalExceptionHandler` existem juntos — dividir por camada quebraria o gate único (mesmo racional de 3.0/5.0 em `prd-solicitacao-reserva`) |
| 3.0 | enabling | 3 | 2 | 4 | N/A | ✅ | Dentro da faixa budget; habilitador justificado |
| 4.0 | vertical | 6 | 1 | 6 | 1 | ⚠️ | Dentro da contagem budget, mas complexidade `high`: a TechSpec frontend proíbe fatiar V-01 por tipo de resposta — recriaria, na UI, o rollout interno que o PRD já proíbe no backend |
| 5.0 | vertical | 1 | 0 | 3 | 1 | ✅ | Dentro da faixa budget; cria só o spec Playwright, reaproveitando tudo de 2.0/3.0/4.0 |

### Integridade dos Gates

| Task | Gate | Teste/fixture disponível | Filtro isolado | Repo compilável | Dependência futura | Status |
|------|------|-----------------------------|-------------------|--------------------|------------------------|--------|
| 1.0 | `gate.sh --static` | N/A (static — sem lógica de transição a testar; a leitura desses valores é validada na própria task 2.0) | Sim (`--static` sem filtro) | Sim | Não | ✅ |
| 2.0 | `gate.sh --filter="FullyQualifiedName~...GetReservationByIdQueryHandlerTests" --filter="FullyQualifiedName~...ReservationQueryEndpointTests"` | Criado na própria task | Sim | Sim | Não | ✅ |
| 3.0 | `gate.sh --filter="reservationDetailApi.test"` | Criado na própria task (MSW) | Sim | Sim | Não | ✅ |
| 4.0 | `gate.sh --filter="ReservationLookupPage.test" --filter="reservationIdValidation.test"` | Criado na própria task; consome `reservationDetailApi`/tipos/MSW (3.0) já existentes | Sim | Sim | Não | ✅ |
| 5.0 | `gate.sh --filter="reservation-lookup"` | Criado na própria task (`e2e/reservation-lookup.spec.ts`); consome UI (4.0) e endpoint real (2.0) já existentes | Sim | Sim | Não | ✅ |

### Ciclo de Vida de Artefatos Compartilhados

| Artefato | Primeira task produtora | Tasks consumidoras | Dependências consistentes | Status |
|----------|----------------------------|--------------------------|---------------------------------|--------|
| `SagaState.Authorized`/`Rejected`, `ReservationSaga.CancellationReason` | 1.0 | 2.0 | Sim | ✅ |
| `GetReservationByIdQuery`/`Handler`/`Validator`, `ReservationNotFoundException`, `IReservationRepository.GetByIdAsync` | 2.0 | 2.0 (único consumidor nesta feature) | Sim | ✅ |
| `src/services/api/generated/reservationDetail.ts`, `reservationDetailApi.ts`, handlers MSW desta feature | 3.0 | 4.0, 5.0 | Sim | ✅ |
| `ReservationLookupPage` (API pública da feature), rota `/reservations/consultar` | 4.0 | 5.0 (alvo do E2E) | Sim | ✅ |

Nenhuma task depende de artefato produzido por task posterior. 3.0 é independente de 1.0/2.0 (nenhum
arquivo compartilhado); 5.0 é a única task que depende de um artefato backend (2.0, o endpoint real) e
de um artefato frontend (4.0, a UI) ao mesmo tempo, porque é o ponto que prova a jornada full-stack.

## Análise de Paralelização

### Lanes de Execução Paralela

| Lane | Tarefas | Descrição |
|------|---------|------------|
| Lane A (backend) | 1.0 → 2.0 | Vocabulário/migration e depois o endpoint que os consome — sequencial dentro da lane |
| Lane B (frontend) | 3.0 → 4.0 | Integração tipada (tipos, `reservationDetailApi`, MSW) e depois a jornada de UI — arquivos disjuntos do backend |

Lane B pode começar imediatamente (só depende do contrato aprovado e da estrutura intermediária já
em `main`), em paralelo à Lane A inteira, porque 3.0/4.0 usam MSW/Prism, não o backend real.

### Caminho Crítico

1.0 → 2.0 (backend) e 3.0 → 4.0 (frontend) avançam em paralelo; 5.0 é o ponto de convergência final —
depende de 2.0 (backend real) e de 4.0 (UI) e é o único checkpoint que prova a jornada full-stack
exigida pela User Story do "frontend de teste".

### Diagrama de Dependências

```
1.0 (Vocabulário SagaState/CancellationReason) ──→ 2.0 (Endpoint completo: 5 cenários) ─┐
                                                                                          ├─→ 5.0 (E2E + gate completo)
3.0 (Integração frontend tipada) ──→ 4.0 (Jornada UI: 5 cenários + 500) ────────────────┘
```
