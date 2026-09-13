---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: [1.0]
---

<task_context>
<domain>services/booking</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database,http_server</dependencies>
<unblocks>"5.0"</unblocks>
<feedback_checkpoint>`dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.GetReservationByIdQueryHandlerTests"` verde (formato inválido não chama o repositório; não encontrado lança `ReservationNotFoundException`; encontrado retorna a mesma instância); `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationQueryEndpointTests"` verde (os 5 cenários do AC de RF-01 via `WebApplicationFactory` real)</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.GetReservationByIdQueryHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationQueryEndpointTests"</gate_command>
<gate_test_selector>Classes `GetReservationByIdQueryHandlerTests` (`LocalizeStay.Booking.UnitTests`) e `ReservationQueryEndpointTests` (`LocalizeStay.Booking.IntegrationTests`)</gate_test_selector>
<gate_expected_result>Todos os testes de ambas as classes passam (verde); 0 falhas; `GET /v1/reservations/{id}` responde 200 com os 3 pares estado/`sagaStatus` (solicitada/pendente, confirmada/autorizado, cancelada/rejeitado com `cancellationReason`), 404 `RESERVATION_NOT_FOUND` para id inexistente e 400 `VALIDATION_ERROR` para id malformado — exatamente como `api-contract.yaml`</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>`GET /v1/reservations/{reservationId}` retorna 200 com todos os campos (incluindo os 3 estados de Reservation e as 3 situações de saga) e distingue corretamente 400 (formato inválido) de 404 (não encontrada), reaproveitando o `Dispatcher` estendido com o lado de query</vertical_slice>
</task_context>

# Tarefa 2.0: Endpoint completo `GET /v1/reservations/{reservationId}` — 5 cenários do AC de RF-01 (V-01 backend)

## Relacionada às User Stories

- "Como Guest, eu quero consultar minha Reservation pelo identificador recebido ao solicitá-la, para
  saber se ela foi confirmada, cancelada, ou ainda aguarda o resultado do pagamento." (cobertura
  direta — HTTP completo)
- "Como autor/arquiteto em estudo, eu quero consultar a situação da saga (pendente, autorizado,
  rejeitado) e o identificador de correlação de uma Reservation..." (cobertura direta)
- "Como Guest, eu quero receber uma resposta clara quando o identificador informado não corresponde a
  nenhuma Reservation..." (cobertura direta — 404 distinto de 400)

## Visão Geral

