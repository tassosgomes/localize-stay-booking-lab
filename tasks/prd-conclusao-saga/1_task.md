---
status: done
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
<feedback_checkpoint>`scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationSagaTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ConfirmReservationCommandHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.CancelReservationCommandHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Messaging.PaymentAuthorizedConsumerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Messaging.PaymentRejectedConsumerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentAuthorizedConsumptionTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentRejectedConsumptionTests"` aprovado; cada filtro encontra testes, o build da solution Booking compila e os cenários RabbitMQ/Postgres passam</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationSagaTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ConfirmReservationCommandHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.CancelReservationCommandHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Messaging.PaymentAuthorizedConsumerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Messaging.PaymentRejectedConsumerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentAuthorizedConsumptionTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentRejectedConsumptionTests"</gate_command>
<gate_test_selector>Classes `ReservationTests`, `ReservationSagaTests`, `ConfirmReservationCommandHandlerTests`, `CancelReservationCommandHandlerTests`, `PaymentAuthorizedConsumerTests`, `PaymentRejectedConsumerTests`, `PaymentAuthorizedConsumptionTests` e `PaymentRejectedConsumptionTests`, com os namespaces completos declarados no `gate_command`</gate_test_selector>
<gate_expected_result>Os 8 filtros selecionam pelo menos um teste e passam sem falhas; os testes unitários comprovam transições (incluindo a gravação de `TerminalTransitionAt`), outcomes, guards, correlação e falha best-effort; os testes de integração comprovam consumo real, migration aplicada, persistência (status + `terminal_transition_at`), payload/headers CloudEvents fiéis ao instante persistido e ausência de publicação duplicada; `dotnet build` e format scoped do gate ficam verdes</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Ao receber um `payment.payment_authorized` ou `payment.payment_rejected` com `correlationId` válido e conhecido, Booking conclui a Reservation Saga em `confirmada`/`Authorized` ou `cancelada`/`Rejected` com o motivo de negócio, persiste o estado e o instante UTC da transição terminal (`TerminalTransitionAt`, EN-01/ADR-005 — pré-requisito de `tasks/prd-publicacao-reservation-calendar`) e publica exatamente o evento final correspondente com esse mesmo instante. Mensagens com correlação ausente/inválida/desconhecida, duplicadas, tardias ou conflitantes não alteram uma Reservation terminal nem seu `TerminalTransitionAt`, e não publicam novamente; se o publish final falhar, o estado persistido permanece terminal e a falha é apenas logada, sem retry ou Outbox nesta fase.</vertical_slice>
</task_context>

# Tarefa 1.0: Fechar a Reservation Saga por autorização/rejeição — consumo, transição, publicação e proteção terminal (V-01)

> **Emenda de alinhamento (2026-09-13):** `Reservation.Confirm()`/`Cancel(string)` foram substituídos
> por `Confirm(DateTime terminalTransitionAt)`/`Cancel(string cancellationReason, DateTime terminalTransitionAt)`,
> que persistem `Reservation.TerminalTransitionAt` (nova coluna `booking.reservations.terminal_transition_at`)
> no mesmo commit da transição. Esta task agora inclui a migration correspondente. A mudança decorre
> do handoff EN-01 de `tasks/prd-publicacao-reservation-calendar/techspec.md` e da
> [ADR-005](../../docs/adr/adr-005-terminal-transition-timestamp.md) (Accepted, 2026-09-13): o dataset
> `reservation_calendar_v1` (F05) exige um `updated_at` estável que só Booking/F04 pode gravar. A
> TechSpec de origem (`techspec.md`) já registra a mesma emenda. Nenhuma outra decisão desta task muda.

## Relacionada às User Stories

- Guest confirma automaticamente sua Reservation quando Payment publica `payment.payment_authorized` (cobertura direta — RF-01).
- Guest recebe cancelamento automático com um motivo de negócio quando Payment publica `payment.payment_rejected` (cobertura direta — RF-02/DP-01).
- Autor/arquiteto observa Booking fechando a saga coreografada e emitindo o resultado final para os consumidores downstream (cobertura direta — RF-01/RF-02/RN-09).
- Catalog e Notification recebem somente eventos finais coerentes com o resultado de Payment, sem que Booking implemente seus consumidores (cobertura direta via contrato e payload; integração downstream fica fora desta task).

