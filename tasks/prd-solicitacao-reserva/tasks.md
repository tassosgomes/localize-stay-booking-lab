# Resumo de Tarefas de Implementação — Solicitação de Reserva (Booking F01)

> **PRD de origem:** [`prd.md`](./prd.md)
> **TechSpec de origem:** [`techspec.md`](./techspec.md) — Status: Aprovado — revisão de 2026-09-12
> **API Contract:** [`api-contract.yaml`](./api-contract.yaml) (v1.0.0) — consumido por referência,
> fonte única de schemas/erros; não duplicado nas tasks.
> **ADRs pertinentes:** [ADR-001](../../docs/adr/adr-001-backend-stack-dotnet.md) (stack .NET),
> [ADR-002](../../docs/adr/adr-002-broker-fase0-rabbitmq.md) (convenção de nomes de evento).
> **Status do plano:** Confirmado para implementação
> **Regra de entrega:** cada task de comportamento é uma fatia vertical validável isoladamente

> **Pré-requisito externo (fora do escopo destas tasks):** a fundação técnica da Fase 0
> (`tasks/prd-fundacao-fase0`) — solution `LocalizeStay.Booking.*` (Clean Architecture
> `1-Services`..`4-Infra`), schema `booking`/role `booking_role` em `postgres-main`, vhost
> `/localize-stay` com `Rmq.CloudEvents` configurado, `BookingDbContext` com a migration sentinela
> `__bootstrap_check`, e os projetos de teste `LocalizeStay.Booking.IntegrationTests` (Testcontainers
> Postgres) e `LocalizeStay.Messaging.IntegrationTests` — está sendo construída em outra worktree e é
> assumida como pré-existente no ambiente de execução destas tasks (já confirmado pelo autor na
> TechSpec). Nenhuma task abaixo recria esses artefatos.
> `scripts/ai-flow/gate.sh` também é assumido como já gerado (`tsg-flow-gate-creator`), com suporte a
> `--filter="<selector>"` e `--static`, seguindo o mesmo padrão já usado em `prd-fundacao-fase0`.

## Visão Geral

Implementa `POST /v1/reservations` (Booking F01): um Guest solicita uma Reservation informando
Accommodation, período e número de hóspedes; Booking valida sincronamente contra Catalog
(`GET /accommodations/{id}/availability-check`, contrato provisório), aplica as regras RN-02 a RN-06,
congela preço/moeda/total, cria a `Reservation` em `solicitada` + `ReservationSaga(PaymentPending)` e
publica `booking.reservation_requested`. É a primeira feature de negócio de Booking e o primeiro
exercício de integração síncrona contratada entre domínios do laboratório.

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|-------|---------|------------|
| `dotnet-architecture` | `.claude/skills/dotnet-architecture` | CQRS nativo, exceções de domínio, `IExceptionHandler` global (`examples/cqrs.md`, `examples/error-handling.md`) |
| `dotnet-dependency-config` | `.claude/skills/dotnet-dependency-config` | EF Core (`IEntityTypeConfiguration`, migration), `IHttpClientFactory` + resiliência (timeout, sem retry), `Rmq.CloudEvents` |
| `dotnet-testing` | `.claude/skills/dotnet-testing` | Unitário (xUnit/Moq), integração (`WebApplicationFactory` + Testcontainers Postgres/RabbitMQ, fake HTTP server) |
| `restful-api` | `.claude/skills/restful-api` | RFC 9457 já aplicada em `api-contract.yaml`; referenciada para a extensão `code` do `GlobalExceptionHandler` |

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

## Mapa de Entrega e Feedback

| Slice | Task | Comportamento observável | Gate executável | Seletor focalizado | Bloqueado por |
|-------|------|---------------------------|--------------------|------------------------|----------------|
| V-01 | 1.0 | `Reservation`/`ReservationSaga` aplicam RN-02 a RN-06 em memória (todas as combinações de `AvailabilityFacts`) e persistem/são lidas de volta no schema `booking` real | `gate.sh --filter="Booking.Reservations.Domain+Persistence"` | `ReservationTests` (unit) + `ReservationPersistenceTests` (integration) | Nenhuma (fundação já provisionada externamente) |
| V-02 | 2.0 | `ICatalogAvailabilityClient` retorna `AvailabilityFacts`, `null` (404) ou lança `CatalogUnavailableException` (timeout/5xx/erro), conforme a resposta de um fake server local | `gate.sh --filter="Booking.IntegrationTests.Catalog.CatalogAvailabilityClientTests"` | `CatalogAvailabilityClientTests` | Nenhuma (independente de 1.0) |
| V-03 | 3.0 | `POST /v1/reservations` responde os 7 cenários do AC de RF-01 exatamente como o `api-contract.yaml`, e o sucesso publica `booking.reservation_requested` com `correlationId`/`causationId` | `gate.sh --filter="Booking.IntegrationTests.Reservations.ReservationEndpointTests"` | `ReservationEndpointTests` | 1.0, 2.0 |

