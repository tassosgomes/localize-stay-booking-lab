# Revisão — Task 1.0 (focused)

- Modo: focused
- Escopo: `tasks/prd-solicitacao-pagamento`, task 1.0 (única task do plano)
- Commit: sem commit (working tree da task, ainda não integrada)
- Attempt: 1

## Gate

Comando executado (worker fresco, antes de carregar material semântico):

```
scripts/ai-flow/gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationSagaTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.RequestReservationCommandHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentRequestedPublishingTests"
```

Resultado: `GATE: APROVADO`
- `dotnet format`: ok (14 arquivos)
- `dotnet build`: ok em todas as soluções do monorepo (0 warnings/0 errors)
- Testes: `ReservationSagaTests`=3 verdes, `RequestReservationCommandHandlerTests`=13 verdes,
  `PaymentRequestedPublishingTests`=1 verde (contra Testcontainers RabbitMQ + Postgres reais)

Exit code 0. Não reaproveitei aprovação do implementer — gate rodado nesta revisão, de forma
independente.

## Revisão semântica

### 1. Publicação best-effort (falha)

`RequestReservationCommandHandler.cs` (bloco adicionado após o já existente de
`reservation_requested`): `try { PublishAsync → MarkPaymentRequestSent → UpdateAsync → LogInformation }
catch (Exception ex) when (ex is not OperationCanceledException) { LogError }`. Confirmado:
- Exceção do publisher não propaga (capturada, apenas logada).
- `MarkPaymentRequestSent` e `UpdateAsync` estão **dentro** do `try`, depois da chamada ao publisher —
  logo, se `PublishAsync` lançar, nenhum dos dois executa (curto-circuito natural do bloco síncrono).
- Teste `HandleAsync_when_payment_requested_publisher_fails_does_not_mark_saga_nor_update_nor_propagate`
  (`RequestReservationCommandHandlerTests.cs`) exercita exatamente isso: mock lança
  `InvalidOperationException`, depois assere `reservation.Status == Solicitada` (retornou normalmente),
  `Saga.PaymentRequestSentAt == null`, `repository.UpdateAsync` `Times.Never`. Cobertura real via
  assertions, não apenas nome do teste.
- A `Reservation` não é desfeita — não há nenhum rollback no bloco, e o teste confirma que o handler
  retorna a entidade normalmente.

### 2. Sucesso

`PaymentRequestedRmqPublisher.cs`: payload `PaymentRequestedEvent(correlationId, correlationId,
reservation.TotalAmount.ToString("F2", CultureInfo.InvariantCulture), reservation.Currency,
DateTimeOffset.UtcNow)`, com `correlationId = reservation.Saga.CorrelationId`. Headers
`x-correlation-id`/`x-causation-id` = mesmo `correlationId.ToString()`. `cloudEventType` =
`com.localizestay.booking.payment_requested.v1`, exchange/routing key `booking.payment_requested` —
tudo fiel ao `api-contract.yaml`.

- Teste unitário `HandleAsync_when_payment_requested_publisher_succeeds_marks_saga_and_updates_once`
  confirma `PaymentRequestSentAt != null`, `PublishAsync` `Times.Once`, `UpdateAsync` `Times.Once`.
- Teste de integração `PaymentRequestedPublishingTests` liga fila real à exchange
  `booking.payment_requested`, lê o envelope CloudEvents da mensagem e confere:
  `EnvelopeType == cloudEventType` esperado; `HeaderCorrelationId`/`HeaderCausationId` e
  `DataCorrelationId`/`DataCausationId` == `Saga.CorrelationId` lido via `SqlQueryRaw` direto na tabela
  `booking.reservation_sagas`; `DataTotalAmount` comparado (string) ao `totalAmount` retornado pela
  resposta HTTP; `DataCurrency == "BRL"`; e finalmente `payment_request_sent_at IS NOT NULL` via query
  direta. Todas as assertions batem com o `gate_expected_result` da task, não apenas os nomes.

### 3. Migration aditiva

