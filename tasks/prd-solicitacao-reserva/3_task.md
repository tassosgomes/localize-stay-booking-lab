---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: [1.0, 2.0]
---

<task_context>
<domain>services/booking</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>http_server,external_apis,database</dependencies>
<unblocks>""</unblocks>
<feedback_checkpoint>`dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationEndpointTests"` verde para os 7 cenários do AC de RF-01 via `WebApplicationFactory` (Postgres + fake Catalog + RabbitMQ via Testcontainers); adicionalmente, `npx dredd tasks/prd-solicitacao-reserva/api-contract.yaml http://localhost:5102` sem falha nos paths de Booking (evidência manual, fora do gate automatizado)</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationEndpointTests"</gate_command>
<gate_test_selector>Classe `ReservationEndpointTests` (`LocalizeStay.Booking.IntegrationTests`)</gate_test_selector>
<gate_expected_result>7 testes passam (1 sucesso + 6 rejeições), cada um com o `status`/`code` exatos do `api-contract.yaml`; o cenário de sucesso confirma a publicação de `booking.reservation_requested` com `correlationId`/`causationId` = `reservationId`; 0 falhas</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>`POST /v1/reservations` completo: os 7 cenários do AC de RF-01 (sucesso + 6 rejeições) respondem exatamente como no `api-contract.yaml`, e o sucesso publica `booking.reservation_requested`</vertical_slice>
</task_context>

# Tarefa 3.0: Endpoint completo `POST /v1/reservations`: 7 cenários + evento (V-03)

## Relacionada às User Stories

- "Como Guest, eu quero solicitar uma reserva... para que meu pedido seja registrado com o preço
  vigente garantido" (cobertura direta)
- "Como Guest, eu quero ser informado imediatamente quando meu pedido não pode ser aceito..."
  (cobertura direta)
- "Como autor/arquiteto em estudo, eu quero que a validação de disponibilidade aconteça via chamada
  síncrona contratada a Catalog..." (cobertura direta — fecha o ciclo ponta a ponta)
- "Como frontend de teste, eu quero enviar a solicitação de reserva e exibir o resultado..."
  (suporte — este contrato HTTP é o que o frontend consome)

## Visão Geral

Fecha RF-01 orquestrando, em `RequestReservationCommandHandler`: validação local de
período/hóspedes (task 1.0) → chamada a Catalog (task 2.0) → regra de domínio sobre os fatos (task
1.0) → persistência (task 1.0) → publicação do evento. Expõe isso como `POST /reservations` (Minimal
API) e traduz cada exceção de domínio/integração no `status`/`code` exato do `api-contract.yaml` via
um `GlobalExceptionHandler` — que esta task **cria**, não apenas estende: a fundação técnica da Fase 0
não registrou nenhum `IExceptionHandler` (não havia exceção de negócio antes desta feature), então o
`GlobalExceptionHandler` citado na TechSpec como algo a "estender" nasce aqui, seguindo o padrão de
`dotnet-architecture/examples/error-handling.md` já com a extensão `code`/`traceId` que a TechSpec
exige desde o início.

É a única fatia que prova RF-01 de ponta a ponta porque o PRD marca a feature como "única e
indivisível para efeito de entrega" (Plano de Rollout Faseado) — o caminho de sucesso e as 6 rejeições
só demonstram o objetivo de estudo (integração síncrona contratada com congelamento de preço) quando
implementados juntos.

## Entrega Observável

- **Entrada ou gatilho:** `POST /v1/reservations` com os 7 corpos de request que caracterizam os
  cenários do AC de RF-01 (válido; `checkOut<=checkIn`; `guestsCount<=0`; Catalog 404; Catalog
  `active=false` com facts hipotéticos via fake server; `guestsCount>maxGuests`;
  `availableForPeriod=false`; Catalog indisponível/timeout/500).