### Habilitadores inevitáveis

Nenhum. A TechSpec já registra que nenhum habilitador horizontal novo é necessário nesta feature —
`Directory.Build.props`, o schema/role `booking` e o vhost `/localize-stay` já foram entregues pela
fundação técnica da Fase 0 (tratada como pré-requisito externo, não repetida aqui).

## Tarefas

- [ ] 1.0 Domínio e persistência da Reservation com RN-02 a RN-06 (V-01)
- [ ] 2.0 Cliente de disponibilidade de Catalog: 200/404/falha (V-02)
- [ ] 3.0 Endpoint completo `POST /v1/reservations`: 7 cenários + evento (V-03)

## Rastreabilidade US → Tasks

| User Story | Tasks Relacionadas | Tipo de Cobertura |
|------------|---------------------|------------------------|
| Guest solicita reserva com período/hóspedes/Accommodation, preço garantido | 1.0, 2.0, 3.0 | Direta |
| Guest é informado imediatamente da rejeição (período/capacidade/indisponibilidade) | 1.0 (regra), 3.0 (superfície HTTP) | Direta |
| Autor/arquiteto observa a integração síncrona contratada com Catalog | 2.0, 3.0 | Direta |
| Frontend de teste exibe sucesso ou motivo de rejeição | 3.0 (contrato HTTP consumido pelo frontend) | Suporte |

## Validação de Cobertura

### Requisitos Funcionais

| Requisito | Task(s) | Status |
|-----------|---------|--------|
| RF-01 — sucesso (Given acomodação ativa/capacidade/disponível → 201 + preço congelado + evento) | 1.0, 2.0, 3.0 | ✅ Coberto |
| RF-01 — período inválido (checkOut ≤ checkIn) → 422 `PERIODO_INVALIDO`, sem chamar Catalog | 1.0 (regra), 3.0 (ordem de execução + HTTP) | ✅ Coberto |
| RF-01 — hóspedes ≤ 0 → 422 `QUANTIDADE_HOSPEDES_INVALIDA`, sem chamar Catalog | 1.0 (regra), 3.0 (ordem de execução + HTTP) | ✅ Coberto |
| RF-01 — acomodação inexistente/inativa → 422 `ACOMODACAO_INDISPONIVEL` | 1.0 (regra `Active=false`), 2.0 (404 do Catalog), 3.0 (tradução do 404 + HTTP) | ✅ Coberto |
| RF-01 — capacidade excedida → 422 `CAPACIDADE_EXCEDIDA` | 1.0 (regra), 3.0 (HTTP) | ✅ Coberto |
| RF-01 — indisponível no período → 422 `PERIODO_INDISPONIVEL` | 1.0 (regra), 3.0 (HTTP) | ✅ Coberto |
| RF-01 — Catalog indisponível/erro → 503 `CATALOG_INDISPONIVEL`, nenhuma Reservation criada | 2.0 (client), 3.0 (mapeamento + HTTP) | ✅ Coberto |

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

### Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill Relacionada | Status |
|---|-----------|----------------|----------------------|--------|
| 1 | Setup / Configuração | 2.0 (`CatalogClient:BaseUrl`), 3.0 (`Program.cs`) | `dotnet-dependency-config` | ✅ |
| 2 | Modelos de Dados | 1.0 | `dotnet-architecture` | ✅ |
| 3 | Lógica de Negócio | 1.0 (RN-02 a RN-06 puras) | `dotnet-architecture` | ✅ |
| 4 | Endpoints / Interfaces | 3.0 | `restful-api` | ✅ |
| 5 | Integrações Externas | 2.0 (Catalog HTTP), 3.0 (RabbitMQ) | `dotnet-dependency-config` | ✅ |
| 6 | Validações e Erros | 1.0 (exceções de domínio), 3.0 (`GlobalExceptionHandler` + RFC 9457 `code`) | `dotnet-code-quality` | ✅ |
| 7 | Testes | Subtarefas em 1.0, 2.0, 3.0 | `dotnet-testing` | ✅ |
| 8 | Observabilidade | 3.0 (log estruturado por ponto de decisão com `correlationId`) | — | ✅ (nível básico já herdado do baseline; sem métricas/tracing nesta fase) |
| 9 | Documentação | N/A — `api-contract.yaml` já é a fonte única de contrato; nenhum doc adicional exigido pelo PRD/TechSpec | — | ✅ (justificado) |
| 10 | Segurança | N/A — Fase 0 não implementa autenticação/autorização (`context/architecture-baseline.md`); nenhuma PII real processada | — | ✅ (justificado) |

### Coesão e Faixa de Tamanho

| Task | slice_type | Criar | Modificar | Subtarefas | Fatias | Faixa | Justificativa |
|------|------------|-------|-----------|------------|--------|-------|----------------|
| 1.0 | vertical | 8 | 2 | 6 | 1 | ✅ | Dentro da faixa budget |
| 2.0 | vertical | 4 | 1 | 5 | 1 | ✅ | Dentro da faixa budget |
| 3.0 | vertical | 8 | 2 | 6 | 1 | ⚠️ | Dentro da contagem budget, mas complexidade `high`: os 7 cenários do AC de RF-01 (sucesso + 6 rejeições) só formam um comportamento verificável quando orquestração, endpoint, mapeamento de exceções e publicação existem juntos — dividir por cenário quebraria o gate único e violaria "feature única e indivisível" do PRD (Plano de Rollout Faseado) |

### Integridade dos Gates

| Task | Gate | Teste/fixture disponível | Filtro isolado | Repo compilável | Dependência futura | Status |
|------|------|-----------------------------|-------------------|--------------------|------------------------|--------|
| 1.0 | `gate.sh --filter="Booking.Reservations.Domain+Persistence"` | Criado na própria task (`ReservationTests`, `ReservationPersistenceTests`) | Sim | Sim | Não | ✅ |
| 2.0 | `gate.sh --filter="Booking.IntegrationTests.Catalog.CatalogAvailabilityClientTests"` | Criado na própria task (inclui fake server local) | Sim | Sim | Não | ✅ |
| 3.0 | `gate.sh --filter="Booking.IntegrationTests.Reservations.ReservationEndpointTests"` | Criado na própria task; consome `Reservation`/exceções (1.0) e `ICatalogAvailabilityClient` (2.0) já existentes | Sim | Sim | Não | ✅ |

### Ciclo de Vida de Artefatos Compartilhados

| Artefato | Primeira task produtora | Tasks consumidoras | Dependências consistentes | Status |
|----------|----------------------------|--------------------------|---------------------------------|--------|
| `Reservation`, `ReservationSaga`, exceções de domínio, `IReservationRepository` | 1.0 | 3.0 | Sim | ✅ |
| `ICatalogAvailabilityClient`, `CatalogUnavailableException` | 2.0 | 3.0 | Sim | ✅ |
| `GlobalExceptionHandler` | 3.0 | 3.0 (único consumidor nesta feature) | Sim | ✅ |

Nenhuma task depende de artefato produzido por task posterior.

## Análise de Paralelização

### Lanes de Execução Paralela

| Lane | Tarefas | Descrição |
|------|---------|------------|
| Lane A | 1.0 | Domínio + persistência — nenhum arquivo compartilhado com 2.0 |
| Lane B | 2.0 | Cliente Catalog — nenhum arquivo compartilhado com 1.0 |

### Caminho Crítico

1.0 e 2.0 podem rodar em paralelo (arquivos disjuntos); 3.0 depende de ambas (consome `Reservation`/
exceções de 1.0 e `ICatalogAvailabilityClient` de 2.0) e é o único ponto que prova o comportamento
completo de RF-01 exigido pelo PRD.

### Diagrama de Dependências

```
1.0 (Domínio + Persistência) ─┐
                                ├─→ 3.0 (Endpoint completo: 7 cenários + evento)
2.0 (Cliente Catalog) ─────────┘
```