## Visão Geral

Esta task implementa a única fatia V-01 da TechSpec. O ponto de entrada é uma mensagem CloudEvents
recebida nas filas próprias de Booking; o consumer é um adapter fino e apenas transforma a mensagem
em um command. O handler busca a Reservation pela `ReservationSaga.CorrelationId`, decide se o evento
é aplicável, aplica a transição de domínio, persiste antes de publicar e trata o publish final como
best-effort.

F02 e F03 já estão disponíveis em `main` no momento da criação deste plano. `SagaState.Authorized`,
`SagaState.Rejected`, `ReservationSaga.CancellationReason`, o mapeamento EF, a migration de F02 e
`IReservationRepository.UpdateAsync` devem ser reutilizados sem reimplementação. Uma única migration
nova é adicionada por esta task (`AddTerminalTransitionAtToReservations`), exigida pelo handoff EN-01
de F05 (ver emenda no topo deste arquivo).

## Entrega Observável

- **Entrada ou gatilho:** mensagem `payment.payment_authorized` na fila `booking.payment_authorized` ou `payment.payment_rejected` na fila `booking.payment_rejected`, envelopada por CloudEvents e contendo ao menos `correlationId`.
- **Resultado esperado — autorização:** Reservation `Solicitada` → `Confirmada`, `TerminalTransitionAt` gravado com o instante UTC calculado pelo handler, Saga `PaymentPending` → `Authorized`, persistência via `UpdateAsync` e um evento `booking.reservation_confirmed` com payload/headers do contrato, cujo `confirmedAt` é exatamente o `TerminalTransitionAt` persistido.
- **Resultado esperado — rejeição:** Reservation `Solicitada` → `Cancelada`, `TerminalTransitionAt` gravado com o instante UTC calculado pelo handler, Saga `PaymentPending` → `Rejected`, `CancellationReason = "Pagamento rejeitado pela simulação de Payment."`, persistência via `UpdateAsync` e um evento `booking.reservation_cancelled` com payload/headers do contrato, cujo `cancelledAt` é exatamente o `TerminalTransitionAt` persistido.
- **Resultado esperado — mensagens não aplicáveis:** correlação ausente/inválida no adapter, correlação desconhecida no handler ou Reservation já terminal retornam sem mutação/publicação; a ocorrência é registrada com `CorrelationId`/`ReservationId` quando disponíveis.
- **Resultado esperado — falha de publicação:** depois de `UpdateAsync` bem-sucedido, exceção do publisher é logada e não relançada; não há retry manual, Outbox, dedup store ou compensação nesta fase.
- **Checkpoint de feedback:** o `gate_command` desta task, com os 8 filtros focalizados, termina com `GATE: APROVADO`; filtros vazios, falhas de build ou Testcontainers indisponíveis não são tratados como sucesso.
- **Seletor focalizado:** os 8 nomes completos declarados em `gate_test_selector`.
- **Fora deste checkpoint:** decisão de autorização/rejeição em Payment; consumidores de Catalog/Notification; endpoint ou tela de frontend; Outbox, retry de negócio, deduplicação robusta, timeout, DLQ/reprocessamento manual e compensação da F06; bindings downstream.

## Requisitos

