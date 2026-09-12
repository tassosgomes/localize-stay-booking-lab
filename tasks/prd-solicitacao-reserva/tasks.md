# Resumo de Tarefas de Implementação — Solicitação de Reserva (Booking F01)

> **PRD de origem:** [`prd.md`](./prd.md)
> **TechSpec backend de origem:** [`techspec.md`](./techspec.md) — Status: Aprovado — revisão de
> 2026-09-12
> **TechSpec frontend de origem:** [`frontend-techspec.md`](./frontend-techspec.md) — Status:
> Aprovado — revisão de 2026-09-12 (PRD exige os dois lados: RF-01 tem User Story própria de
> "frontend de teste" e seção "Experiência do Usuário" descrevendo a UI; nenhum plano parcial só de
> backend seria completo)
> **API Contract:** [`api-contract.yaml`](./api-contract.yaml) (v1.0.0) — consumido por referência,
> fonte única de schemas/erros; não duplicado nas tasks.
> **ADRs pertinentes:** [ADR-001](../../docs/adr/adr-001-backend-stack-dotnet.md) (stack .NET),
> [ADR-002](../../docs/adr/adr-002-broker-fase0-rabbitmq.md) (convenção de nomes de evento),
> [ADR-003](../../docs/adr/adr-003-frontend-teste-react.md) (frontend de teste React, cliente fino,
> CORS direto).
> **Status do plano:** Confirmado para implementação
> **Regra de entrega:** cada task de comportamento é uma fatia vertical validável isoladamente

> **Pré-requisito externo (fora do escopo destas tasks):** a fundação técnica da Fase 0
> (`tasks/prd-fundacao-fase0`) — solution `LocalizeStay.Booking.*` (Clean Architecture
> `1-Services`..`4-Infra`), schema `booking`/role `booking_role` em `postgres-main`, vhost
> `/localize-stay` com `Rmq.CloudEvents` configurado, `BookingDbContext` com a migration sentinela
> `__bootstrap_check`, os projetos de teste `LocalizeStay.Booking.IntegrationTests` (Testcontainers
> Postgres) e `LocalizeStay.Messaging.IntegrationTests`, e a Fundação V-04 do frontend de teste
> (`frontend/localize-stay-frontend` com React/Vite/TypeScript materializado) — está sendo construída
> em outra worktree e é assumida como pré-existente no ambiente de execução destas tasks (já
> confirmado pelo autor nas duas TechSpecs). Nenhuma task abaixo recria esses artefatos. A feature
> irmã `tasks/prd-cadastro-property` pode já ter promovido o frontend para a estrutura intermediária
> (`src/features`, `apiClient.ts`, `env.ts`, `FormErrorSummary`, `OperationFeedback`) — as tasks 4.0 e
> 5.0 reaproveitam esses artefatos se existirem e os criam apenas se ainda não existirem, sem duplicar
> uma segunda versão concorrente (ordem entre as duas features de frontend é livre, conforme as duas
> TechSpecs).
> `scripts/ai-flow/gate.sh` também é assumido como já gerado (`tsg-flow-gate-creator`), com suporte a
> `--filter="<selector>"` e `--static`, seguindo o mesmo padrão já usado em `prd-fundacao-fase0` e
> `prd-cadastro-property`.

## Visão Geral