- **Resultado esperado:**
  - Sucesso → `201 Created` + `Location` + `ReservationResponse` com `id`, `status="solicitada"`,
    `pricePerNight`/`totalAmount` como string decimal, `currency="BRL"`; evento
    `booking.reservation_requested` publicado no vhost `/localize-stay` com `correlationId` =
    `causationId` = `reservation.id`.
  - `checkOut<=checkIn` → `422` `PERIODO_INVALIDO`, **sem** chamar Catalog.
  - `guestsCount<=0` → `422` `QUANTIDADE_HOSPEDES_INVALIDA`, **sem** chamar Catalog.
  - Catalog 404 ou `active=false` → `422` `ACOMODACAO_INDISPONIVEL`.
  - `guestsCount>maxGuests` → `422` `CAPACIDADE_EXCEDIDA`.
  - `availableForPeriod=false` → `422` `PERIODO_INDISPONIVEL`.
  - Catalog indisponível/erro → `503` `CATALOG_INDISPONIVEL` com `traceId`.
  - Em toda rejeição (422/503), nenhuma `Reservation` é persistida e nenhum evento é publicado.
- **Checkpoint de feedback:** `dotnet test --filter "FullyQualifiedName~ReservationEndpointTests"`
  verde para os 7 cenários; adicionalmente (fora do gate automatizado, evidência manual):
  `npx dredd tasks/prd-solicitacao-reserva/api-contract.yaml http://localhost:5102` sem falha nos
  paths de Booking.
- **Seletor focalizado:** `LocalizeStay.Booking.IntegrationTests.Reservations.ReservationEndpointTests`
- **Fora deste checkpoint:** consulta de uma Reservation já criada (F02); qualquer avanço da saga de
  pagamento (F03/F04); cancelamento/alteração de Reservation (RN-12, fora de escopo permanente).

## Requisitos

- `RequestReservationCommand(AccommodationId, GuestReference, CheckIn, CheckOut, GuestsCount)` +
  `RequestReservationCommandValidator` (FluentValidation) cobre o shape do request (campos
  obrigatórios/tipo) → `400` `VALIDATION_ERROR` quando malformado.
- **Ordem de execução normativa** no handler (não apenas otimização): `Reservation.EnsurePeriodIsValid`
  e `Reservation.EnsureGuestsCountIsValid` rodam **antes** de
  `ICatalogAvailabilityClient.CheckAvailabilityAsync` — a AC de RF-01 exige que período/hóspedes
  inválidos rejeitem sem chamar Catalog.
- 404 de Catalog (`client` retorna `null`) é traduzido pela Application em
  `AcomodacaoIndisponivelException` (mesma exceção do domínio, mesmo `code` 422).
- `CatalogUnavailableException` propagada do client vira `503` `CATALOG_INDISPONIVEL` — nunca `422`.
- No sucesso: `IReservationRepository.AddAsync` seguido de
  `IReservationRequestedPublisher.PublishAsync`; falha de publish não desfaz a criação (best-effort,
  já aceito como risco conhecido na TechSpec — sem outbox nesta fase).
- `GlobalExceptionHandler` (`IExceptionHandler`) mapeia:
  | Exceção | HTTP | `code` |
  |---|---|---|
  | `FluentValidation.ValidationException` | 400 | `VALIDATION_ERROR` |
  | `PeriodoInvalidoException` | 422 | `PERIODO_INVALIDO` |
  | `QuantidadeHospedesInvalidaException` | 422 | `QUANTIDADE_HOSPEDES_INVALIDA` |
  | `AcomodacaoIndisponivelException` | 422 | `ACOMODACAO_INDISPONIVEL` |
  | `CapacidadeExcedidaException` | 422 | `CAPACIDADE_EXCEDIDA` |
  | `PeriodoIndisponivelException` | 422 | `PERIODO_INDISPONIVEL` |
  | `CatalogUnavailableException` | 503 | `CATALOG_INDISPONIVEL` (+ `traceId`) |
  | (não tratada) | 500 | `INTERNAL_ERROR` (+ `traceId`) |
  usando `ProblemDetails.Extensions["code"]` e, para 503/500, `Extensions["traceId"] =
  httpContext.TraceIdentifier`.
- `pricePerNight`/`totalAmount` serializados como string decimal de 2 casas via
  `MoneyStringJsonConverter : JsonConverter<decimal>`, aplicado só no `ReservationResponseDto` (Domain
  continua com `decimal` puro).