- `Reservation` ganha `TerminalTransitionAt` (nullable enquanto `Solicitada`, não nulo após transição terminal; mapeado para `booking.reservations.terminal_transition_at`, `timestamptz`).
- `Reservation.Confirm(DateTime terminalTransitionAt)` só aceita `Status == Solicitada`, atribui `Confirmada`, normaliza o instante recebido para UTC e grava `TerminalTransitionAt`, e chama `Saga.MarkAuthorized()`.
- `Reservation.Cancel(string cancellationReason, DateTime terminalTransitionAt)` só aceita `Status == Solicitada`, atribui `Cancelada`, normaliza o instante recebido para UTC e grava `TerminalTransitionAt`, e chama `Saga.MarkRejected(cancellationReason)`.
- A guarda privada de domínio lança `InvalidOperationException` para qualquer estado não solicitado, sem alterar `Status`/`TerminalTransitionAt`; o handler deve verificar o estado antes, para que duplicidade/tardio seja um outcome normal e não exceção de controle.
- Cada `CommandHandler` calcula um único `DateTime.UtcNow` (`terminalTransitionAt`) antes de chamar `Confirm`/`Cancel` e reutiliza exatamente esse valor no payload publicado (`confirmedAt`/`cancelledAt`); nunca dois `DateTime.UtcNow` distintos para o mesmo evento.
- Uma migration EF Core aditiva adiciona `terminal_transition_at` (`timestamptz` nullable) a `booking.reservations` e instala a constraint `(status = 'solicitada' AND terminal_transition_at IS NULL) OR (status IN ('confirmada', 'cancelada') AND terminal_transition_at IS NOT NULL)`. Sem backfill: se houver Reservation terminal pré-existente sem timestamp, a migration falha ao aplicar a constraint.
- `ReservationSaga.MarkAuthorized()` grava `SagaState.Authorized`; `MarkRejected(reason)` grava `SagaState.Rejected` e a razão exatamente recebida. A propriedade `CancellationReason` e os enum values vêm de F02.
- `IReservationRepository` ganha `GetByCorrelationIdAsync(Guid, CancellationToken)`; a implementação inclui `Saga`, consulta `Saga.CorrelationId` e usa `FirstOrDefaultAsync`, nunca `SingleOrDefaultAsync`, pois a coluna não tem unicidade declarada. `UpdateAsync` de F03 permanece a única operação de escrita aditiva.
- Cada handler retorna outcomes explícitos (`Confirmed`/`Cancelled`, `AlreadyTerminal`, `NotCorrelatable`), nunca publica nos dois últimos casos e propaga `CancellationToken`.
- Publishers adaptam `IRmqPublisher.PublishToTopicAsync` para exchanges topic duráveis `booking.reservation_confirmed` e `booking.reservation_cancelled`, com routing key igual ao evento e CloudEvent types `com.localizestay.booking.reservation_confirmed.v1` e `com.localizestay.booking.reservation_cancelled.v1`.
- Payloads publicados usam `[JsonPropertyName]` explícito em `camelCase`: `correlationId`, `causationId`, `reservationId`, `accommodationId`, `guestReference`, `checkIn`, `checkOut` e o timestamp correspondente; o cancelamento inclui `cancellationReason`. Não incluir `totalAmount`, `currency`, `pricePerNight`, `guestsCount` ou detalhes técnicos de Payment.
- Headers `x-correlation-id` e `x-causation-id` são preenchidos com `reservation.Saga.CorrelationId`; nesta versão `causationId == correlationId` conforme o contrato aprovado.
- `PaymentAuthorizedMessage`/`PaymentRejectedMessage` desserializam defensivamente `CorrelationId` como `string?` e o timestamp opcional (`AuthorizedAt?`/`RejectedAt?`), ignorando campos extras. GUID ausente, vazio ou inválido não gera command nem NACK/retry de negócio.
- `PaymentRejectedConsumer` usa somente o texto aprovado de DP-01 e nunca lê um eventual motivo técnico do payload de Payment.
- `MessagingExtensions` registra as quatro exchanges envolvidas como topic/durable (`payment.payment_authorized`, `payment.payment_rejected`, `booking.reservation_confirmed` e `booking.reservation_cancelled`), os dois publishers e dois `AddRmqTopicConsumer<TMessage,THandler>` com exchange, fila e binding pattern definidos pelas topologias. O uso da biblioteca `Rmq.CloudEvents` 1.1.1 dispensa `BackgroundService`, `RabbitMQ.Client` manual e `IServiceScopeFactory` no consumer; a biblioteca cria o invoker scoped por mensagem.
- `ApplicationExtensions` registra explicitamente os dois `ICommandHandler<,>` porque o `Dispatcher` atual resolve handlers pelo tipo via DI; sem esse wiring a jornada não executa, embora a TechSpec não tenha enumerado o arquivo.
- Logs estruturados cobrem confirmação/cancelamento efetivados, correlação inexistente, Saga terminal e falha best-effort, sempre com os identificadores disponíveis. Não adicionar métricas ou tracing customizados.

