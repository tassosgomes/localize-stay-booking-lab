# TechSpec: Solicitação de Reserva (Booking F01)

> **Modo de operação:** API-First
> **PRD de origem:** `tasks/prd-solicitacao-reserva/prd.md`
> **API Contract:** `tasks/prd-solicitacao-reserva/api-contract.yaml` (v1.0.0, status "Em Revisão")
> **Data:** 2026-09-12
> **Status:** Aprovado
> **Handoff:** approved — pode alimentar o Task Creator
>
> Aprovado pelo autor nesta revisão, com duas ressalvas explicitamente confirmadas (ver histórico em
> "Questões em Aberto" e "Dependências Técnicas Bloqueantes"): (1) a resolução do conflito de porta de
> dev entre `api-contract.yaml` e a fundação já aprovada usando os valores já aprovados (`5101`
> Catalog / `5102` Booking); (2) a fundação técnica da Fase 0 (`tasks/prd-fundacao-fase0`) é assumida
> como pré-requisito externo — já está sendo construída em outra worktree, não nesta.

---

## Resumo Executivo

Esta TechSpec implementa `POST /v1/reservations` no serviço `LocalizeStay.Booking`, primeira feature
de negócio do domínio Booking. O caso de uso (`RequestReservationCommand`) valida localmente período
e quantidade de hóspedes, chama sincronamente o endpoint de disponibilidade de Catalog
(`GET /v1/accommodations/{id}/availability-check`, contrato provisório) via `HttpClient` resiliente
(timeout explícito, sem retry), aplica as regras de negócio de Booking (RN-02 a RN-06) sobre a
resposta, congela preço/moeda/total na `Reservation` criada em `solicitada`, cria o registro inicial
da `Reservation Saga` (estado `PaymentPending`, sem interação com Payment ainda) e publica
`booking.reservation_requested` via `Rmq.CloudEvents` no vhost `/localize-stay` já provisionado pela
fundação técnica da Fase 0.

Adota CQRS nativo (sem MediatR, conforme `dotnet-architecture`) porque o caso de uso tem múltiplos
passos com efeitos colaterais coordenados (chamada síncrona externa, persistência, publicação de
evento) e caminhos de rejeição que precisam ser rastreáveis individualmente — não é um CRUD simples.
Toda regra de rejeição (RN-02 a RN-06) vive no Domain como lógica pura, testável sem mock de HTTP;
a Application layer só orquestra a chamada a Catalog e traduz falhas de integração em `503`, nunca em
`422`.

**Trade-off primário:** o preço é congelado exatamente uma vez, no momento da validação síncrona, sem
nenhuma proteção contra corrida entre duas solicitações concorrentes sobre a mesma Accommodation —
isso é aceito deliberadamente (RN-09, `domains/booking/domain.md` §8, Não-Objetivos do PRD) e resolvido
apenas na Fase 1 (F06). Em troca, esta feature entrega o padrão de integração síncrona contratada mais
simples possível, sem introduzir idempotência/lock otimista antes de haver um problema real observado.

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|-------|---------|------------------------|
| `dotnet-architecture` | `.claude/skills/dotnet-architecture` | CQRS nativo (`examples/cqrs.md`, adaptado de Controllers para Minimal API), Clean Architecture já usada pela fundação, exceções de domínio (`examples/error-handling.md`) |
| `dotnet-dependency-config` | `.claude/skills/dotnet-dependency-config` | `IHttpClientFactory` + resiliência para o cliente de Catalog, EF Core (nova migration substituindo a tabela sentinela), `Rmq.CloudEvents` para publicar o evento (reaproveitando a configuração de V-03 da fundação) |
| `dotnet-testing` | `.claude/skills/dotnet-testing` | Unitário (regras de domínio, handler com mocks), integração (`WebApplicationFactory` + Testcontainers Postgres + fake HTTP de Catalog) |
| `restful-api` | `.claude/skills/restful-api` | Já aplicada na geração do `api-contract.yaml` (RFC 9457, paths, versionamento); referenciada aqui só para a implementação do `IExceptionHandler` global |

Não foram lidos `dotnet-code-quality`, `dotnet-observability`, `dotnet-performance` e
`design-patterns` porque nenhuma decisão desta TechSpec depende deles além do que o baseline já
define (logging estruturado básico com correlationId/causationId, já herdado da fundação).

---

## Arquitetura do Sistema

### Visão Geral dos Componentes

- **`services/booking` (`LocalizeStay.Booking.Api`)** — adiciona a primeira feature de negócio sobre
  o esqueleto Clean Architecture já aprovado pela TechSpec da fundação (`tasks/prd-fundacao-fase0`):
  endpoint Minimal API `POST /reservations`, dispatcher CQRS já existente é estendido com o primeiro
  command real.
- **`LocalizeStay.Booking.Domain`** — `Reservation` (aggregate root) e `ReservationSaga` (entidade
  filha, 1:1), `AvailabilityFacts` (DTO de entrada vindo de Catalog), exceções de negócio
  (`PeriodoInvalidoException`, `QuantidadeHospedesInvalidaException`, `AcomodacaoIndisponivelException`,
  `CapacidadeExcedidaException`, `PeriodoIndisponivelException`). Nenhuma dependência de HTTP, EF Core
  ou RabbitMQ.
