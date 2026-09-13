---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: []
---

<task_context>
<domain>services/booking</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>external_apis,database</dependencies>
<unblocks>""</unblocks>
<feedback_checkpoint>`dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationSagaTests"` verde; `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.RequestReservationCommandHandlerTests"` verde (todos os cenários existentes de F01 + os 2 novos de F03); `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentRequestedPublishingTests"` verde contra RabbitMQ + Postgres reais (Testcontainers)</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationSagaTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.RequestReservationCommandHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentRequestedPublishingTests"</gate_command>
<gate_test_selector>Classes `ReservationSagaTests`, `RequestReservationCommandHandlerTests` (`LocalizeStay.Booking.UnitTests`) e `PaymentRequestedPublishingTests` (`LocalizeStay.Booking.IntegrationTests`)</gate_test_selector>
<gate_expected_result>Todos os testes das 3 classes passam (verde); 0 falhas; `RequestReservationCommandHandlerTests` inclui os cenários já existentes de F01 (nenhuma regressão) mais os 2 novos (sucesso publica+registra; falha não registra e não propaga); `PaymentRequestedPublishingTests` confirma na exchange `booking.payment_requested` (Testcontainers RabbitMQ) `correlationId`/`causationId` = `reservation.Saga.CorrelationId`, `totalAmount` string decimal 2 casas idêntico ao congelado, `currency="BRL"`, `cloudEventType="com.localizestay.booking.payment_requested.v1"`, e a coluna `booking.reservation_sagas.payment_request_sent_at` preenchida</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Toda `Reservation` criada com sucesso por F01 resulta em exatamente uma tentativa de publicação de `booking.payment_requested` com `correlationId`/`totalAmount`/`currency` fiéis aos congelados; a `ReservationSaga` registra `PaymentRequestSentAt` quando a publicação é bem-sucedida; uma falha de publicação não lança exceção, não desfaz a `Reservation` e não grava `PaymentRequestSentAt` (RF-01 completo, sucesso + falha, feature indivisível por decisão da TechSpec)</vertical_slice>
</task_context>

# Tarefa 1.0: Solicitação automática de pagamento — publisher, registro na Saga e testes (V-01)

## Relacionada às User Stories

- "Como Guest, eu quero que minha reserva avance automaticamente para a etapa de pagamento assim que
  for aceita, para não precisar executar nenhuma ação manual adicional" (cobertura direta)
- "Como autor/arquiteto em estudo, eu quero observar Booking publicando `booking.payment_requested`
  com o valor congelado e o identificador de correlação da saga..." (cobertura direta)
- "Como domínio Payment (consumidor do evento), eu quero receber, junto ao pedido, o valor total, a
  moeda e o identificador de correlação da Reservation, sem precisar consultar dados internos de
  Booking..." (cobertura direta — via isolamento do payload)

## Visão Geral

F01 (`RequestReservationCommandHandler`, já mesclado em `main`) persiste a `Reservation`/
`ReservationSaga` e publica `booking.reservation_requested` como primeiro bloco best-effort. Esta task
adiciona o segundo passo da saga coreografada: logo após esse ponto, publica `booking.payment_requested`
(`PaymentRequested.v1`) com o `correlationId` da `ReservationSaga` e o `totalAmount`/`currency` já
congelados (RN-06), e registra `ReservationSaga.PaymentRequestSentAt` quando a publicação é bem-sucedida.
É a única task do plano porque o PRD marca RF-01 como indivisível — o objetivo de estudo (coreografia de
saga via eventos) só se demonstra com o caminho de sucesso e o de falha de publicação implementados e
testados juntos, e todo o código novo pertence ao mesmo fluxo síncrono já existente (um handler, uma
porta, uma entidade).

## Entrega Observável

- **Entrada ou gatilho:** `RequestReservationCommandHandler.HandleAsync` conclui o bloco já existente
  de F01 (validação → Catalog → `Reservation.Create` → `AddAsync` → publicação best-effort de
  `reservation_requested`) para uma `Reservation` válida.