## Arquivos Envolvidos

- **Criar:**
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationConfirmedPublisher.cs`
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationCancelledPublisher.cs`
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/ConfirmReservationCommand.cs`
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/ConfirmReservationCommandHandler.cs`
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/CancelReservationCommand.cs`
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/CancelReservationCommandHandler.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/ReservationConfirmedRmqPublisher.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/ReservationCancelledRmqPublisher.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentAuthorizedTopology.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentAuthorizedMessage.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentAuthorizedConsumer.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentRejectedTopology.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentRejectedMessage.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentRejectedConsumer.cs`
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/ConfirmReservationCommandHandlerTests.cs`
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/CancelReservationCommandHandlerTests.cs`
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/Messaging/PaymentAuthorizedConsumerTests.cs`
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/Messaging/PaymentRejectedConsumerTests.cs`
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/PaymentAuthorizedConsumptionTests.cs`
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/PaymentRejectedConsumptionTests.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Migrations/*_AddTerminalTransitionAtToReservations.cs` — migration aditiva de `terminal_transition_at` + constraint (EN-01/ADR-005); nome/timestamp definitivos gerados pelo EF.
- **Modificar:**
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Reservation.cs` — adicionar `TerminalTransitionAt`, transições `Confirm(DateTime)`/`Cancel(string, DateTime)` e guarda de invariante.
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/ReservationConfiguration.cs` — mapear `TerminalTransitionAt` para `terminal_transition_at` (`timestamptz` nullable).
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Migrations/BookingDbContextModelSnapshot.cs` — atualizado automaticamente pelo EF ao gerar a migration.
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/ReservationSaga.cs` — adicionar os dois mutators, preservando o campo de F02.
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationRepository.cs` — adicionar busca por correlação, preservando `UpdateAsync` de F03.
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/ReservationRepository.cs` — implementar a busca por `Saga.CorrelationId`.
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/ApplicationExtensions.cs` — registrar os dois command handlers no DI.
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/MessagingExtensions.cs` — registrar exchanges, publishers e consumers.
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/LocalizeStay.Booking.UnitTests.csproj` — adicionar a referência ao `AwesomeAssertions` já versionado em `Directory.Packages.props`; não criar pacote ou atualizar versão.
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/ReservationTests.cs` — cobrir confirmação, cancelamento e guards.
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/ReservationSagaTests.cs` — cobrir `MarkAuthorized`/`MarkRejected`.
- **Referência, não alterar:**
  - `tasks/prd-conclusao-saga/api-contract.yaml` — fonte única de schemas, exchanges, filas, headers e CloudEvents.
  - `tasks/prd-conclusao-saga/api-contract.md` — decisões e validação do contrato já aprovado.
  - `tasks/prd-conclusao-saga/techspec.md` — decisões técnicas e inventário aprovado.
  - `tasks/prd-consulta-reserva/techspec.md` e `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Migrations/20260913005542_AddSagaCancellationReason.cs` — vocabulário/schema de F02 já disponível.
  - `tasks/prd-publicacao-reservation-calendar/techspec.md` (§Handoff obrigatório com F04, EN-01) e `docs/adr/adr-005-terminal-transition-timestamp.md` — assinatura normativa de `Confirm`/`Cancel` e semântica de `TerminalTransitionAt` consumida por `integration.reservation_calendar_v1`.
  - `.config/dotnet-tools.json` — versão fixada do `dotnet-ef` para gerar a migration.
  - `tasks/prd-solicitacao-pagamento/techspec.md` e `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationRepository.cs` — assinatura de `UpdateAsync` de F03.
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/RequestReservationCommandHandler.cs` — logging e publish best-effort.
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/PaymentRequestedRmqPublisher.cs` — adapter/payload/headers de `Rmq.CloudEvents`.
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/CustomWebApplicationFactory.cs`, `BookingIntegrationTestCollection.cs` e `Catalog/FakeCatalogServerFactory.cs` — fixtures reais de Postgres/RabbitMQ e Catalog falso.
  - `context/architecture-baseline.md`, `domains/booking/domain.md`, `docs/adr/adr-001-backend-stack-dotnet.md` e `docs/adr/adr-002-broker-fase0-rabbitmq.md` — fronteiras, regras e decisões duráveis.