- **`LocalizeStay.Booking.Application`** — `RequestReservationCommand`/`Handler`/`Validator`, portas
  `ICatalogAvailabilityClient`, `IReservationRepository`, `IReservationRequestedPublisher`. Orquestra:
  validação local → chamada a Catalog → regra de domínio → persistência → publicação de evento.
- **`LocalizeStay.Booking.Infra`** — `CatalogAvailabilityHttpClient` (implementa a porta via
  `IHttpClientFactory` + pipeline de resiliência), `ReservationRepository` (EF Core), configuração
  EF Core de `Reservation`/`ReservationSaga` no schema `booking`, `ReservationRequestedRmqPublisher`
  (adapta `IRmqPublisher` já configurado por `MessagingExtensions` na fundação).
- **Catalog (`GET /accommodations/{id}/availability-check`)** — dependência externa síncrona, contrato
  provisório já descrito em `api-contract.yaml`; esta TechSpec só consome, não implementa esse lado.

### Diagrama de Componentes

```text
┌─────────────────────────┐
│ Frontend de teste        │
└────────────┬─────────────┘
             │ POST /v1/reservations (sem auth, Fase 0)
             ▼
┌──────────────────────────────────────────────────────────────────┐
│ LocalizeStay.Booking.Api                                          │
│  Endpoints/ReservationEndpoints.cs → IDispatcher                  │
└────────────┬───────────────────────────────────────────────────────┘
             ▼
┌──────────────────────────────────────────────────────────────────┐
│ Application: RequestReservationCommandHandler                     │
│  1. Validator (FluentValidation)        → 400 se malformado       │
│  2. Reservation.EnsurePeriodIsValid      → 422 PERIODO_INVALIDO    │
│  3. Reservation.EnsureGuestsCountIsValid → 422 QTD_HOSPEDES...     │
│  4. ICatalogAvailabilityClient.CheckAsync(...)                     │
└────────────┬───────────────────────────────────────────┬──────────┘
             │ HTTP GET (timeout, sem retry)              │ falha/timeout
             ▼                                             ▼
┌─────────────────────────────┐                 ┌────────────────────────┐
│ Catalog (dependência)        │                 │ CatalogUnavailableExc.  │
│ /accommodations/{id}/         │                 │ → 503 CATALOG_INDISP.  │
│ availability-check           │                 └────────────────────────┘
└──────────┬────────────────────┘
           │ 200 (facts) ou 404 (not found)
           ▼
┌──────────────────────────────────────────────────────────────────┐
│ Domain: Reservation.Create(command, AvailabilityFacts)             │
│  → 422 ACOMODACAO_INDISPONIVEL / CAPACIDADE_EXCEDIDA /              │
│    PERIODO_INDISPONIVEL, ou Reservation "solicitada" com preço      │
│    congelado + ReservationSaga(PaymentPending)                      │
└────────────┬───────────────────────────────────────────────────────┘
             ▼
┌──────────────────────────────────────────────────────────────────┐
│ Infra: ReservationRepository (EF Core, schema booking)              │
│        + ReservationRequestedRmqPublisher (Rmq.CloudEvents,          │
│          vhost /localize-stay, exchange booking.reservation-events) │
└──────────────────────────────────────────────────────────────────┘
             ▼
        201 Created + Location + ReservationResponse
```

---

## Estratégia de Entrega Incremental

### Mapa de Fatias Verticais