Implementa `POST /v1/reservations` (Booking F01) de ponta a ponta, backend e frontend: um Guest
solicita uma Reservation informando Accommodation, período e número de hóspedes; Booking valida
sincronamente contra Catalog (`GET /accommodations/{id}/availability-check`, contrato provisório),
aplica as regras RN-02 a RN-06, congela preço/moeda/total, cria a `Reservation` em `solicitada` +
`ReservationSaga(PaymentPending)` e publica `booking.reservation_requested`. O frontend de teste
React expõe uma única jornada (`/reservations`) que envia a solicitação e exibe a Reservation criada
ou o motivo da rejeição, distinguindo explicitamente rejeição de negócio (422) de falha temporária de
Catalog (503). É a primeira feature de negócio de Booking e o primeiro exercício de integração
síncrona contratada entre domínios do laboratório, além da segunda feature de negócio do frontend de
teste (pode ser implementada em qualquer ordem relativa a `prd-cadastro-property`).

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|-------|---------|------------|
| `dotnet-architecture` | `.claude/skills/dotnet-architecture` | CQRS nativo, exceções de domínio, `IExceptionHandler` global (`examples/cqrs.md`, `examples/error-handling.md`) |
| `dotnet-dependency-config` | `.claude/skills/dotnet-dependency-config` | EF Core (`IEntityTypeConfiguration`, migration), `IHttpClientFactory` + resiliência (timeout, sem retry), `Rmq.CloudEvents` |
| `dotnet-testing` | `.claude/skills/dotnet-testing` | Unitário (xUnit/Moq), integração (`WebApplicationFactory` + Testcontainers Postgres/RabbitMQ, fake HTTP server) |
| `restful-api` | `.claude/skills/restful-api` | RFC 9457 já aplicada em `api-contract.yaml`; referenciada para a extensão `code` do `GlobalExceptionHandler` |
| `react-architecture` | `.claude/skills/react-architecture` | Estrutura `src/features/reservation-request`, API pública via `index.ts`, fronteira de imports |
| `react-testing` | `.claude/skills/react-testing` | Vitest + RTL + `userEvent` + MSW para os 7 cenários, Playwright para a jornada crítica, cobertura mínima 70% |

## Fases de Implementação

### Fase 1 — Domínio e persistência da Reservation (V-01)
`Reservation`/`ReservationSaga` com as regras RN-02 a RN-06 implementadas como lógica pura e
totalmente testadas por unidade, persistidas e lidas de volta no schema `booking` real via EF Core —
substitui a tabela sentinela `__bootstrap_check`.

### Fase 2 — Cliente de disponibilidade de Catalog (V-02)
`ICatalogAvailabilityClient` distingue 200 (fatos), 404 (não encontrada) e falha de infraestrutura
(timeout/5xx/erro de deserialização), sem nenhuma regra de negócio aplicada nesta camada.

### Fase 3 — Endpoint completo de RF-01 (V-03)
`POST /v1/reservations` orquestra local → Catalog → domínio → persistência → publicação, respondendo
exatamente como os 7 cenários (sucesso + 6 rejeições) do `api-contract.yaml`.

### Fase 4 — Integração frontend tipada com o contrato de Booking (EN-FE-01)
Tipos gerados de `api-contract.yaml`, `VITE_BOOKING_API_URL`, `reservationApi.ts` e handlers MSW de
Booking — contrato compartilhado consumido pela fatia de UI (5.0) e pelo E2E (6.0); pode avançar em
paralelo às Fases 1–3 (não compartilha arquivos com o backend).

### Fase 5 — Jornada completa de solicitação na UI (V-FE-01)
`/reservations` cobre formulário, validação local, loading e os 7 cenários de resposta (201 + 400 +
5×422 + 503) num único incremento acessível, provado por RTL + MSW — a mesma indivisibilidade que o
PRD exige no backend (Plano de Rollout Faseado) se aplica à UI.

### Fase 6 — Fechamento full-stack com E2E real (V-FE-02)
Jornada Playwright (sucesso + uma rejeição representativa) contra Booking + Catalog reais, contrato
gerado sem drift e gate completo — fecha RF-01 nos dois lados.

## Mapa de Entrega e Feedback