- **Skills para consultar durante implementação:**
  - `.agents/skills/dotnet-architecture/SKILL.md` — fronteiras Domain/Application/Infra/API, CQRS nativo, DI por tipo e invariantes.
  - `.agents/skills/dotnet-dependency-config/SKILL.md` — `Rmq.CloudEvents`, topologia RabbitMQ, EF Core e ausência de migration/pacote novo.
  - `.agents/skills/dotnet-testing/SKILL.md` — AAA, xUnit/Moq e integração com WebApplicationFactory/Testcontainers.

## Subtarefas

- [x] 1.1 Estender `Reservation`/`ReservationSaga` com as transições, guardas e testes unitários, reutilizando sem alteração o vocabulário/migration de F02.
- [x] 1.2 Criar portas, commands/outcomes, handlers e `GetByCorrelationIdAsync`; registrar os handlers no `ApplicationExtensions` e cobrir sucesso, não correlação, estado terminal e publisher com exceção.
- [x] 1.3 Criar publishers CloudEvents e adaptar `ReservationRepository`; registrar exchanges/publishers, topologias de consumo e `AddRmqTopicConsumer` no wiring da API.
- [x] 1.4 Criar os dois consumers defensivos e seus testes; validar dispatch, ausência de dispatch para GUID inválido e motivo fixo de rejeição.
- [x] 1.5 Criar os dois testes de integração com Postgres/RabbitMQ reais, incluindo sucesso, payload/headers, persistência, duplicidade/tardio/conflito/não correlação e o gate focalizado.

## Sequenciamento

- **Bloqueado por:** Nenhum — F02 (`522a6cd`) e F03 já estão em `main`; contrato, gate e fixtures existem.
- **Desbloqueia:** Nenhuma task deste plano; habilita F05 e os consumidores de Catalog/Notification fora deste plano.
- **Paralelizável:** Não. A task é única e modifica artefatos compartilhados de Domain, repository, DI, messaging e fixtures; a ordem interna é Domain → Application/DI → Infra → Api/Messaging → testes.

## Rastreabilidade

- **Esta tarefa cobre:** RF-01, RF-02, RF-03; RN-07, RN-08, RN-09, RN-10, RN-11; DP-01, DP-02, DP-03 e DP-04; User Stories de Guest, autor/arquiteto, Catalog e Notification consumidor.
- **Evidência esperada:** testes unitários focados nos dois caminhos e integração que publica mensagens simuladas de Payment, aguarda a persistência terminal, lê os eventos finais reais e comprova que duplicados/tardios/não correlacionáveis não produzem nova mutação/publicação.

## Detalhes de Implementação

### Fluxo ponta a ponta de autorização