`20260913011832_AddPaymentRequestSentAtToReservationSagas.cs`: `Up` só adiciona
`payment_request_sent_at timestamptz null` em `booking.reservation_sagas`; `Down` remove a mesma
coluna; nenhuma outra tabela/coluna tocada. `BookingDbContextModelSnapshot.cs` reflete a mesma
propriedade (`PaymentRequestSentAt` → `timestamptz`/`payment_request_sent_at`), coerente com
`ReservationSagaConfiguration.cs` (`HasColumnName("payment_request_sent_at").HasColumnType("timestamptz")`).
Migration foi gerada via `dotnet ef migrations add` (Designer.cs presente, padrão do projeto).

Observação não bloqueante: a coluna existente `CreatedAt` na mesma tabela usa o tipo default do
Npgsql ("timestamp with time zone", sem `HasColumnType` explícito), enquanto a nova coluna usa
`timestamptz` explícito. São aliases equivalentes no Postgres — não é um bug, apenas uma pequena
inconsistência estilística entre colunas irmãs da mesma entidade. Ver recomendações.

### 4. Cobertura de teste vs. `gate_expected_result`

Todos os elementos do `gate_expected_result` da task foram lidos nas assertions (não apenas nos nomes
dos testes) e confirmados:
- 3 classes de teste existem e cada seletor do `gate_command` encontrou testes (3 + 13 + 1 = 17,
  nenhum "0 tests found").
- `RequestReservationCommandHandlerTests` mantém os cenários de F01 sem alteração (diff é
  estritamente aditivo — `git diff --stat` mostra `41 insertions(+)`, `0 deletions`) e adiciona os 2
  cenários exigidos (sucesso publica+registra; falha não registra e não propaga).
- `PaymentRequestedPublishingTests` confirma `correlationId`/`causationId` = `Saga.CorrelationId`,
  `totalAmount` string 2 casas idêntico ao retornado pela API, `currency="BRL"`,
  `cloudEventType="com.localizestay.booking.payment_requested.v1"`, e
  `payment_request_sent_at` preenchido — todos os itens do `gate_expected_result` literalmente
  cobertos.
- `ReservationSagaTests` cobre o estado inicial (`null`), a gravação do instante exato recebido, e a
  não-alteração de `SagaState`.

### 5. Escopo

Diff estritamente limitado aos arquivos listados na task (`Arquivos Envolvidos`). Nenhuma classe de
`Domain/Reservations/**` referencia `HttpClient`/`DbContext`/`Rmq.CloudEvents` (grep vazio — regra do
baseline respeitada). Único arquivo fora do código de produção/teste é `1_task.md` (mudança de
`status: pending` → `status: validating`, estado do orquestrador, não da implementação) e
`flow-state.json` (novo, também estado do orquestrador) — nenhum dos dois é código revisável desta
task. Nenhuma complexidade desnecessária introduzida: o publisher replica exatamente o padrão de
`ReservationRequestedRmqPublisher`, sem abstrações novas.

## Achados

Nenhum bloqueante.

- **[Recomendação / baixa severidade]** `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/ReservationSagaConfiguration.cs:19-21`
  e `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Migrations/BookingDbContextModelSnapshot.cs`
  (nova propriedade `PaymentRequestSentAt`): usa `HasColumnType("timestamptz")` explícito, enquanto a
  coluna irmã `CreatedAt` na mesma entidade não declara `HasColumnType` (fica com o default do Npgsql,
  "timestamp with time zone" no snapshot). Equivalente em Postgres, não afeta comportamento nem gate —
  apenas inconsistência estilística entre colunas da mesma tabela. Não bloqueia.

## Veredito

**VALIDAÇÃO APROVADA** (0 bloqueantes, 1 recomendação de baixa severidade).

Confirmo que o `gate_expected_result` da task 1.0 foi de fato coberto pelos testes (assertions lidas,
não apenas nomes), o gate comportamental foi executado de forma independente nesta revisão (exit 0,
17 testes verdes nas 3 classes exigidas), e o código revisado corresponde exatamente aos requisitos
normativos da task (trecho do handler, payload do publisher, migration aditiva, DI/wiring).