| Slice | Task | Comportamento observável | Gate executável | Seletor focalizado | Bloqueado por |
|-------|------|---------------------------|--------------------|------------------------|----------------|
| V-01 | 1.0 | `Reservation`/`ReservationSaga` aplicam RN-02 a RN-06 em memória (todas as combinações de `AvailabilityFacts`) e persistem/são lidas de volta no schema `booking` real | `gate.sh --filter="Booking.Reservations.Domain+Persistence"` | `ReservationTests` (unit) + `ReservationPersistenceTests` (integration) | Nenhuma (fundação já provisionada externamente) |
| V-02 | 2.0 | `ICatalogAvailabilityClient` retorna `AvailabilityFacts`, `null` (404) ou lança `CatalogUnavailableException` (timeout/5xx/erro), conforme a resposta de um fake server local | `gate.sh --filter="Booking.IntegrationTests.Catalog.CatalogAvailabilityClientTests"` | `CatalogAvailabilityClientTests` | Nenhuma (independente de 1.0) |
| V-03 | 3.0 | `POST /v1/reservations` responde os 7 cenários do AC de RF-01 exatamente como o `api-contract.yaml`, e o sucesso publica `booking.reservation_requested` com `correlationId`/`causationId` | `gate.sh --filter="Booking.IntegrationTests.Reservations.ReservationEndpointTests"` | `ReservationEndpointTests` | 1.0, 2.0 |
| EN-FE-01 | 4.0 | `reservationApi.request` envia `POST /v1/reservations` tipado e interpreta `ProblemDetails` (400/422×5/503/500) via MSW, sem rede real | `gate.sh --filter="reservationApi.test"` | `reservationApi.test.ts` | Nenhuma (Fundação V-04 externa) |
| V-FE-01 | 5.0 | Guest preenche `/reservations` e recebe, no mesmo formulário, os 7 desfechos do contrato (sucesso com resumo congelado; 6 variações de erro com tom/campo corretos), acessível | `gate.sh --filter="ReservationRequestPage"` | `ReservationRequestPage.test.tsx` (+ `reservationFormValidation.test.ts`, `reservationErrorMapping.test.ts`) | 4.0 |
| V-FE-02 | 6.0 | Jornada Playwright sucesso + uma rejeição contra Booking/Catalog reais; tipos gerados sem drift; gate completo (lint/type-check/coverage/build) verde | `gate.sh --filter="reservation-request"` | `e2e/reservation-request.spec.ts` | 3.0, 5.0 |

### Habilitadores inevitáveis

| Enabler | Task | Justificativa de horizontalidade | Menor validação | Desbloqueia |
|---------|------|----------------------------------|------------------|-------------|
| EN-FE-01 | 4.0 | Tipos gerados do OpenAPI, `reservationApi.ts` e handlers MSW são o único ponto de contato do frontend com o contrato de Booking; tanto a fatia de UI (5.0) quanto o E2E/drift-check (6.0) os consomem — duplicá-los criaria parsing de `ProblemDetails` e mocks divergentes entre as duas fatias | `reservationApi.test.ts` prova request/response/erro via MSW sem componente algum | V-FE-01, V-FE-02 |

Nenhum habilitador novo no backend. A TechSpec backend já registra que nenhum habilitador horizontal
é necessário nesse lado — `Directory.Build.props`, o schema/role `booking` e o vhost `/localize-stay`
já foram entregues pela fundação técnica da Fase 0 (tratada como pré-requisito externo, não repetida
aqui).

## Tarefas

- [x] 1.0 Domínio e persistência da Reservation com RN-02 a RN-06 (V-01)
- [x] 2.0 Cliente de disponibilidade de Catalog: 200/404/falha (V-02)
- [ ] 3.0 Endpoint completo `POST /v1/reservations`: 7 cenários + evento (V-03)
- [ ] 4.0 Preparar integração frontend tipada com o contrato de Booking (EN-FE-01)
- [ ] 5.0 Solicitar reserva pela interface acessível: 7 desfechos (V-FE-01)
- [ ] 6.0 Provar a jornada full-stack com Playwright e fechar o gate (V-FE-02)

## Rastreabilidade US → Tasks

| User Story | Tasks Relacionadas | Tipo de Cobertura |
|------------|---------------------|------------------------|
| Guest solicita reserva com período/hóspedes/Accommodation, preço garantido | 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 | Direta (backend + UI + E2E) |
| Guest é informado imediatamente da rejeição (período/capacidade/indisponibilidade) | 1.0 (regra), 3.0 (superfície HTTP), 5.0 (UI, tom/campo por `code`) | Direta |
| Guest é informado que a validação não pôde ser concluída (Catalog indisponível), sem interpretar como rejeição | 3.0 (503 HTTP), 5.0 (UI com `role="status"`, tom de falha temporária) | Direta |
| Autor/arquiteto em estudo observa a integração síncrona contratada com Catalog | 2.0, 3.0 | Direta |
| Frontend de teste exibe sucesso ou motivo de rejeição | 4.0, 5.0, 6.0 | Direta (antes coberta só como "Suporte" via contrato HTTP; agora implementada) |

## Validação de Cobertura

### Requisitos Funcionais