1. `AddRmqTopicConsumer<PaymentAuthorizedMessage, PaymentAuthorizedConsumer>` declara a exchange topic `payment.payment_authorized`, a fila durável `booking.payment_authorized` e o binding `payment.payment_authorized`.
2. `PaymentAuthorizedConsumer` valida `Guid.TryParse(message.CorrelationId)`. Se falhar, loga warning com `context.QueueName` e retorna sem dispatch.
3. Para GUID válido, envia `ConfirmReservationCommand(correlationId)` ao `IDispatcher`; não decide regra de negócio no adapter.
4. `ConfirmReservationCommandHandler` usa `GetByCorrelationIdAsync`. `null` retorna `NotCorrelatable` após log; estado diferente de `Solicitada` retorna `AlreadyTerminal` após log; somente estado solicitado calcula `terminalTransitionAt = DateTime.UtcNow` e chama `reservation.Confirm(terminalTransitionAt)`.
5. O handler chama `UpdateAsync` antes de publicar. O publisher envia `booking.reservation_confirmed` com `confirmedAt = reservation.TerminalTransitionAt` (o mesmo instante persistido, não um novo `UtcNow`), campos contratuais, CloudEvent type versionado e os dois headers de correlação.
6. Exceção do publisher que não seja `OperationCanceledException` é logada com estado terminal já persistido; o handler retorna `Confirmed` sem segunda tentativa.

### Fluxo ponta a ponta de rejeição

1. `AddRmqTopicConsumer<PaymentRejectedMessage, PaymentRejectedConsumer>` declara a exchange `payment.payment_rejected`, a fila durável `booking.payment_rejected` e o binding `payment.payment_rejected`.
2. `PaymentRejectedConsumer` valida a correlação com a mesma regra defensiva; para GUID válido envia `CancelReservationCommand(correlationId, "Pagamento rejeitado pela simulação de Payment.")`.
3. `CancelReservationCommandHandler` repete a busca/decisão de correlação e estado terminal, calcula `terminalTransitionAt = DateTime.UtcNow` e chama `reservation.Cancel(reason, terminalTransitionAt)` somente em `Solicitada`, persiste via `UpdateAsync` e publica `booking.reservation_cancelled` best-effort.
4. O payload de cancelamento usa `cancelledAt = reservation.TerminalTransitionAt` (o mesmo instante persistido), `reservation.Saga.CancellationReason` e os campos mínimos do contrato; nunca propaga eventual motivo técnico de Payment.

### Decisões técnicas fechadas

- O domínio permanece independente de EF Core, RabbitMQ e ASP.NET Core. Consumers são driving adapters finos; handlers concentram regra e orquestração.
- CQRS nativo existente continua sendo usado sem MediatR. O `Dispatcher` faz resolução por tipo; os dois registros explícitos em `ApplicationExtensions` são obrigatórios.
- A biblioteca instalada é `Rmq.CloudEvents` 1.1.1. Use `AddRmqTopicConsumer` e o invoker scoped provido pela biblioteca; não replicar o `BackgroundService` manual de `DiagnosticPingConsumer`.
- A consulta por correlação inclui `Saga` e usa `FirstOrDefaultAsync` por não existir índice único em `correlation_id`. Não assumir que `Reservation.Id == Saga.CorrelationId` é garantia de domínio.
- O publisher final ocorre somente depois da persistência. Não fazer rollback lógico, retry manual, Outbox ou dedup store; a janela de concorrência verdadeira é risco aceito para F06.
- `Confirm`/`Cancel` recebem `DateTime terminalTransitionAt` e persistem `TerminalTransitionAt` no mesmo commit da transição (EN-01/ADR-005); o handler calcula um único `DateTime.UtcNow` e o reutiliza no payload publicado. Não recalcular o timestamp na publicação nem deixar a coluna sem constraint.
- `[JsonPropertyName]` é obrigatório em todos os records de mensagem novos para garantir `camelCase` do contrato, independentemente da política global de serialização.
- Nenhum contrato é alterado. O schema de Payment continua provisório: somente `correlationId` e timestamp informativo são desserializados; campos extras são ignorados.

### Testes e checkpoint