| Slice | Comportamento observável | RF/RN cobertos | Entrada → processamento → saída | Artefatos principais | Evidência / checkpoint | Bloqueado por |
|-------|--------------------------|-----------------|----------------------------------|-----------------------|--------------------------|----------------|
| V-01 | `Reservation` e `ReservationSaga` persistem e são lidos de volta no schema `booking` real (substitui a tabela sentinela `__bootstrap_check` da fundação) | RN-01, RN-05, RN-06 (modelo de dados), domain.md §3 | Teste de integração cria uma `Reservation` via `Reservation.Create(...)` com `AvailabilityFacts` válidas → `ReservationRepository.AddAsync` → nova leitura via `DbContext` confirma linha em `booking.reservations` e `booking.reservation_sagas` com `State=PaymentPending` | `LocalizeStay.Booking.Domain/Reservations/{Reservation,ReservationSaga,AvailabilityFacts,ReservationStatus,SagaState}.cs`; exceções de domínio; `LocalizeStay.Booking.Infra/Persistence/Configurations/{ReservationConfiguration,ReservationSagaConfiguration}.cs`; migration `AddReservationAndSaga` | `dotnet test --filter FullyQualifiedName~Booking.IntegrationTests.Persistence` verde contra Testcontainers Postgres; migration remove `__bootstrap_check` | Fundação técnica da Fase 0 (schema/role `booking`) — em construção em outra worktree, assumida como pré-requisito |
| V-02 | `ICatalogAvailabilityClient` distingue corretamente 200 (fatos), 404 (não encontrado) e falha de infraestrutura (timeout/5xx/erro de deserialização), sem nenhuma regra de negócio aplicada aqui | RN-04 (mecanismo de consulta), AC de RF-01 "Catalog indisponível" | Teste de integração aponta o `HttpClient` para um fake server local (`WebApplicationFactory` minimalista dedicado só a simular Catalog) que responde 200/404/500/timeout → client retorna `AvailabilityFacts`, `null` (404) ou lança `CatalogUnavailableException` | `LocalizeStay.Booking.Application/Reservations/ICatalogAvailabilityClient.cs`; `LocalizeStay.Booking.Infra/Catalog/{CatalogAvailabilityHttpClient,CatalogClientOptions}.cs`; `CatalogClientExtensions.cs` (registro do `HttpClient` + pipeline de resiliência) | `dotnet test --filter FullyQualifiedName~Booking.IntegrationTests.CatalogClient` verde para os 4 cenários (200/404/500/timeout) | Nenhum (independente de V-01) |
| V-03 | `POST /v1/reservations` completo: os 7 cenários do AC de RF-01 (sucesso + 6 rejeições) respondem exatamente como no `api-contract.yaml`, e o sucesso publica `booking.reservation_requested` com `correlationId`/`causationId` | RF-01 completo (todos os ACs), RN-01 a RN-06, RN-10 (parcial), RN-12 | `POST /v1/reservations` → `RequestReservationCommandHandler` (validação local → V-02 → V-01 → publish) → resposta HTTP + evento no vhost `/localize-stay` | `Endpoints/ReservationEndpoints.cs`; `RequestReservationCommand(Handler,Validator)`; `IReservationRequestedPublisher` + `ReservationRequestedRmqPublisher`; `ReservationExceptionMapping` no `GlobalExceptionHandler`; DTOs `CreateReservationRequestDto`/`ReservationResponseDto` + `MoneyStringJsonConverter`; remoção/isolamento de `POST /internal/diagnostics/ping` do contrato público | `dotnet test --filter FullyQualifiedName~Booking.IntegrationTests.Reservations` cobrindo os 7 cenários via `WebApplicationFactory`; `npx dredd tasks/prd-solicitacao-reserva/api-contract.yaml http://localhost:5102` sem falha nos paths de Booking | V-01, V-02 |

### Habilitadores inevitáveis

Nenhum habilitador horizontal novo é necessário nesta TechSpec — `Directory.Build.props`,
`Directory.Packages.props`, o schema `booking` e a role `booking_role` já foram entregues por
EN-01/V-02 da fundação técnica (`tasks/prd-fundacao-fase0`). Essa fundação é tratada como pré-requisito
externo a este plano de fatias, não repetida aqui (ver Dependências Técnicas Bloqueantes).

---

## Design de Implementação

### Interfaces Principais

```csharp
// Application — porta para a dependência síncrona de Catalog
public interface ICatalogAvailabilityClient
{
    // Retorna null quando Catalog responde 404 (accommodation não encontrada).
    // Lança CatalogUnavailableException para timeout, erro de rede, 5xx ou payload inesperado.
    Task<AvailabilityFacts?> CheckAvailabilityAsync(
        Guid accommodationId, DateOnly checkIn, DateOnly checkOut, int guestsCount,
        CancellationToken cancellationToken);
}

// Application — portas de persistência e publicação
public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);
}

public interface IReservationRequestedPublisher
{
    Task PublishAsync(Reservation reservation, CancellationToken cancellationToken);
}
```

```csharp
// Domain — regra pura, sem dependência externa
public sealed class Reservation
{
    public static Reservation Create(
        Guid accommodationId, string guestReference, DateOnly checkIn, DateOnly checkOut,
        int guestsCount, AvailabilityFacts facts)
    {
        // pressupõe que EnsurePeriodIsValid/EnsureGuestsCountIsValid já rodaram antes de chamar Catalog
        if (!facts.Active)
            throw new AcomodacaoIndisponivelException();
        if (guestsCount > facts.MaxGuests)
            throw new CapacidadeExcedidaException();
        if (!facts.AvailableForPeriod)
            throw new PeriodoIndisponivelException();

        var nights = checkOut.DayNumber - checkIn.DayNumber;
        var totalAmount = facts.PricePerNight * nights;
        // ... constrói Reservation "solicitada" + ReservationSaga(PaymentPending)
    }

    public static void EnsurePeriodIsValid(DateOnly checkIn, DateOnly checkOut)
    {
        if (checkOut <= checkIn) throw new PeriodoInvalidoException();
    }

    public static void EnsureGuestsCountIsValid(int guestsCount)
    {
        if (guestsCount <= 0) throw new QuantidadeHospedesInvalidaException();
    }
}
```

### Modelos de Dados

**Mapeamento Entidade do Domain Doc → Modelo Técnico:**

| Entidade (`domains/booking/domain.md` §3) | Modelo Técnico | Local |
|---|---|---|
| Reservation | `Reservation` (EF Core entity, aggregate root) | `Domain/Reservations/Reservation.cs` → tabela `booking.reservations` |
| Reservation Saga | `ReservationSaga` (entidade filha, 1:1 via `ReservationId`) | `Domain/Reservations/ReservationSaga.cs` → tabela `booking.reservation_sagas` |

`ReservationSaga` nesta feature só existe para registrar o estado inicial (`SagaState.PaymentPending`)
exigido pela rastreabilidade do PRD ("Entidades envolvidas": Reservation Saga criada em estado inicial
de pagamento pendente). `SagaState` tem hoje um único valor (`PaymentPending`); F03/F04 estendem o enum
quando introduzirem as próprias transições — não antecipado aqui (evita design para requisito
hipotético).