| Requisito | Task(s) | Status |
|-----------|---------|--------|
| RF-01 — sucesso (Given acomodação ativa/capacidade/disponível → 201 + preço congelado + evento) | 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 | ✅ Coberto |
| RF-01 — período inválido (checkOut ≤ checkIn) → 422 `PERIODO_INVALIDO`, sem chamar Catalog | 1.0 (regra), 3.0 (ordem de execução + HTTP), 5.0 (validação local + UI) | ✅ Coberto |
| RF-01 — hóspedes ≤ 0 → 422 `QUANTIDADE_HOSPEDES_INVALIDA`, sem chamar Catalog | 1.0 (regra), 3.0 (ordem de execução + HTTP), 5.0 (validação local + UI) | ✅ Coberto |
| RF-01 — acomodação inexistente/inativa → 422 `ACOMODACAO_INDISPONIVEL` | 1.0 (regra `Active=false`), 2.0 (404 do Catalog), 3.0 (tradução do 404 + HTTP), 5.0 (UI) | ✅ Coberto |
| RF-01 — capacidade excedida → 422 `CAPACIDADE_EXCEDIDA` | 1.0 (regra), 3.0 (HTTP), 5.0 (UI) | ✅ Coberto |
| RF-01 — indisponível no período → 422 `PERIODO_INDISPONIVEL` | 1.0 (regra), 3.0 (HTTP), 5.0 (UI) | ✅ Coberto |
| RF-01 — Catalog indisponível/erro → 503 `CATALOG_INDISPONIVEL`, nenhuma Reservation criada, não interpretado como rejeição | 2.0 (client), 3.0 (mapeamento + HTTP), 5.0 (UI com tom de falha temporária, `role="status"`) | ✅ Coberto |
| Experiência do Usuário — jornada `/reservations`, acessibilidade WCAG 2.1 AA da fatia entregue | 5.0 | ✅ Coberto |
| Jornada full-stack real (browser → Booking → Catalog) e ausência de drift entre contrato e tipos gerados | 6.0 | ✅ Coberto |

### Artefatos da TechSpec

| Artefato | Task | Status |
|----------|------|--------|
| `Domain/Reservations/{Reservation,ReservationSaga,AvailabilityFacts,ReservationStatus,SagaState}.cs` + `Exceptions/*.cs` | 1.0 | ✅ |
| `Application/Reservations/IReservationRepository.cs` | 1.0 | ✅ |
| `Infra/Persistence/Configurations/{ReservationConfiguration,ReservationSagaConfiguration}.cs` + migration `AddReservationAndSaga` + `ReservationRepository.cs` | 1.0 | ✅ |
| `Application/Reservations/ICatalogAvailabilityClient.cs` | 2.0 | ✅ |
| `Infra/Catalog/{CatalogAvailabilityHttpClient,CatalogClientOptions,CatalogClientExtensions,CatalogUnavailableException}.cs` | 2.0 | ✅ |
| `Application/Reservations/{RequestReservationCommand,RequestReservationCommandHandler,RequestReservationCommandValidator,IReservationRequestedPublisher}.cs` | 3.0 | ✅ |
| `Infra/Messaging/ReservationRequestedRmqPublisher.cs` | 3.0 | ✅ |
| `Api/Endpoints/ReservationEndpoints.cs` + `Api/Contracts/{CreateReservationRequestDto,ReservationResponseDto,MoneyStringJsonConverter}.cs` | 3.0 | ✅ |
| `Api/ErrorHandling/GlobalExceptionHandler.cs` (resolve lacuna: a fundação não criou nenhum `IExceptionHandler` — ver nota em 3.0) | 3.0 | ✅ |
| `Program.cs`, `PersistenceExtensions.cs`, `appsettings.json`, `DiagnosticsEndpoints.cs` (modificações) | 1.0, 2.0, 3.0 | ✅ |
| `src/services/api/generated/booking.ts` (gerado), `src/features/reservation-request/api/reservationApi.ts`, `src/test/mocks/{handlers,server}.ts`, `.env.example` | 4.0 | ✅ |
| `.../pages/ReservationRequestPage.tsx`, `.../components/{RequestReservationForm,ReservationResultSummary}.tsx`, `.../types/reservationForm.ts`, `.../validation/reservationFormValidation.ts`, `.../errors/reservationErrorMapping.ts`, `.../index.ts`, `src/components/{FormErrorSummary,OperationFeedback}.tsx` (se ainda não existirem) | 5.0 | ✅ |
| `e2e/reservation-request.spec.ts` | 6.0 | ✅ |
| `src/config/env.ts`, `src/services/apiClient.ts`, `src/App.tsx` (rota `/reservations`), `package.json` (`api:generate`) (modificações) | 4.0, 5.0 | ✅ |

### Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill Relacionada | Status |
|---|-----------|----------------|----------------------|--------|
| 1 | Setup / Configuração | 2.0 (`CatalogClient:BaseUrl`), 3.0 (`Program.cs`), 4.0 (`VITE_BOOKING_API_URL`, `api:generate`) | `dotnet-dependency-config`, `react-architecture` | ✅ |
| 2 | Modelos de Dados | 1.0 (domínio), 4.0 (tipos de transporte gerados), 5.0 (form state local) | `dotnet-architecture`, `react-architecture` | ✅ |
| 3 | Lógica de Negócio | 1.0 (RN-02 a RN-06 puras), 5.0 (validação local que antecipa RN-02/RN-03) | `dotnet-architecture` | ✅ |
| 4 | Endpoints / Interfaces | 3.0 (HTTP), 5.0 (rota `/reservations`, formulário) | `restful-api`, `react-architecture` | ✅ |
| 5 | Integrações Externas | 2.0 (Catalog HTTP), 3.0 (RabbitMQ), 4.0 (Booking HTTP via `reservationApi`) | `dotnet-dependency-config`, `react-testing` | ✅ |
| 6 | Validações e Erros | 1.0 (exceções de domínio), 3.0 (`GlobalExceptionHandler` + RFC 9457 `code`), 5.0 (`reservationErrorMapping`, `FormErrorSummary`) | `dotnet-code-quality`, `react-testing` | ✅ |
| 7 | Testes | Subtarefas em 1.0–6.0 (unit/integration .NET; Vitest/RTL/MSW; Playwright em 6.0) | `dotnet-testing`, `react-testing` | ✅ |
| 8 | Observabilidade | 3.0 (log estruturado por ponto de decisão com `correlationId`), 5.0 (`traceId` visível em 500, feedback acessível `role="status"`/`role="alert"`) | — | ✅ (nível básico já herdado do baseline; sem métricas/tracing nesta fase) |
| 9 | Documentação | N/A — `api-contract.yaml` já é a fonte única de contrato; check de drift em 4.0/6.0 substitui doc adicional | — | ✅ (justificado) |
| 10 | Segurança | N/A — Fase 0 não implementa autenticação/autorização (`context/architecture-baseline.md`); nenhuma PII real processada; `guestReference` sem validação de identidade | — | ✅ (justificado) |

### Coesão e Faixa de Tamanho

| Task | slice_type | Criar | Modificar | Subtarefas | Fatias | Faixa | Justificativa |
|------|------------|-------|-----------|------------|--------|-------|----------------|
| 1.0 | vertical | 8 | 2 | 6 | 1 | ✅ | Dentro da faixa budget |
| 2.0 | vertical | 4 | 1 | 5 | 1 | ✅ | Dentro da faixa budget |
| 3.0 | vertical | 8 | 2 | 6 | 1 | ⚠️ | Dentro da contagem budget, mas complexidade `high`: os 7 cenários do AC de RF-01 (sucesso + 6 rejeições) só formam um comportamento verificável quando orquestração, endpoint, mapeamento de exceções e publicação existem juntos — dividir por cenário quebraria o gate único e violaria "feature única e indivisível" do PRD (Plano de Rollout Faseado) |
| 4.0 | enabling | 6 | 4 | 5 | N/A | ✅ | Dentro da faixa budget; habilitador justificado (ver Habilitadores inevitáveis) |
| 5.0 | vertical | 8 | 2 | 6 | 1 | ⚠️ | Dentro da contagem budget, mas complexidade `high` pelo mesmo motivo do backend (3.0): a TechSpec frontend proíbe fatiar V-01 por tipo de rejeição porque recriaria, na UI, o rollout interno que o PRD já proíbe no backend |
| 6.0 | vertical | 1 | 0 | 4 | 1 | ✅ | Dentro da faixa budget; cria só o spec Playwright, reaproveitando tudo de 4.0/5.0 |

### Integridade dos Gates