- `ReservationTests`: confirmação/cancelamento desde `Solicitada` gravando `TerminalTransitionAt` com o instante recebido, marcação da Saga, `InvalidOperationException` desde `Confirmada`/`Cancelada` sem alterar `Status`/`TerminalTransitionAt`, e normalização de instante não-UTC para UTC.
- `ReservationSagaTests`: `MarkAuthorized` e `MarkRejected` atualizam exatamente estado e motivo, sem modificar o comportamento de `PaymentRequestSentAt` de F03.
- Handlers: encontrado e pendente (Update + Publish uma vez); não encontrado (sem efeitos); terminal (sem Update/Publish); publisher falhando (estado/retorno preservados, sem rethrow).
- Consumers: correlação válida despacha exatamente o command esperado; nula/vazia/não-GUID não despacha.
- Integração: criar Reservation por F01 com `FakeCatalogServerFactory`, publicar o envelope Payment em exchange real, aguardar com timeout curto o banco, conferir estado/Saga/`terminal_transition_at` e ler uma fila temporária nas exchanges finais. Validar `type`, `data` (incluindo `confirmedAt`/`cancelledAt` igual ao `terminal_transition_at` persistido), headers, campos mínimos e ausência de segunda mensagem/mudança de timestamp após duplicado/tardio/conflitante.
- Não adicionar teste de derrubar o broker no meio da integração: a evidência de RF-03 é unitária, decisão explicitamente aprovada na TechSpec.

**Convenções da stack (das skills consultadas):**

- Domain/Application/Infrastructure/API seguem Clean Architecture; interfaces permanecem na Application e adapters concretos na Infra/API (`dotnet-architecture`).
- Operações assíncronas recebem e propagam `CancellationToken`; handlers e consumers aguardam todas as Tasks (`dotnet-architecture`, `dotnet-testing`).
- Testes unitários seguem xUnit + Moq, AAA e nomes `MethodName_Condition_ExpectedBehavior`; testes de persistência/mensageria usam WebApplicationFactory e Testcontainers reais (`dotnet-testing`).
- RabbitMQ usa CloudEvents, ACK em sucesso, retry/DLQ padrão da biblioteca para falha técnica não tratada; caminhos de negócio ignorados retornam sucesso e não vão para DLQ (`dotnet-dependency-config`).
- Testes unitários usam xUnit + Moq + AwesomeAssertions, AAA e nomes descritivos; testes de integração usam banco/broker reais em Testcontainers (`dotnet-testing`).
- Não adicionar credenciais, endpoints de broker ou novas versões de pacote em arquivos versionados (`dotnet-dependency-config`).

## Prontidão para Implementação

- **Decisões fechadas:** uma única V-01; nomes dos commands/outcomes; busca por `Saga.CorrelationId` com `FirstOrDefaultAsync`; transições e guards de domínio; `TerminalTransitionAt` persistido no mesmo commit via `Confirm(DateTime)`/`Cancel(string, DateTime)` (EN-01/ADR-005, handoff de `tasks/prd-publicacao-reservation-calendar/techspec.md`); texto fixo de DP-01; exchanges/filas/routing keys/CloudEvent types; payload mínimo em `camelCase` com `confirmedAt`/`cancelledAt` = `TerminalTransitionAt`; headers de correlação; persistir antes de publicar; publish best-effort sem retry/Outbox/dedup; `AddRmqTopicConsumer`; sem endpoint/frontend, pacote ou versão nova. A referência de `AwesomeAssertions` é aditiva no projeto de testes e usa a versão central existente.
- **Limites de decisão do implementer:** nomes de métodos privados, organização interna dos testes e helpers de fixture, detalhes de `TopicSubscriptionOptions` equivalentes ao contrato, timeout curto de polling e nome/timestamp da migration (gerado pelo EF). Nenhuma dessas escolhas pode alterar o contrato ou criar tecnologia nova.
- **Dependências disponíveis:** `main` com F01/F02/F03; `ReservationStatus` terminal; F02 `SagaState`/`CancellationReason`/migration; F03 `UpdateAsync`; `Rmq.CloudEvents` 1.1.1; `AwesomeAssertions` 9.6.0 já pinado em `Directory.Packages.props`; `CustomWebApplicationFactory`, coleção de integração, Fake Catalog e `scripts/ai-flow/gate.sh`; `dotnet-ef` fixado em `.config/dotnet-tools.json`.
- **Artefatos exigidos pelo gate:** os 6 arquivos unitários novos, 2 arquivos de integração e a migration `AddTerminalTransitionAtToReservations` são criados nesta task; `ReservationTests`/`ReservationSagaTests`/`ReservationConfiguration.cs`/`BookingDbContextModelSnapshot.cs` são modificados nesta task; fixtures, solution e biblioteca são preexistentes.
- **Dependências futuras:** Nenhuma para compilar/validar esta task. `tasks/prd-publicacao-reservation-calendar` (F05) depende desta task para existir (`terminal_transition_at` é pré-requisito de `integration.reservation_calendar_v1`), não o inverso; Catalog/Notification só consomem os eventos depois.
- **Ambiguidades bloqueantes:** Nenhuma. O schema do produtor Payment é provisório, mas o piso de `correlationId`/timestamp, o tratamento defensivo e o motivo de negócio já estão decididos; a corrida concorrente residual e bindings downstream são riscos/decisões futuras documentadas, não bloqueadores.