Tabela `booking.reservations`:

| Coluna | Tipo | Observação |
|---|---|---|
| `id` | uuid, PK | gerado pela aplicação |
| `accommodation_id` | uuid | referência opaca a Catalog, sem FK física (fronteira de domínio) |
| `guest_reference` | varchar(255) | PD-002, sem validação de identidade |
| `check_in`, `check_out` | date | |
| `guests_count` | integer | |
| `status` | varchar/enum mapeado | `solicitada` \| `confirmada` \| `cancelada` (só `solicitada` é gravado por F01) |
| `price_per_night`, `total_amount` | numeric(10,2) | congelados no momento da criação |
| `currency` | varchar(3) | sempre `BRL` |
| `created_at` | timestamptz | UTC |

Tabela `booking.reservation_sagas`:

| Coluna | Tipo | Observação |
|---|---|---|
| `id` | uuid, PK | |
| `reservation_id` | uuid, FK única para `reservations.id` | 1:1 |
| `correlation_id` | uuid | igual a `reservation_id` nesta feature (ver Considerações Técnicas) |
| `state` | varchar/enum mapeado | só `PaymentPending` nesta feature |
| `created_at` | timestamptz | |

**`AvailabilityFacts` (Domain, DTO de entrada vindo da Application após chamar Catalog):**

```csharp
public sealed record AvailabilityFacts(bool Active, int MaxGuests, bool AvailableForPeriod,
    decimal PricePerNight, string Currency);
```

### Endpoints de API

> Os endpoints, schemas, autenticação e formato de erros vêm de
> [`api-contract.yaml`](api-contract.yaml). Esta TechSpec não os duplica.

**Mapeamento de implementação:**

| operationId | Caminho de Implementação |
|-------------|--------------------------|
| `requestReservation` | `Endpoints/ReservationEndpoints.cs` (Minimal API — mesmo padrão de `Program.cs` já usado pela fundação, adaptando o exemplo de Controller de `dotnet-architecture/examples/cqrs.md` para `app.MapPost`) → `IDispatcher.SendAsync(RequestReservationCommand)` → `RequestReservationCommandHandler` → `Reservation` (Domain) → `IReservationRepository` + `IReservationRequestedPublisher` (Infra) |
| `checkAccommodationAvailability` | Não implementado por Booking — é o contrato provisório de Catalog, consumido via `ICatalogAvailabilityClient`. Fora do escopo de artefatos desta TechSpec. |

**Validações adicionais** (além do schema JSON do contrato):

| Validação | Local | HTTP / `code` |
|---|---|---|
| `checkOut` posterior a `checkIn` | Domain — `Reservation.EnsurePeriodIsValid` | 422 `PERIODO_INVALIDO` |
| `guestsCount` > 0 | Domain — `Reservation.EnsureGuestsCountIsValid` | 422 `QUANTIDADE_HOSPEDES_INVALIDA` |
| Accommodation ativa (Property + Accommodation) | Domain — `Reservation.Create` sobre `AvailabilityFacts.Active` | 422 `ACOMODACAO_INDISPONIVEL` |
| `guestsCount` ≤ capacidade | Domain — `Reservation.Create` sobre `AvailabilityFacts.MaxGuests` | 422 `CAPACIDADE_EXCEDIDA` |
| Disponibilidade no período | Domain — `Reservation.Create` sobre `AvailabilityFacts.AvailableForPeriod` | 422 `PERIODO_INDISPONIVEL` |
| Catalog não encontrou a Accommodation (404) | Application — `RequestReservationCommandHandler` traduz `null` do client em rejeição | 422 `ACOMODACAO_INDISPONIVEL` |
| Falha ao consultar Catalog (timeout/5xx/erro) | Application — `CatalogUnavailableException` propagada do client | 503 `CATALOG_INDISPONIVEL` |

**Ordem de execução no handler é normativa** (não apenas uma otimização): a AC de RF-01 exige que
período/hóspedes inválidos rejeitem *sem* chamar Catalog. `EnsurePeriodIsValid` e
`EnsureGuestsCountIsValid` rodam antes de `ICatalogAvailabilityClient.CheckAvailabilityAsync`.

**Mapeamento de Exceções → ErrorResponse do Contrato:**

| Exceção | HTTP | `code` |
|---|---|---|
| `FluentValidation.ValidationException` (shape do request) | 400 | `VALIDATION_ERROR` |
| `PeriodoInvalidoException` | 422 | `PERIODO_INVALIDO` |
| `QuantidadeHospedesInvalidaException` | 422 | `QUANTIDADE_HOSPEDES_INVALIDA` |
| `AcomodacaoIndisponivelException` | 422 | `ACOMODACAO_INDISPONIVEL` |
| `CapacidadeExcedidaException` | 422 | `CAPACIDADE_EXCEDIDA` |
| `PeriodoIndisponivelException` | 422 | `PERIODO_INDISPONIVEL` |
| `CatalogUnavailableException` | 503 | `CATALOG_INDISPONIVEL` |
| (não tratado) | 500 | `INTERNAL_ERROR` |