- **Resultado esperado:**
  - Sucesso: `IPaymentRequestedPublisher.PublishAsync` é chamado com a `Reservation` criada;
    `reservation.Saga.PaymentRequestSentAt` deixa de ser `null`; `IReservationRepository.UpdateAsync`
    é chamado exatamente uma vez; a mensagem publicada na exchange `booking.payment_requested` carrega
    `correlationId`/`causationId` = `reservation.Saga.CorrelationId`, `totalAmount` como string decimal
    de 2 casas idêntica ao congelado, `currency="BRL"`, `requestedAt` (UTC no momento da publicação).
  - Falha (publisher lança exceção): o handler não propaga a exceção, ainda retorna a `Reservation`
    normalmente, `PaymentRequestSentAt` permanece `null`, `UpdateAsync` não é chamado, e a falha é
    logada (`LogError`) com `ReservationId`/`CorrelationId`.
  - Reserva rejeitada por F01 (qualquer exceção de domínio antes do `AddAsync`): nenhum código novo
    desta task executa — já garantido pela estrutura do handler (nenhum teste dedicado além dos já
    existentes de F01).
- **Checkpoint de feedback:** as 3 classes de teste do `gate_command` acima, verdes.
- **Seletor focalizado:** `ReservationSagaTests`, `RequestReservationCommandHandlerTests`,
  `PaymentRequestedPublishingTests`.
- **Fora deste checkpoint:** consumo de `payment.payment_authorized`/`payment.payment_rejected` e
  qualquer transição de `SagaState` a partir desse resultado (F04); retry, Outbox, DLQ ou idempotência
  robusta da publicação (F06); qualquer endpoint HTTP novo (não existe nesta feature).

## Requisitos

- `ReservationSaga` ganha `PaymentRequestSentAt` (`DateTime?`, `private set`) e
  `MarkPaymentRequestSent(DateTime occurredAt)`, que apenas atribui o valor recebido. `SagaState` não é
  alterado por esta feature.
- Nova porta `IPaymentRequestedPublisher.PublishAsync(Reservation reservation, CancellationToken ct)`
  em `Application/Reservations`, mesmo padrão de `IReservationRequestedPublisher`.
- `IReservationRepository` ganha `Task UpdateAsync(Reservation reservation, CancellationToken ct)`;
  `ReservationRepository.UpdateAsync` implementa via `dbContext.Update(reservation)` +
  `SaveChangesAsync` (decisão fechada da TechSpec — `Update()` explícito, não só `SaveChangesAsync`
  sobre a entidade já rastreada).
- `RequestReservationCommandHandler` injeta `IPaymentRequestedPublisher` e adiciona um segundo bloco
  `try/catch` **depois** do bloco já existente de `reservation_requested`, sem alterar esse bloco nem o
  `return reservation` final:
  ```csharp
  try
  {
      await paymentRequestedPublisher.PublishAsync(reservation, cancellationToken).ConfigureAwait(false);

      reservation.Saga.MarkPaymentRequestSent(DateTime.UtcNow);
      await reservationRepository.UpdateAsync(reservation, cancellationToken).ConfigureAwait(false);

      logger.LogInformation(
          "Evento booking.payment_requested publicado para a Reservation {ReservationId} " +
          "(correlationId={CorrelationId})", reservation.Id, reservation.Saga.CorrelationId);
  }
  catch (Exception ex) when (ex is not OperationCanceledException)
  {
      logger.LogError(
          ex,
          "Falha best-effort ao publicar booking.payment_requested da Reservation {ReservationId} " +
          "(correlationId={CorrelationId}); ReservationSaga permanece sem registro de solicitação enviada",
          reservation.Id, reservation.Saga.CorrelationId);
  }
  ```
- `PaymentRequestedRmqPublisher` (Infra/Messaging) implementa a porta adaptando `IRmqPublisher` do
  `Rmq.CloudEvents` (mesma instância já configurada pela fundação/F01) — publica na exchange topic
  `booking.payment_requested`, routing key `booking.payment_requested`, `cloudEventType`
  `com.localizestay.booking.payment_requested.v1`, headers `x-correlation-id`/`x-causation-id` =
  `reservation.Saga.CorrelationId.ToString()`. Payload do evento (nomes `camelCase`, conforme
  `api-contract.yaml`):
  - `correlationId`/`causationId`: `reservation.Saga.CorrelationId` (ambos iguais — evento
    autocausado).
  - `totalAmount`: `reservation.TotalAmount.ToString("F2", CultureInfo.InvariantCulture)` — **string**,
    diferente do payload numérico de `reservation_requested` (contrato AsyncAPI já aprovado exige
    string decimal de 2 casas).
  - `currency`: `reservation.Currency`.
  - `requestedAt`: `DateTimeOffset.UtcNow` no momento da publicação.