| Task | Gate | Teste/fixture disponível | Filtro isolado | Repo compilável | Dependência futura | Status |
|------|------|-----------------------------|-------------------|--------------------|------------------------|--------|
| 1.0 | `gate.sh --filter="Booking.Reservations.Domain+Persistence"` | Criado na própria task (`ReservationTests`, `ReservationPersistenceTests`) | Sim | Sim | Não | ✅ |
| 2.0 | `gate.sh --filter="Booking.IntegrationTests.Catalog.CatalogAvailabilityClientTests"` | Criado na própria task (inclui fake server local) | Sim | Sim | Não | ✅ |
| 3.0 | `gate.sh --filter="Booking.IntegrationTests.Reservations.ReservationEndpointTests"` | Criado na própria task; consome `Reservation`/exceções (1.0) e `ICatalogAvailabilityClient` (2.0) já existentes | Sim | Sim | Não | ✅ |
| 4.0 | `gate.sh --filter="reservationApi.test"` | Criado na própria task (`reservationApi.test.ts` com MSW) | Sim | Sim | Não | ✅ |
| 5.0 | `gate.sh --filter="ReservationRequestPage"` | Criado na própria task; consome `reservationApi`/tipos/MSW (4.0) já existentes | Sim | Sim | Não | ✅ |
| 6.0 | `gate.sh --filter="reservation-request"` | Criado na própria task (`e2e/reservation-request.spec.ts`); consome UI (5.0) e endpoint real (3.0) já existentes | Sim | Sim | Não | ✅ |

### Ciclo de Vida de Artefatos Compartilhados

| Artefato | Primeira task produtora | Tasks consumidoras | Dependências consistentes | Status |
|----------|----------------------------|--------------------------|---------------------------------|--------|
| `Reservation`, `ReservationSaga`, exceções de domínio, `IReservationRepository` | 1.0 | 3.0 | Sim | ✅ |
| `ICatalogAvailabilityClient`, `CatalogUnavailableException` | 2.0 | 3.0 | Sim | ✅ |
| `GlobalExceptionHandler` | 3.0 | 3.0 (único consumidor nesta feature) | Sim | ✅ |
| `src/services/api/generated/booking.ts`, `reservationApi.ts`, handlers MSW de Booking | 4.0 | 5.0, 6.0 | Sim | ✅ |
| `ReservationRequestPage` (API pública da feature), rota `/reservations` | 5.0 | 6.0 (alvo do E2E) | Sim | ✅ |

Nenhuma task depende de artefato produzido por task posterior. 4.0 é independente de 1.0–3.0 (nenhum
arquivo compartilhado); 6.0 é a única task que depende de um artefato backend (3.0, o endpoint real)
e de um artefato frontend (5.0, a UI) ao mesmo tempo, porque é o ponto que prova a jornada full-stack.

## Análise de Paralelização

### Lanes de Execução Paralela

| Lane | Tarefas | Descrição |
|------|---------|------------|
| Lane A (backend) | 1.0, 2.0 | Domínio + persistência e cliente Catalog — arquivos disjuntos entre si |
| Lane B (frontend) | 4.0 | Integração tipada (tipos, `reservationApi`, MSW) — nenhum arquivo compartilhado com o backend nem com Lane A |

Lane B pode começar imediatamente (só depende do contrato aprovado e da Fundação V-04 externa),
inclusive antes de 1.0/2.0 estarem prontas, porque 4.0 usa MSW/Prism, não o backend real.

### Caminho Crítico

1.0 e 2.0 podem rodar em paralelo (arquivos disjuntos); 3.0 depende de ambas (consome `Reservation`/
exceções de 1.0 e `ICatalogAvailabilityClient` de 2.0) e é o único ponto que prova o comportamento
backend completo de RF-01. Em paralelo, 4.0 (independente) desbloqueia 5.0; 6.0 é o ponto de
convergência final — depende de 3.0 (backend real) e de 5.0 (UI) e é o único checkpoint que prova a
jornada full-stack exigida pela User Story do "frontend de teste".

### Diagrama de Dependências

```
1.0 (Domínio + Persistência) ─┐
                                ├─→ 3.0 (Endpoint completo: 7 cenários + evento) ─┐
2.0 (Cliente Catalog) ─────────┘                                                  │
                                                                                   ├─→ 6.0 (E2E + gate completo)
4.0 (Integração frontend tipada) ──→ 5.0 (Jornada UI: 7 desfechos) ───────────────┘
```