Estende o `GlobalExceptionHandler` (`error-handling.md`) com o campo de extensão `code` do RFC 9457 —
o exemplo padrão da skill não tem esse campo; é adicionado via `ProblemDetails.Extensions["code"]` e
`Extensions["traceId"] = httpContext.TraceIdentifier` para os 503/500.

### Money e formato de datas

`pricePerNight`/`totalAmount` são `decimal` no Domain/Infra; a serialização como string decimal de 2
casas (`"350.00"`) é responsabilidade só da Api layer via
`MoneyStringJsonConverter : JsonConverter<decimal>` aplicado nas propriedades do
`ReservationResponseDto`. `checkIn`/`checkOut` usam `DateOnly` (serialização nativa do
`System.Text.Json` já produz `yyyy-MM-dd`); `createdAt` usa `DateTime` em UTC (produz `...Z`
nativamente).

### Cliente HTTP de Catalog — resiliência

- `IHttpClientFactory` com typed client (`AddHttpClient<ICatalogAvailabilityClient, CatalogAvailabilityHttpClient>`),
  base address via `CatalogClientOptions.BaseUrl` (`appsettings.json`, chave `CatalogClient:BaseUrl`).
- Pipeline via `Microsoft.Extensions.Http.Resilience` (`AddResilienceHandler`) com **apenas** uma
  estratégia de timeout explícito (ex.: 3s) — **sem retry**. Isso é uma decisão deliberada: o
  `AddStandardResilienceHandler` padrão do pacote inclui retry, mas RF-01 exige que qualquer falha de
  Catalog vire `503` imediatamente, e retry/idempotência de rede são não-objetivos explícitos desta
  feature (reservados a F06 — Resiliência da Saga, `domains/booking/domain.md` §8). Adicionar retry
  aqui antecipa uma decisão que pertence a outro PRD.