Implementa `GET /v1/reservations/{reservationId}` de ponta a ponta: valida o formato do
identificador, busca a `Reservation` com sua `ReservationSaga` associada via uma única leitura
consistente, e projeta o estado do ciclo de vida, a situação observável da saga (`sagaStatus`) e o
motivo de cancelamento quando aplicável. Estende o `Dispatcher` nativo (CQRS, criado em F01 só com o
lado de comando) com o lado de query (`IQuery<TResponse>`/`IQueryHandler<,>`), exatamente como o
comentário já deixado em `Dispatcher.cs` antecipava ("Queries entram quando a primeira consulta
existir (F02)"). Reaproveita a mesma resposta de erro RFC 9457 e o mesmo `IExceptionHandler` global de
F01, apenas com um novo caso de exceção (`ReservationNotFoundException` → 404). É a única fatia
vertical do backend desta feature — os 5 cenários do AC de RF-01 (3 combinações de sucesso + 404 +
400) só formam um comportamento verificável quando a extensão do `Dispatcher`, o repositório, o
endpoint e o tratamento de erro existem juntos.

## Entrega Observável

- **Entrada ou gatilho:** `GET /v1/reservations/{reservationId}` via `WebApplicationFactory` real.
- **Resultado esperado:** 200 com o corpo `ReservationDetail` completo para os 3 pares
  estado/`sagaStatus` (com `cancellationReason` presente só quando `cancelada`); 404
  `RESERVATION_NOT_FOUND` para um id bem formado sem Reservation correspondente; 400
  `VALIDATION_ERROR` para um id malformado.
- **Checkpoint de feedback:** `dotnet test --filter "FullyQualifiedName~GetReservationByIdQueryHandlerTests"`
  (unitário) e `dotnet test --filter "FullyQualifiedName~ReservationQueryEndpointTests"` (integração,
  Testcontainers Postgres) — ambos verdes.
- **Seletor focalizado:** `LocalizeStay.Booking.UnitTests.Reservations.GetReservationByIdQueryHandlerTests`,
  `LocalizeStay.Booking.IntegrationTests.Reservations.ReservationQueryEndpointTests`
- **Fora deste checkpoint:** nenhuma escrita real que produza `confirmada`/`cancelada` em produção
  (os testes de integração simulam esses estados via `UPDATE` SQL direto, dívida documentada na
  TechSpec até F04 existir); nenhuma UI (task 4.0).

## Requisitos

- `GetReservationByIdQuery(string ReservationId) : IQuery<Reservation>` — recebe o identificador como
  `string` bruta (não `Guid`), para que a validação de formato seja responsabilidade do
  validator/handler, não do model binding de rota.
- `GetReservationByIdQueryValidator` (FluentValidation): rejeita `reservationId` vazio ou que não seja
  um `Guid` válido → 400 `VALIDATION_ERROR`.
- `GetReservationByIdQueryHandler`: valida (via `ValidateAndThrowAsync`), faz `Guid.Parse`, chama
  `IReservationRepository.GetByIdAsync`; se `null`, lança `ReservationNotFoundException`; caso
  contrário retorna a `Reservation` (com `.Saga` carregado) sem nenhuma transformação — a tradução
  para o schema do contrato acontece na camada de API (mesmo padrão de
  `RequestReservationCommandHandler`/`ReservationResponseDto` em F01).
- `IReservationRepository.GetByIdAsync(Guid id, CancellationToken)`: nova leitura com
  `Include(r => r.Saga)` e `AsNoTracking()` — não reaproveita nenhuma lógica de escrita.
- `Dispatcher`/`IDispatcher` ganham `IQuery<TResponse>`, `IQueryHandler<TQuery, TResponse>` e um
  overload de `SendAsync` para queries, no mesmo padrão do lado de comando existente (reflection sobre
  `IServiceProvider`, log de `command.type`/`query.type`).
- `ReservationDetailResponseDto.From(reservation)`: projeta `Reservation`+`Saga` no schema
  `ReservationDetail` do contrato, incluindo a tradução de `SagaState` → `sagaStatus`
  (`PaymentPending`→`"pendente"`, `Authorized`→`"autorizado"`, `Rejected`→`"rejeitado"`) e
  `cancellationReason` (nulo quando ausente).
- `ReservationEndpoints` ganha `MapGet("/v1/reservations/{reservationId}", ...)`, delegando ao
  `IDispatcher` já existente; `reservationId` chega como `string` na rota (sem constraint `:guid`).
- `GlobalExceptionHandler` ganha um novo `case ReservationNotFoundException` → 404
  `RESERVATION_NOT_FOUND`, sem alterar nenhum case existente de F01.
- `ApplicationExtensions` registra `IQueryHandler<GetReservationByIdQuery, Reservation>` e
  `IValidator<GetReservationByIdQuery>`.

## Arquivos Envolvidos

- **Criar:**
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Exceptions/ReservationNotFoundException.cs`
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/GetReservationByIdQuery.cs`
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/GetReservationByIdQueryValidator.cs`
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/GetReservationByIdQueryHandler.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Contracts/ReservationDetailResponseDto.cs`
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/GetReservationByIdQueryHandlerTests.cs`
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationQueryEndpointTests.cs`
- **Modificar:**
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationRepository.cs`
    (`+ GetByIdAsync(Guid, CancellationToken)`)
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/ReservationRepository.cs`
    (implementa `GetByIdAsync` com `Include(Saga)` + `AsNoTracking()`)
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Cqrs/Dispatcher.cs`
    (`+ IQuery<TResponse>`, `+ IQueryHandler<,>`, `+ SendAsync(IQuery<TResponse>, ...)`)
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Endpoints/ReservationEndpoints.cs`
    (`+ MapGet("/v1/reservations/{reservationId}", ...)`)
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/ErrorHandling/GlobalExceptionHandler.cs`
    (`+` case `ReservationNotFoundException` → 404 `RESERVATION_NOT_FOUND`)
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/ApplicationExtensions.cs`
    (registra o novo `IQueryHandler`/`IValidator`)
- **Referência:**
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Contracts/MoneyStringJsonConverter.cs` —
    reaproveitado sem alteração no novo DTO
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Contracts/ReservationResponseDto.cs` —
    padrão de DTO a seguir (`From(reservation)` estático)
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/CustomWebApplicationFactory.cs`,
    `BookingIntegrationTestCollection.cs` — reaproveitados sem alteração
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationEndpointTests.cs`
    — padrão de teste de integração HTTP a seguir
  - `api-contract.yaml` (`ReservationDetail`, `ReservationDetailResponse`, `ProblemDetails`) — schema
    exato da resposta
  - `techspec.md` (Design de Implementação, Abordagem de Testes) — assinaturas e os 5 cenários
    normativos
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — extensão de CQRS nativo (`examples/cqrs.md`), exceção de domínio
    específica (`examples/error-handling.md`)
  - `dotnet-testing` — xUnit/Moq para unitário; `WebApplicationFactory` + Testcontainers Postgres para
    integração

## Subtarefas

- [ ] 2.1 Estender `Dispatcher`/`IDispatcher` com `IQuery<TResponse>`, `IQueryHandler<TQuery,TResponse>`
      e o overload de `SendAsync` para queries (lado de comando existente inalterado)
- [ ] 2.2 Implementar `GetReservationByIdQuery` + `GetReservationByIdQueryValidator` +
      `GetReservationByIdQueryHandler` + `ReservationNotFoundException`
- [ ] 2.3 Estender `IReservationRepository`/`ReservationRepository` com `GetByIdAsync`
      (`Include(Saga)` + `AsNoTracking()`)
- [ ] 2.4 Implementar `ReservationDetailResponseDto` (projeção + tradução de `sagaStatus`); ligar o
      endpoint (`MapGet`), o novo case do `GlobalExceptionHandler` e o registro DI em
      `ApplicationExtensions`
- [ ] 2.5 Escrever `GetReservationByIdQueryHandlerTests`: formato inválido (repositório nunca chamado),
      não encontrado, encontrado (retorna a mesma instância)
- [ ] 2.6 Escrever `ReservationQueryEndpointTests`: os 5 cenários do AC de RF-01 via
      `WebApplicationFactory` real (seed via `AddAsync` para solicitada/pendente; `UPDATE` SQL direto
      para confirmada/autorizado e cancelada/rejeitado+motivo; 404; 400)

## Sequenciamento

- Bloqueado por: 1.0 (consome `SagaState.Authorized`/`Rejected` e `ReservationSaga.CancellationReason`)
- Desbloqueia: 5.0 (E2E consome o endpoint real)
- Paralelizável: Não com 1.0 (dependência direta); pode avançar em paralelo às tasks 3.0/4.0 do
  frontend (nenhum arquivo compartilhado)

## Rastreabilidade

- Esta tarefa cobre: RF-01 (todas as ACs), RN-05, RN-06, RN-07, RN-08, RN-10, RN-11; PD-002 (sem
  verificação de identidade).
- Evidência esperada: `GetReservationByIdQueryHandlerTests` verde cobrindo os 3 casos do handler;
  `ReservationQueryEndpointTests` verde cobrindo os 5 cenários do AC de RF-01 via HTTP real.

## Detalhes de Implementação

Assinaturas normativas (`techspec.md`, Design de Implementação):

```csharp
// Application/Cqrs/Dispatcher.cs — extensão do que já existe (F01)
public interface IQuery<TResponse>;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken); // já existe
    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);      // novo
}
```

```csharp
// Application/Reservations/GetReservationByIdQuery.cs (novo)
public sealed record GetReservationByIdQuery(string ReservationId) : IQuery<Reservation>;

// string bruta (não Guid): a validação de formato é responsabilidade desta
// query/validator, não do model binding — permite distinguir 400 de 404 sem
// depender de constraint de rota ":guid" (que geraria 404 do próprio
// roteamento em vez do 400 controlado pelo contrato).
```

```csharp
// Application/Reservations/IReservationRepository.cs (estendido)
public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken); // já existe
    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken); // novo
}
```

Tradução `SagaState` → `sagaStatus` (feita em `ReservationDetailResponseDto`, não no domínio):

| `SagaState` | `sagaStatus` (contrato) |
|---|---|
| `PaymentPending` | `"pendente"` |
| `Authorized` | `"autorizado"` |
| `Rejected` | `"rejeitado"` |

Mapeamento de exceção → `ProblemDetails` (novo case no `GlobalExceptionHandler`, mesmo `switch`
expression já usado por F01):

```csharp
ReservationNotFoundException => (
    StatusCodes.Status404NotFound,
    "RESERVATION_NOT_FOUND",
    "reservation-not-found",
    "Reservation não encontrada",
    "Nenhuma Reservation existe com o identificador informado."),
```

`reservationId` continua como `string` no path (sem constraint `:guid` da rota) — mesma decisão já
validada em F01 para outros campos: uma constraint de rota faria o próprio ASP.NET Core devolver 404
(rota não encontrada) para um valor malformado, sobrepondo-se à distinção 400×404 exigida pelo AC de
RF-01.

Money e formato de datas: reaproveita `MoneyStringJsonConverter` já existente (F01) para
`pricePerNight`/`totalAmount`; `checkIn`/`checkOut` como `DateOnly`, `createdAt` como `DateTime` —
serialização default do ASP.NET Core já produz `date`/`date-time` ISO 8601.

Os 5 cenários do teste de integração (`techspec.md` §Abordagem de Testes):

1. `solicitada` + `pendente` — seed via `IReservationRepository.AddAsync` (caminho normal de F01).
2. `confirmada` + `autorizado` — seed via `AddAsync` e, em seguida, `UPDATE` SQL direto
   (`dbContext.Database.ExecuteSqlInterpolated`) em `reservations.status` e `reservation_sagas.state`
   — **não** por um método de domínio (nenhum existe ainda; é responsabilidade de uma feature futura
   de conclusão da saga). Documentado como decisão de teste, não de produção.
3. `cancelada` + `rejeitado` + `cancellationReason` preenchido — mesma técnica de seed, incluindo a
   coluna nova.
4. Identificador bem formado sem Reservation correspondente → 404 `RESERVATION_NOT_FOUND`.
5. Identificador malformado (ex.: `"nao-e-um-uuid"`) → 400 `VALIDATION_ERROR`.

**Convenções da stack:**
- Exceção de domínio herda `DomainException` (padrão de `dotnet-architecture/examples/error-handling.md`).
- Testes seguem Arrange-Act-Assert; unitário usa `Moq` (mesmo padrão de
  `RequestReservationCommandHandlerTests`); integração reaproveita
  `CustomWebApplicationFactory`/`BookingIntegrationTestCollection` já existentes.

## Prontidão para Implementação

- **Decisões fechadas:** `GetReservationByIdQuery` recebe `string` bruta (não `Guid`); handler
  retorna `Reservation` de domínio diretamente (tradução para `ReservationDetailResponseDto` acontece
  na API); `reservationId` sem constraint `:guid` na rota; seed de `confirmada`/`cancelada` nos testes
  via `UPDATE` SQL direto (dívida documentada, não produção).
- **Limites de decisão do implementer:** organização interna do `switch` expression no
  `GlobalExceptionHandler` (onde inserir o novo case); nomes exatos de variáveis locais.
- **Dependências disponíveis:** `SagaState.Authorized`/`Rejected`, `ReservationSaga.CancellationReason`
  (task 1.0, já prontos); `Reservation`, `IReservationRepository.AddAsync`, `Dispatcher` (lado de
  comando), `GlobalExceptionHandler`, `CustomWebApplicationFactory` (F01, já em `main`).
- **Artefatos exigidos pelo gate:** `GetReservationByIdQueryHandlerTests.cs` e
  `ReservationQueryEndpointTests.cs` são criados nesta própria task; o Testcontainers Postgres é
  efêmero.
- **Dependências futuras:** Nenhuma — a task 5.0 (E2E) consome o endpoint já pronto e testado por
  esta task, sem precisar reabri-lo.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.GetReservationByIdQueryHandlerTests"`
- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationQueryEndpointTests"`
- [ ] Os dois seletores encontram pelo menos um teste cada e não executam casos sem relação com esta
      task
- [ ] Build compila sem erros: `dotnet build services/booking/LocalizeStay.Booking.sln`
- [ ] `GetReservationByIdQueryHandlerTests` confirma: id vazio/não-Guid → `ValidationException` sem
      chamar `GetByIdAsync`; repositório retorna `null` → `ReservationNotFoundException`; repositório
      retorna uma `Reservation` → handler devolve a mesma instância
- [ ] `ReservationQueryEndpointTests` confirma os 5 cenários: 200 solicitada/pendente (sem
      `cancellationReason`), 200 confirmada/autorizado (sem `cancellationReason`), 200
      cancelada/rejeitado (com `cancellationReason`), 404 `RESERVATION_NOT_FOUND`, 400
      `VALIDATION_ERROR`
- [ ] Nenhuma rejeição (400/404) persiste ou altera qualquer estado existente
- [ ] Checkpoint de feedback executado conforme descrito acima
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente o backend (query + endpoint), não a UI (tasks 3.0/4.0) nem o
      E2E full-stack (task 5.0)