- `POST /internal/diagnostics/ping` (endpoint técnico da fundação) isolado sob tag/grupo interno do
  Swagger, fora do documento público consumido pelo frontend — pendência já sinalizada pela TechSpec
  da fundação para "o primeiro PRD de Booking".
- Log estruturado em cada ponto de decisão (request recebida, resultado de Catalog, rejeição aplicada
  com `code`, Reservation criada, evento publicado), todos com `correlationId = Reservation.Id`.

## Arquivos Envolvidos

- **Criar:**
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/{RequestReservationCommand,RequestReservationCommandHandler,RequestReservationCommandValidator}.cs`
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationRequestedPublisher.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/ReservationRequestedRmqPublisher.cs`
    (adapta `IRmqPublisher` do `Rmq.CloudEvents`, já configurado pela fundação, para
    `IReservationRequestedPublisher`; `cloudEventType` sugerido:
    `booking.reservation_requested.v1`, exchange `booking.reservation-events`, routing key
    `reservation.requested` — nome exato é decisão local, ver Prontidão)
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Endpoints/ReservationEndpoints.cs`
    (Minimal API `app.MapPost("/reservations", ...)` → `IDispatcher.SendAsync`)
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Contracts/{CreateReservationRequestDto,ReservationResponseDto,MoneyStringJsonConverter}.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/ErrorHandling/GlobalExceptionHandler.cs`
    (novo — ver Visão Geral sobre a lacuna da fundação)
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/RequestReservationCommandHandlerTests.cs`
    (mocks de `ICatalogAvailabilityClient`, `IReservationRepository`, `IReservationRequestedPublisher`)
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationEndpointTests.cs`
    (`WebApplicationFactory` real: Postgres Testcontainers + fake Catalog de 2.0 + RabbitMQ
    Testcontainers para o cenário de sucesso)
- **Modificar:**
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Program.cs` (registra
    `ReservationEndpoints`, `AddCatalogAvailabilityClient` de 2.0,
    `AddExceptionHandler<GlobalExceptionHandler>()` + `app.UseExceptionHandler()`)
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/DiagnosticsEndpoints.cs` (isola
    `POST /internal/diagnostics/ping` em tag/grupo Swagger interno; arquivo criado pela fundação na
    task 6.0 de `prd-fundacao-fase0`)