- `MessagingExtensions.AddMessagingConfiguration` registra a nova exchange
  (`options.Exchanges[PaymentRequestedTopology.Exchange]`, topic, durable, ao lado das já existentes) e
  `services.AddScoped<IPaymentRequestedPublisher, PaymentRequestedRmqPublisher>()`.
- `ReservationSagaConfiguration` mapeia `PaymentRequestSentAt` → coluna `payment_request_sent_at`
  (`timestamptz`, nullable).
- Migration EF Core aditiva (`dotnet ef migrations add`, não escrita à mão) adiciona
  `payment_request_sent_at timestamptz null` em `booking.reservation_sagas`, sem alterar nenhuma outra
  coluna/tabela.
- Nenhuma classe de `Domain/Reservations/**` referencia `HttpClient`, `DbContext` ou `Rmq.CloudEvents`
  (regra do baseline, já respeitada por F01).

## Arquivos Envolvidos

- **Criar:**
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IPaymentRequestedPublisher.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/PaymentRequestedRmqPublisher.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Migrations/*_AddPaymentRequestSentAtToReservationSagas.cs`
    (gerada via `dotnet ef migrations add`)
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/ReservationSagaTests.cs`
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/PaymentRequestedPublishingTests.cs`
- **Modificar:**
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/ReservationSaga.cs`
    (`+ PaymentRequestSentAt`, `+ MarkPaymentRequestSent`)
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationRepository.cs`
    (`+ UpdateAsync`)
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/RequestReservationCommandHandler.cs`
    (injeta `IPaymentRequestedPublisher`; adiciona o segundo bloco best-effort, sem tocar no primeiro)
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/ReservationRepository.cs`
    (implementa `UpdateAsync`)
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/ReservationSagaConfiguration.cs`
    (mapeia a coluna nova)
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/MessagingExtensions.cs`
    (registra exchange + DI da nova porta)
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/RequestReservationCommandHandlerTests.cs`
    (+2 cenários novos; nenhum cenário existente de F01 é alterado)
- **Referência:**
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationRequestedPublisher.cs`
    — padrão exato da porta a replicar
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/ReservationRequestedRmqPublisher.cs`
    — modelo do adapter `Rmq.CloudEvents` (topologia, headers de correlação, record de payload)
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationEndpointTests.cs`
    — padrão de teste de integração que liga fila real a uma exchange e lê a mensagem via
    `RabbitMQ.Client` (`BindReservationEventsQueueAsync`/`BasicGetOneAsync`)
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationPersistenceTests.cs`
    — padrão de leitura direta de coluna via `dbContext.Database.SqlQueryRaw`
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Reservation.cs` — `Saga`,
    `TotalAmount`, `Currency` (não alterado nesta task)
  - `tasks/prd-solicitacao-pagamento/api-contract.yaml` — fonte única do schema do evento,
    exchange/routing key, headers e envelope CloudEvents
  - `domains/booking/domain.md` — RN-06; F03 no roadmap
  - `tasks/prd-consulta-reserva/techspec.draft.md` — confirma que `SagaState` é vocabulário à parte de
    `PaymentRequestSentAt` (evita colisão com a extensão de `Authorized`/`Rejected` planejada por F02)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — porta+adapter para a nova integração assíncrona, extensão do handler CQRS
    sem introduzir MediatR/barramento de eventos
  - `dotnet-dependency-config` — `IEntityTypeConfiguration<T>` estendido, migration EF Core aditiva,
    reaproveitamento do `Rmq.CloudEvents` já configurado
  - `dotnet-testing` — `xUnit`/`Moq` para o unitário; `CustomWebApplicationFactory` + Testcontainers
    (Postgres + RabbitMQ) para a integração, mesmo padrão de `ReservationEndpointTests`

## Subtarefas

- [ ] 1.1 Implementar `ReservationSaga.PaymentRequestSentAt`/`MarkPaymentRequestSent` e escrever
      `ReservationSagaTests` (chamada única grava o instante recebido)
- [ ] 1.2 Implementar `IPaymentRequestedPublisher`, `PaymentRequestedRmqPublisher`
      (`PaymentRequestedTopology`) e `IReservationRepository.UpdateAsync` +
      `ReservationRepository.UpdateAsync`
- [ ] 1.3 Estender `ReservationSagaConfiguration` (coluna nova) e gerar a migration
      `AddPaymentRequestSentAtToReservationSagas`
- [ ] 1.4 Estender `RequestReservationCommandHandler` com o segundo bloco best-effort e registrar a
      exchange + DI em `MessagingExtensions`
- [ ] 1.5 Estender `RequestReservationCommandHandlerTests` com os 2 cenários novos (sucesso
      publica+registra; falha não registra e não propaga), confirmando que os cenários já existentes
      de F01 continuam verdes sem modificação
- [ ] 1.6 Escrever `PaymentRequestedPublishingTests` (Testcontainers RabbitMQ + Postgres): cria uma
      Reservation via `POST /v1/reservations`, liga fila real à exchange `booking.payment_requested`,
      confere payload/headers/`cloudEventType`, e lê `payment_request_sent_at` via `BookingDbContext`

## Sequenciamento

- Bloqueado por: Nenhuma — F01 (`RequestReservationCommandHandler`, `ReservationSaga`,
  `IReservationRepository`, `ReservationSagaConfiguration`) já está mesclado em `main`
  (`fbeea05 Merge pull request #4 from tassosgomes/feature/prd-solicitacao-reserva`), pré-requisito
  bloqueante já satisfeito.
- Desbloqueia: F04 (Conclusão da Saga) — consumirá `IReservationRepository.UpdateAsync` para persistir
  a confirmação/cancelamento da `Reservation`; não é uma task deste plano.
- Paralelizável: Não — única task do plano, ordem interna normativa (Domain → Infra → Application →
  Wiring/Testes, `techspec.md` §Sequenciamento de Desenvolvimento).

## Rastreabilidade

- Esta tarefa cobre: RF-01 completo (os 3 cenários do AC: sucesso, falha de publicação, reserva
  rejeitada por F01); RN-06 (fidelidade do valor/moeda publicados aos congelados).
- Evidência esperada: `ReservationSagaTests` verde; `RequestReservationCommandHandlerTests` verde (F01
  sem regressão + 2 cenários novos); `PaymentRequestedPublishingTests` verde confirmando mensagem real
  na exchange e coluna persistida.

## Detalhes de Implementação

Trecho normativo do handler (já detalhado em Requisitos) reproduz exatamente o padrão do bloco já
existente de `reservation_requested` — mesma forma de `try/catch`, mesmo nível de log, mesma regra
"falha não propaga, não desfaz, não repete" (DP-02). O `RequestReservationCommandHandlerTests` atual
(F01) já tem o teste-espelho `HandleAsync_when_publisher_fails_returns_reservation_anyway_best_effort`
para o publisher de `reservation_requested` — o cenário de falha desta task usa o mesmo formato de
mock (`_paymentRequestedPublisher.Setup(...).ThrowsAsync(new InvalidOperationException(...))`),
adicionando o novo mock ao construtor do handler nos testes.

Modelo de dados (schema `booking`, já existente):

| Coluna (nova) | Tipo | Observação |
|---|---|---|
| `payment_request_sent_at` | `timestamptz`, nullable | `NULL` até a primeira (e única) publicação bem-sucedida; preenchida pela aplicação (`DateTime.UtcNow`), nunca pelo banco |

Mapeamento de payload do contrato (`api-contract.yaml`) → publisher:

| Campo do contrato | Origem no código |
|---|---|
| `correlationId` | `reservation.Saga.CorrelationId` |
| `causationId` | `reservation.Saga.CorrelationId` (evento autocausado) |
| `totalAmount` | `reservation.TotalAmount.ToString("F2", CultureInfo.InvariantCulture)` |
| `currency` | `reservation.Currency` |
| `requestedAt` | `DateTimeOffset.UtcNow` no momento da publicação |
| headers `x-correlation-id`/`x-causation-id` | mesmo valor de `correlationId`/`causationId`, como string |

O teste de integração segue o mesmo mecanismo de `ReservationEndpointTests.BindReservationEventsQueueAsync`
(exchange topic real declarada, fila exclusiva/auto-delete ligada por routing key, `BasicGetAsync` +
`BasicAckAsync`), trocando a exchange/routing key para `booking.payment_requested` e o
`cloudEventType` esperado para `com.localizestay.booking.payment_requested.v1`. Não há teste de
integração dedicado ao cenário de falha de publicação (broker indisponível) — decisão já registrada na
TechSpec, mesma que F01 já aplicou para `reservation_requested` (complexidade desproporcional ao valor
já coberto pelo unitário).

**Convenções da stack (das skills consultadas):**
- Porta + adapter para a nova integração assíncrona, sem MediatR/barramento de eventos internos
  (`dotnet-architecture`) — decisão explícita da TechSpec: estender o handler existente, não introduzir
  domain events.
- `IEntityTypeConfiguration<T>` estendido, migration EF Core gerada via `dotnet ef migrations add`
  (`dotnet-dependency-config`).
- Testes seguem Arrange-Act-Assert; integração usa `CustomWebApplicationFactory` +
  `[Collection("BookingIntegrationTests")]` (mesma coleção/fixture de `ReservationEndpointTests`)
  (`dotnet-testing`).

## Prontidão para Implementação

- **Decisões fechadas:** estender diretamente o handler/entidades de F01 em vez de um mecanismo de
  eventos internos desacoplado (já confirmado na TechSpec); `PaymentRequestSentAt` é atributo simples
  (`DateTime?`), não uma extensão de `SagaState`; `totalAmount` do evento é **string** decimal de 2
  casas (diferente do payload numérico de `reservation_requested`); `UpdateAsync` usa
  `dbContext.Update(reservation)` explícito; exchange/routing key/`cloudEventType` exatamente como em
  `api-contract.yaml` (já Aprovado, sem espaço para reinterpretação).
- **Limites de decisão do implementer:** nome exato do arquivo/timestamp da migration gerada;
  organização interna de `Infra/Messaging/PaymentRequestedRmqPublisher.cs` (record de payload privado,
  mesmo padrão de `ReservationRequestedRmqPublisher`).
- **Dependências disponíveis:** F01 completo e mesclado em `main`
  (`RequestReservationCommandHandler`, `ReservationSaga`, `IReservationRepository`,
  `ReservationSagaConfiguration`, `MessagingExtensions`, `ReservationEndpointTests`,
  `RequestReservationCommandHandlerTests` — todos existentes hoje no repositório).
- **Artefatos exigidos pelo gate:** as 3 classes de teste (`ReservationSagaTests`,
  `RequestReservationCommandHandlerTests` estendido, `PaymentRequestedPublishingTests`) são
  criadas/estendidas nesta própria task; nenhum fixture externo além dos Testcontainers já usados por
  `ReservationEndpointTests`/`ReservationPersistenceTests`.
- **Dependências futuras:** Nenhuma — F04 (fora deste plano) reutilizará `IReservationRepository.UpdateAsync`
  já pronto e testado por esta task.
- **Ambiguidades bloqueantes:** Nenhuma — TechSpec Aprovada sem Questões em Aberto que bloqueiem a
  implementação (a única pendência registrada, "F01 mesclada", já está resolvida).

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationSagaTests"`
- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.RequestReservationCommandHandlerTests"`
- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentRequestedPublishingTests"`
- [ ] Os três seletores encontram pelo menos um teste cada e não executam casos sem relação com esta
      task
- [ ] Build compila sem erros: `dotnet build services/booking/LocalizeStay.Booking.sln`
- [ ] Todos os cenários de rejeição já existentes de F01 em `RequestReservationCommandHandlerTests`
      continuam verdes sem modificação (nenhuma regressão)
- [ ] Cenário de sucesso: `PublishAsync` do novo publisher é chamado; `PaymentRequestSentAt` deixa de
      ser `null`; `UpdateAsync` é chamado exatamente uma vez
- [ ] Cenário de falha: exceção do publisher não propaga; handler retorna a `Reservation` normalmente;
      `PaymentRequestSentAt` permanece `null`; `UpdateAsync` não é chamado
- [ ] `PaymentRequestedPublishingTests` confirma na mensagem real: `correlationId`/`causationId` =
      `Saga.CorrelationId`, `totalAmount` string 2 casas idêntico ao retornado pela resposta HTTP,
      `currency="BRL"`, headers AMQP presentes, `cloudEventType` correto
- [ ] `PaymentRequestedPublishingTests` confirma `payment_request_sent_at` preenchido em
      `booking.reservation_sagas` via `BookingDbContext`
- [ ] Checkpoint de feedback executado: `scripts/ai-flow/gate.sh --filter="...ReservationSagaTests" --filter="...RequestReservationCommandHandlerTests" --filter="...PaymentRequestedPublishingTests"` → verde
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova RF-01 completo (sucesso + falha + reserva rejeitada), fechando a feature