- Qualquer `HttpRequestException`, `TaskCanceledException` (timeout), status `>= 500` ou falha ao
  deserializar o corpo de 200 lança `CatalogUnavailableException`. Status `404` retorna `null` (não é
  exceção — é um resultado válido que a Application traduz em rejeição de negócio, conforme nota do
  `api-contract.yaml` sobre quem traduz o 404 de Catalog).

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Fatia | Tipo | Skills Aplicáveis | Descrição |
|---------|-------|------|-------------------|-----------|
| `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/{Reservation,ReservationSaga,AvailabilityFacts,ReservationStatus,SagaState}.cs` | V-01 | Entity/VO | `dotnet-architecture` | Aggregate + entidade filha + regras puras |
| `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Exceptions/*.cs` | V-01 | Domain Exception | `dotnet-architecture` | `PeriodoInvalidoException`, `QuantidadeHospedesInvalidaException`, `AcomodacaoIndisponivelException`, `CapacidadeExcedidaException`, `PeriodoIndisponivelException` |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/{ReservationConfiguration,ReservationSagaConfiguration}.cs` | V-01 | Config (EF) | `dotnet-dependency-config` | `IEntityTypeConfiguration<T>`, schema `booking` |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Migrations/*_AddReservationAndSaga.cs` | V-01 | Migration | `dotnet-dependency-config` | Remove `__bootstrap_check`, cria `reservations`/`reservation_sagas` |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/ReservationRepository.cs` | V-01 | Repository | `dotnet-architecture` | Implementa `IReservationRepository` |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/ICatalogAvailabilityClient.cs` | V-02 | Port | `dotnet-architecture` | Interface consumida pelo handler |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Catalog/{CatalogAvailabilityHttpClient,CatalogClientOptions,CatalogClientExtensions}.cs` | V-02 | Infra/Config | `dotnet-dependency-config` | `HttpClient` + pipeline de resiliência (timeout, sem retry) |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Catalog/CatalogUnavailableException.cs` | V-02 | Exception | `dotnet-architecture` | Sinaliza falha de integração (não é regra de negócio) |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/{RequestReservationCommand,RequestReservationCommandHandler,RequestReservationCommandValidator}.cs` | V-03 | Command/Handler/Validator | `dotnet-architecture` | Orquestração completa de RF-01 |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/{IReservationRepository,IReservationRequestedPublisher}.cs` | V-01/V-03 | Port | `dotnet-architecture` | Portas de persistência e publicação |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/ReservationRequestedRmqPublisher.cs` | V-03 | Infra | `dotnet-dependency-config` | Adapta `IRmqPublisher` (já configurado na fundação) para `IReservationRequestedPublisher` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Endpoints/ReservationEndpoints.cs` | V-03 | Endpoint | `dotnet-architecture` | Minimal API `POST /reservations` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Contracts/{CreateReservationRequestDto,ReservationResponseDto,MoneyStringJsonConverter}.cs` | V-03 | DTO/Converter | `dotnet-architecture` | Contrato HTTP e serialização de `decimal` como string |
| `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/{ReservationTests,RequestReservationCommandHandlerTests}.cs` | V-01/V-02/V-03 | Test | `dotnet-testing` | Regras de domínio puras + orquestração com mocks |
| `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/*.cs` | V-01/V-02/V-03 | Test | `dotnet-testing` | Persistência real (Testcontainers), cliente Catalog (fake server), endpoint completo (`WebApplicationFactory`) |

### Arquivos a Modificar

| Caminho | Fatia | Skills Aplicáveis | Alteração |
|---------|-------|-------------------|-----------|
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Program.cs` | V-03 | `dotnet-program-setup` | Registra `ReservationEndpoints`, `CatalogClientExtensions`, `GlobalExceptionHandler` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/PersistenceExtensions.cs` | V-01 | `dotnet-dependency-config` | Registra `IReservationRepository` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/DiagnosticsController.cs` (ou endpoint equivalente de V-03 da fundação) | V-03 | `dotnet-program-setup` | Isola `POST /internal/diagnostics/ping` fora do agrupamento público de Swagger (tag interna) — item já sinalizado como pendência pela TechSpec da fundação para "o primeiro PRD de Booking" |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/appsettings.json` | V-02 | `dotnet-dependency-config` | Adiciona `CatalogClient:BaseUrl` (dev: `http://localhost:5101/v1`, ver Considerações Técnicas sobre a divergência com o contrato) |

### Arquivos de Referência (não alterar)

| Caminho | Motivo da Consulta |
|---------|-------------------|
| `tasks/prd-fundacao-fase0/techspec.md` | Estrutura de solution já aprovada (`1-Services`..`4-Infra`), convenção de portas (Booking `:5102`), padrão `Extensions/*`, `Rmq.CloudEvents` já configurado |
| `context/architecture-baseline.md` | Ownership de dados, comunicação síncrona/assíncrona, correlationId/causationId |
| `domains/booking/domain.md`, `domains/catalog/domain.md` | RN-01 a RN-12 (Booking) e RN-02/03/04/06 (Catalog, herdadas) |
| `api-contract.yaml` | Fonte única de schemas, paths e formato de erro |

---

## Pontos de Integração

- **Catalog — `GET /accommodations/{id}/availability-check`:** chamada síncrona contratada (OpenAPI,
  provisória). Timeout explícito (3s, ajustável), sem retry nesta feature. 404 é resultado de negócio
  válido (traduzido para `ACOMODACAO_INDISPONIVEL`); qualquer outra falha vira `CATALOG_INDISPONIVEL`
  (503), nunca rejeição de negócio — já mapeado no Domain Doc (RN-04, dependência upstream de alta
  criticidade).
- **RabbitMQ (`ecad-dev-rabbitmq`, vhost `/localize-stay`):** publica `booking.reservation_requested`
  reaproveitando a configuração de `Rmq.CloudEvents` já provisionada em V-03 da fundação. Sem
  consumidor obrigatório nesta fase (PRD explícito); publicação é best-effort após persistir a
  `Reservation` — falha de publish não desfaz a criação (ver Riscos Conhecidos).

---

## Análise de Impacto

| Componente Afetado | Tipo de Impacto | Descrição & Risco | Ação Requerida |
|--------------------|-----------------|-------------------|-----------------|
| Schema `booking` (`postgres-main`) | Modificado | Nova migration substitui `__bootstrap_check` por `reservations`/`reservation_sagas`; risco baixo, schema isolado por `booking_role` | Rodar migration via pipeline de deploy do serviço, não no boot (já convenção da fundação) |
| Vhost `/localize-stay` (RabbitMQ) | Modificado | Nova exchange/routing key real (`booking.reservation-events` ou equivalente) ao lado da `diagnostics.topic` de diagnóstico; risco baixo | Nenhuma migração de infraestrutura manual — `Rmq.CloudEvents` declara topologia idempotente no boot |
| `POST /internal/diagnostics/ping` | Modificado (isolado) | Já sinalizado como pendência pela fundação; risco de vazar endpoint técnico no Swagger público se não isolado | Mover para tag/grupo interno do Swagger, fora do documento consumido pelo frontend |
| Catalog F04 (Consulta de Disponibilidade) | Nenhum nesta TechSpec | Contrato consumido é provisório; quando Catalog ratificar seu próprio PRD, `ICatalogAvailabilityClient`/`CatalogAvailabilityHttpClient` podem precisar de ajuste de schema | Nenhuma ação agora — já registrado como pendência no `api-contract.yaml` |
| Booking F02/F03 (futuras) | Nenhum nesta TechSpec | F02 lerá `Reservation`; F03 lerá/atualizará `ReservationSaga` — ambos passam a ter dado real para trabalhar | Nenhuma ação agora |

---

## Abordagem de Testes

### Testes Unitários

- `Reservation.EnsurePeriodIsValid` / `EnsureGuestsCountIsValid`: casos-limite (`checkOut == checkIn`,
  `guestsCount == 0`, negativo).
- `Reservation.Create`: `AvailabilityFacts` combinando `Active=false`, `guestsCount > MaxGuests`,
  `AvailableForPeriod=false`, e o caminho de sucesso (preço/total corretos para 1, 2 e N noites).
- `RequestReservationCommandHandler` (mocks de `ICatalogAvailabilityClient`, `IReservationRepository`,
  `IReservationRequestedPublisher`): garante que Catalog **não é chamado** quando período/hóspedes são
  inválidos; garante que `AddAsync`/`PublishAsync` não rodam quando qualquer rejeição ocorre.
- Cobertura de RN-01 a RN-06 mapeada 1:1 com casos de teste, conforme exigido pelo modo Pipeline
  herdado do Domain Doc.

### Testes de Integração

- **Persistência (V-01):** `WebApplicationFactory` + Testcontainers PostgreSQL — cria e lê de volta
  `Reservation`/`ReservationSaga` reais no schema `booking`.
- **Cliente Catalog (V-02):** um servidor HTTP fake mínimo (outro `WebApplicationFactory` no próprio
  projeto de teste, sem NuGet adicional) simula 200/404/500/timeout; confirma que `CatalogUnavailableException`
  só é lançada nos casos de falha de infraestrutura.
- **Endpoint completo (V-03):** os 7 cenários do AC de RF-01 via `WebApplicationFactory` real (Postgres
  Testcontainers + fake Catalog + broker real via Testcontainers RabbitMQ para o cenário de sucesso,
  confirmando publicação com `correlationId`/`causationId`).

### Testes de Contrato

- `npx dredd tasks/prd-solicitacao-reserva/api-contract.yaml http://localhost:5102` contra a instância
  local do serviço, cobrindo os exemplos de `POST /reservations` do contrato (sugestão já registrada em
  `api-contract.md`).

---

## Sequenciamento de Desenvolvimento

### Build Order

1. V-01 (persistência) — depende apenas da fundação já aprovada (schema/role `booking` existentes).
2. V-02 (cliente Catalog) — independente de V-01, pode rodar em paralelo.
3. V-03 (endpoint completo) — depende de V-01 e V-02.

### Dependências Técnicas Bloqueantes

- **A fundação técnica da Fase 0 (`tasks/prd-fundacao-fase0`) precisa estar executada para o serviço
  Booking antes desta feature**, especificamente: solution `LocalizeStay.Booking.*` existente
  (V-02 da fundação), schema `booking` + `booking_role` provisionados em `postgres-main`
  (`db/bootstrap`), e o vhost `/localize-stay` + `Rmq.CloudEvents` configurados (V-03 da fundação).
  **Confirmado pelo autor:** essa fundação já está sendo construída em outra worktree — esta TechSpec
  assume os artefatos acima como pré-requisito externo, sem recriá-los.
- Catalog precisa expor `GET /accommodations/{id}/availability-check` (mesmo que como stub/mock) para
  os testes de integração de V-03 rodarem contra algo real; até lá, os testes de V-03 usam o fake
  server local descrito em V-02, não a instância real de Catalog.

---

## Monitoramento e Observabilidade

- Logging estruturado (já padrão da fundação) em cada ponto de decisão: requisição recebida, resultado
  da chamada a Catalog (sucesso/404/falha), rejeição aplicada (com `code`), Reservation criada, evento
  publicado — todos com `correlationId` (= `Reservation.Id`).
- Nenhuma métrica/tracing formal nesta fase (Fase 1 trata isso), conforme baseline.

---

## Considerações Técnicas

### Decisões Principais

- **Decisão:** CQRS nativo para `RequestReservation`, adaptando o exemplo de Controller de
  `dotnet-architecture/examples/cqrs.md` para um endpoint Minimal API (`app.MapPost`), consistente com
  o padrão Minimal API já adotado pela fundação técnica.
  **Racional:** múltiplos passos com efeitos colaterais coordenados (chamada síncrona externa,
  persistência, publicação) e necessidade de rastreabilidade por tipo de operação — critério explícito
  do `SKILL.md` para preferir CQRS sobre Service Pattern simples.
  **Trade-offs:** mais peças móveis (command, handler, validator) que um serviço simples para uma
  única operação.
  **Alternativas rejeitadas:** Service Pattern simples — rejeitado porque o caso de uso já tem
  complexidade suficiente (6 caminhos de rejeição + integração síncrona) para justificar a indireção.

- **Decisão:** Todas as regras RN-02 a RN-06 vivem no Domain como funções puras sobre
  `AvailabilityFacts` (um DTO simples), não na Application.
  **Racional:** mantém as regras de negócio 100% testáveis sem mock de HTTP, e reforça a regra não
  negociável do baseline de que o Domain não depende de infraestrutura.
  **Trade-offs:** nenhum significativo — `AvailabilityFacts` é um DTO trivial.

- **Decisão:** Pipeline de resiliência do cliente Catalog usa **apenas timeout**, sem retry, via
  `Microsoft.Extensions.Http.Resilience` (`AddResilienceHandler` customizado, não o
  `AddStandardResilienceHandler` padrão).
  **Racional:** retry/idempotência de rede são não-objetivos explícitos desta feature (F06, Fase 1);
  adicionar retry aqui misturaria uma decisão de resiliência que pertence a outro PRD.
  **Trade-offs:** uma falha transitória de rede vira `503` imediatamente, exigindo que o solicitante
  tente de novo manualmente — aceitável e coerente com o AC de RF-01 ("essa reserva pode ser tentada
  novamente pelo solicitante").
  **Alternativas rejeitadas:** usar o `AddStandardResilienceHandler` padrão (inclui retry) — rejeitado
  por antecipar F06.

- **Decisão:** `ReservationSaga.CorrelationId` = `Reservation.Id` nesta feature; `causationId` do
  evento `booking.reservation_requested` também é o mesmo valor (evento autocausado, por ser o
  primeiro da saga).
  **Racional:** o baseline define `correlationId` como identificador da reserva/saga; como F01 não
  reage a nenhum evento anterior, não há um `causationId` externo real disponível.
  **Trade-offs:** nenhum — F03/F04 já preveem propagar `causationId` a partir de eventos reais quando
  existirem.

### Riscos Conhecidos

- **Corrida entre solicitações concorrentes sobre a mesma Accommodation:** já aceito e documentado em
  `domains/booking/domain.md` §8 e nos Não-Objetivos do PRD; nenhuma mitigação nesta TechSpec (fica
  para F06).
- **Publicação do evento não é transacional com a persistência:** se `IReservationRequestedPublisher`
  falhar após `IReservationRepository.AddAsync` ter sido concluído, a `Reservation` fica criada sem o
  evento publicado. Aceito nesta fase porque o PRD marca `booking.reservation_requested` como "sem
  consumidor obrigatório na Fase 0" — não há efeito colateral observável de uma publicação perdida
  ainda. Um padrão de outbox transacional não é introduzido agora (antecipar solução sem consumidor
  real contraria a disciplina de introdução de tecnologia do baseline); revisitar quando um consumidor
  real depender desse evento.

### Requisitos Especiais

Não aplicável — sem requisito de performance, segurança adicional (Fase 0 não implementa
autenticação) ou conformidade regulatória.

### Conformidade com Skills

- Segue `dotnet-architecture` (Clean Architecture, CQRS nativo, exceções específicas de domínio,
  `ProblemDetails` para erros).
- Segue `dotnet-dependency-config` (EF Core com `IEntityTypeConfiguration`, `IHttpClientFactory` +
  Polly/resiliência com timeout explícito, `Rmq.CloudEvents` reaproveitado).
- Segue `dotnet-testing` (unitário para regra de domínio, integração com Testcontainers para
  persistência e mensageria).

**Desvios identificados:**

| Desvio | Skill | Justificativa |
|--------|-------|----------------|
| `AddResilienceHandler` só com timeout, sem retry (o padrão sugerido pela biblioteca moderna de resiliência inclui retry) | `dotnet-dependency-config` | Decisão desta feature, não do baseline: retry pertence a F06 (ver Decisões Principais) |
| Fake HTTP server em teste em vez de uma ferramenta dedicada de stub (ex. WireMock.Net) | `dotnet-testing` | Evita introduzir dependência nova só para testes quando um `WebApplicationFactory` simples já resolve (disciplina de introdução de tecnologia do baseline) |

---

## Questões em Aberto

- [x] **Conflito com o API Contract** (resolvido pelo autor nesta revisão): `api-contract.yaml`
  declara `servers.url` de desenvolvimento para Booking como `http://localhost:5000/v1` e para a
  dependência de Catalog como `http://localhost:5010/v1`. A TechSpec/tasks já aprovadas da fundação
  (`tasks/prd-fundacao-fase0`) fixam Booking em `:5102` e Catalog em `:5101`. Resolução confirmada: usar
  os valores já aprovados (`5101`/`5102`) na configuração real (`CatalogClient:BaseUrl`, launch profile
  de Booking); os `servers.url` do `api-contract.yaml` deveriam ser corrigidos para consistência num
  próximo ajuste do contrato — não bloqueou esta TechSpec porque não afeta schema, comportamento ou
  dado, só a URL ilustrativa de desenvolvimento no documento OpenAPI.
- [ ] Valor exato do timeout do cliente Catalog (proposto: 3s) pode ser ajustado durante a
  implementação sem impacto arquitetural.
- [ ] Nome exato da exchange/routing key de `booking.reservation_requested` no vhost `/localize-stay`
  (proposto: `booking.reservation-events` / routing key `reservation.requested`) fica para a
  implementação, seguindo a convenção `<domínio>.<propósito>` já estabelecida pela fundação.

---

## Architecture Decision Records

Nenhuma ADR nova é necessária: esta TechSpec aplica decisões já aceitas (estilo arquitetural, stack,
broker, comunicação síncrona vs. dataset publicado) sem introduzir escolha arquitetural nova. A opção
por CQRS nativo e a política de resiliência sem retry são decisões de implementação de escopo local
(uma feature, um serviço), não decisões estruturais que sobrevivem à remoção desta feature.

- [ADR-001: Stack de backend — .NET / C# (ASP.NET Core)](../../docs/adr/adr-001-backend-stack-dotnet.md)
- [ADR-002: Broker de eventos da Fase 0 — RabbitMQ](../../docs/adr/adr-002-broker-fase0-rabbitmq.md) — define a convenção de nomes de evento reaproveitada por `booking.reservation_requested`.

---

## Próximos Passos

1. **Implementação:** usar `tsg-flow-task-creator` referenciando esta TechSpec. Confirmar antes que a
   fundação técnica da Fase 0 (schema/role `booking`, solution skeleton, vhost RabbitMQ), em construção
   em outra worktree, já está disponível no ambiente onde as tasks desta feature serão executadas —
   esta TechSpec assume esses artefatos como pré-requisito e não os recria.
2. **Frontend:** usar `tsg-flow-frontend-techspec-creator` referenciando `api-contract.yaml` e o PRD.
3. **Ajuste de contrato (não bloqueante):** corrigir os `servers.url` de desenvolvimento em
   `api-contract.yaml` (Booking `5000`→`5102`, Catalog `5010`→`5101`) num próximo ajuste do contrato.