- **Referência:**
  - `api-contract.yaml` (paths `/reservations`, `ReservationResponse`, `ProblemDetails`, os 7
    exemplos de resposta) — fonte única de schema/erro, não duplicar
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/**` (task 1.0) —
    `Reservation.Create`, `EnsurePeriodIsValid`, `EnsureGuestsCountIsValid`, as 5 exceções
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/ICatalogAvailabilityClient.cs`,
    `CatalogUnavailableException.cs` (task 2.0)
  - `.claude/skills/dotnet-architecture/examples/error-handling.md` — padrão base do
    `GlobalExceptionHandler`
  - `.claude/skills/dotnet-dependency-config/examples/messaging-rabbitmq.md` — assinatura de
    `IRmqPublisher.PublishAsync(queueName, payload, cloudEventType, headers, cancellationToken)`
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — CQRS nativo (adaptar `examples/cqrs.md` de Controller para Minimal API),
    `IExceptionHandler`
  - `dotnet-dependency-config` — `Rmq.CloudEvents` (publish), registro de `HttpClient`/DI
  - `dotnet-testing` — `WebApplicationFactory` com múltiplos Testcontainers (Postgres + RabbitMQ) +
    fake HTTP server
  - `restful-api` — RFC 9457, extensão `code`

## Subtarefas

- [ ] 3.1 Implementar `RequestReservationCommand`/`Validator`/`Handler` com a ordem normativa
      (período/hóspedes → Catalog → domínio → persistência → publish) e a tradução do 404 de Catalog
      em `AcomodacaoIndisponivelException`
- [ ] 3.2 Implementar `IReservationRequestedPublisher`/`ReservationRequestedRmqPublisher` e
      `ReservationEndpoints` (Minimal API) + DTOs/`MoneyStringJsonConverter`
- [ ] 3.3 Criar `GlobalExceptionHandler` com a tabela completa de mapeamento exceção→HTTP/`code`
      (incluindo `traceId` em 503/500) e registrar em `Program.cs`
- [ ] 3.4 Isolar `POST /internal/diagnostics/ping` em tag/grupo Swagger interno
      (`DiagnosticsEndpoints.cs`)
- [ ] 3.5 Escrever `RequestReservationCommandHandlerTests` (mocks) garantindo que Catalog não é chamado
      em período/hóspedes inválidos e que `AddAsync`/`PublishAsync` não rodam em nenhuma rejeição
- [ ] 3.6 Escrever `ReservationEndpointTests` cobrindo os 7 cenários via `WebApplicationFactory` e
      validar manualmente com `dredd` contra a instância local

## Sequenciamento

- Bloqueado por: 1.0 (`Reservation`, exceções, `IReservationRepository`), 2.0
  (`ICatalogAvailabilityClient`, `CatalogUnavailableException`)
- Desbloqueia: nenhuma task desta feature (fecha RF-01); externamente, F02 (Consulta de Reserva) e F03
  (Solicitação de Pagamento) passam a ter uma `Reservation` real para consumir — fora deste plano
- Paralelizável: Não — é a única fatia e depende de ambas as anteriores

## Rastreabilidade

- Esta tarefa cobre: RF-01 completo (todos os ACs), RN-01 a RN-06 (end-to-end), RN-10 (parcial,
  estado inicial da saga observável via evento), RN-12 (nenhuma operação de cancelamento/alteração é
  exposta, conforme Não-Objetivos).
- Evidência esperada: `ReservationEndpointTests` verde para os 7 cenários;
  `RequestReservationCommandHandlerTests` verde garantindo a ordem de execução e o "nenhum efeito
  colateral em rejeição"; `dredd` sem falha (evidência manual).

## Detalhes de Implementação

Fluxo do handler (normativo, `techspec.md` Diagrama de Componentes):

```
Validator (shape) → 400 se malformado
  ↓
Reservation.EnsurePeriodIsValid      → 422 PERIODO_INVALIDO (sem chamar Catalog)
Reservation.EnsureGuestsCountIsValid → 422 QUANTIDADE_HOSPEDES_INVALIDA (sem chamar Catalog)
  ↓
ICatalogAvailabilityClient.CheckAvailabilityAsync(...)
  ├─ null (404)                         → AcomodacaoIndisponivelException → 422 ACOMODACAO_INDISPONIVEL
  ├─ CatalogUnavailableException        → 503 CATALOG_INDISPONIVEL (+ traceId)
  └─ AvailabilityFacts
       ↓
     Reservation.Create(...)  → 422 ACOMODACAO_INDISPONIVEL / CAPACIDADE_EXCEDIDA / PERIODO_INDISPONIVEL
       ↓ sucesso
     IReservationRepository.AddAsync(...)
       ↓
     IReservationRequestedPublisher.PublishAsync(...)  (best-effort, falha não desfaz a criação)
       ↓
     201 Created + Location + ReservationResponse
```

`GlobalExceptionHandler` — esqueleto (adaptado de `error-handling.md`, com a extensão `code` exigida
pela TechSpec, que o exemplo padrão da skill não tem):

```csharp
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code) = exception switch
        {
            FluentValidation.ValidationException => (400, "VALIDATION_ERROR"),
            PeriodoInvalidoException => (422, "PERIODO_INVALIDO"),
            QuantidadeHospedesInvalidaException => (422, "QUANTIDADE_HOSPEDES_INVALIDA"),
            AcomodacaoIndisponivelException => (422, "ACOMODACAO_INDISPONIVEL"),
            CapacidadeExcedidaException => (422, "CAPACIDADE_EXCEDIDA"),
            PeriodoIndisponivelException => (422, "PERIODO_INDISPONIVEL"),
            CatalogUnavailableException => (503, "CATALOG_INDISPONIVEL"),
            _ => (500, "INTERNAL_ERROR"),
        };
        var problem = new ProblemDetails { Status = status, Instance = httpContext.Request.Path };
        problem.Extensions["code"] = code;
        if (status >= 500) problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
```

Publicação do evento (`Rmq.CloudEvents`, mesma biblioteca já configurada pela fundação para o
diagnóstico):

```csharp
await _publisher.PublishAsync(
    queueName: "booking.reservation-events", // ou nome equivalente definido nesta task
    payload: new ReservationRequestedEvent(reservation.Id, reservation.AccommodationId, ...),
    cloudEventType: "booking.reservation_requested.v1",
    headers: new Dictionary<string, object>
    {
        ["x-correlation-id"] = reservation.Id.ToString(),
        ["x-causation-id"] = reservation.Id.ToString(),
    },
    cancellationToken: cancellationToken);
```

**Convenções da stack:**
- Minimal API adaptando `dotnet-architecture/examples/cqrs.md` (exemplo é de Controller) para
  `app.MapPost`, consistente com o padrão já usado pela fundação nos 3 serviços HTTP.
- `IExceptionHandler` + `ProblemDetails.Extensions` conforme `error-handling.md` e `restful-api`
  (RFC 9457).
- Testes de integração multi-dependência seguem `dotnet-testing/examples/integration-tests.md`
  (`CustomWebApplicationFactory` compondo Postgres + RabbitMQ + o fake Catalog de 2.0).

## Prontidão para Implementação

- **Decisões fechadas:** ordem de execução do handler (período/hóspedes antes de Catalog); 404 de
  Catalog → `ACOMODACAO_INDISPONIVEL`; qualquer outra falha de Catalog → `503`, nunca `422`; publish
  best-effort sem outbox; `pricePerNight`/`totalAmount` como string decimal de 2 casas só na Api
  layer; `CorrelationId`/`causationId` = `Reservation.Id`; tabela completa de mapeamento
  exceção→HTTP/`code` (ver Requisitos).
- **Limites de decisão do implementer:** nome exato da exchange/routing key de
  `booking.reservation_requested` (proposto `booking.reservation-events` / `reservation.requested`,
  convenção `<domínio>.<propósito>` já usada pela fundação — questão em aberto não bloqueante da
  TechSpec); organização interna de `ErrorHandling/` vs. namespace do `GlobalExceptionHandler`.
- **Dependências disponíveis:** `Reservation`/exceções/`IReservationRepository` (task 1.0),
  `ICatalogAvailabilityClient`/`CatalogUnavailableException` (task 2.0), `IRmqPublisher` já
  configurado pela fundação (`Rmq.CloudEvents`), `IDispatcher` já existente no esqueleto CQRS da
  fundação.
- **Artefatos exigidos pelo gate:** `ReservationEndpointTests.cs` e
  `RequestReservationCommandHandlerTests.cs` são criados nesta própria task; Postgres e RabbitMQ via
  Testcontainers são efêmeros; o fake Catalog reutiliza o helper criado na task 2.0.
- **Dependências futuras:** Nenhuma — esta task fecha RF-01 por completo.
- **Ambiguidades bloqueantes:** Nenhuma — a ausência de `GlobalExceptionHandler` na fundação é
  resolvida nesta própria task (criação, não modificação), conforme registrado acima.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationEndpointTests"`
- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.RequestReservationCommandHandlerTests"`
- [ ] Os seletores encontram pelo menos um teste cada e não executam casos sem relação com esta task
- [ ] Build compila sem erros: `dotnet build services/booking/LocalizeStay.Booking.sln`
- [ ] Os 7 cenários do AC de RF-01 respondem com o `status`/`code` exatos do `api-contract.yaml`
- [ ] Período/hóspedes inválidos rejeitam sem nenhuma chamada ao `ICatalogAvailabilityClient` (mock
      verificado em `RequestReservationCommandHandlerTests`)
- [ ] Nenhuma rejeição (422/503) resulta em `AddAsync`/`PublishAsync` chamados
- [ ] Sucesso publica `booking.reservation_requested` com `correlationId`/`causationId` = `reservation.id`
- [ ] `POST /internal/diagnostics/ping` não aparece no grupo público do Swagger consumido pelo
      frontend
- [ ] Checkpoint de feedback executado: `npx dredd tasks/prd-solicitacao-reserva/api-contract.yaml http://localhost:5102` sem falha nos paths de Booking (evidência manual)
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova RF-01 completo (sucesso + 6 rejeições), sem depender de F02/F03