## Criterios de Sucesso (Verificáveis)

- [ ] `ReservationTests` passa e comprova `Confirm(DateTime)`/`Cancel(string, DateTime)` desde `Solicitada` gravando `TerminalTransitionAt`, marcação da Saga, guardas contra estados terminais sem alterar `Status`/`TerminalTransitionAt`, e normalização de instante não-UTC.
- [ ] `ReservationSagaTests` passa e comprova `Authorized`/`Rejected` e `CancellationReason` sem regressão nos testes de F03.
- [ ] `ConfirmReservationCommandHandlerTests` passa: sucesso chama `UpdateAsync`/publisher uma vez; não correlacionável e terminal não têm efeitos; falha de publisher não relança e preserva o estado terminal.
- [ ] `CancelReservationCommandHandlerTests` passa com os mesmos cenários e confere o texto exato de `cancellationReason` persistido/publicado.
- [ ] `PaymentAuthorizedConsumerTests` e `PaymentRejectedConsumerTests` passam: GUID válido despacha uma vez; nulo/vazio/inválido não despacha.
- [ ] `PaymentAuthorizedConsumptionTests` passa contra Postgres/RabbitMQ Testcontainers: a migration `AddTerminalTransitionAtToReservations` aplica; autorização correlacionada persiste `confirmada`/`Authorized`/`terminal_transition_at`, publica exatamente um `booking.reservation_confirmed` fiel ao contrato com `confirmedAt` igual ao timestamp persistido, ignora duplicado/tardio e não correlacionável sem alterar o timestamp.
- [ ] `PaymentRejectedConsumptionTests` passa contra Postgres/RabbitMQ Testcontainers: rejeição correlacionada persiste `cancelada`/`Rejected`/`terminal_transition_at` + motivo, publica exatamente um `booking.reservation_cancelled` fiel ao contrato com `cancelledAt` igual ao timestamp persistido, ignora duplicado/tardio/conflitante sem alterar o timestamp.
- [ ] Os payloads de integração contêm somente os campos aprovados, `camelCase`, timestamps UTC iguais ao `TerminalTransitionAt` persistido, `correlationId`/`causationId` coerentes e headers `x-correlation-id`/`x-causation-id` esperados.
- [ ] `scripts/ai-flow/gate.sh` encontra pelo menos um teste em cada um dos 8 filtros; nenhum selector vazio é aceito.
- [ ] `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln ...` retorna `GATE: APROVADO`, com format scoped, build sem erros/warnings e testes verdes.
- [ ] Falha do publisher final é logada sem rethrow, sem nova tentativa automática e sem alterar o estado terminal/timestamp já salvo.
- [ ] A migration `AddTerminalTransitionAtToReservations` converge quando aplicada duas vezes e instala a constraint solicitada-null/terminal-non-null; nenhum pacote/versão nova, endpoint HTTP, frontend, Outbox, dedup store ou consumidor de Catalog/Notification é adicionado por esta task; a referência de `AwesomeAssertions` usa a versão já centralizada.
- [ ] Todos os artefatos usados pelo gate existem antes da task ou são criados/modificados nela; fixtures preexistentes estão referenciadas explicitamente.
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta fatia, e a evidência prova apenas V-01.
