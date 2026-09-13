# Revisão Full — PRD Solicitação de Pagamento (Booking F03)

- Modo: full
- full_attempt: 1
- base_ref: `4d1ab4cdc06ad9b02d25580855d9271ebf939d65`
- target_ref: idêntico ao base (branch rebaseada diretamente sobre o tip de `main`)
- validated_commit (HEAD revisado): `ab5bb57aea356784d5c3682b3125762a9c8d32cd`
- validated_tree: estável durante toda a revisão (HEAD e `git status --short` conferidos no início e no
  fim; único item pendente é `tasks/prd-solicitacao-pagamento/flow-state.json`, untracked, estado do
  orquestrador, fora do diff revisado)

## Gate

```
scripts/ai-flow/gate.sh --base=4d1ab4cdc06ad9b02d25580855d9271ebf939d65 --all-tests
```

Resultado: `GATE: APROVADO`
- arquivos alterados: 18 (.NET: 14, node: 0)
- `dotnet format`: ok (14 arquivos)
- `dotnet build`: ok em todas as soluções do monorepo (booking, catalog, payment, notification-worker,
  RegisterRabbitMq) — 0 warnings/0 errors; typecheck do frontend pulado (sem tsc local, sem arquivo
  frontend no diff)
- testes: ok (suíte .NET completa — `--all-tests`, não apenas o filtro da task)

Exit code 0. Gate amplo (full) executado nesta revisão, de forma independente do implementer/focused.

## Diff revisado

`git diff 4d1ab4cdc06ad9b02d25580855d9271ebf939d65..HEAD` — 17 arquivos, 764 inserções, 2 deleções, um
único commit (`ab5bb57`). Único commit desde a base; nada além dele na branch.

## Rastreabilidade ponta a ponta (RF-01 completo)

- **Cenário de sucesso**: `RequestReservationCommandHandler` (bloco novo, após o já existente de
  `reservation_requested`) chama `IPaymentRequestedPublisher.PublishAsync` →
  `PaymentRequestedRmqPublisher` publica na exchange/routing key `booking.payment_requested` (topic,
  durable), payload `correlationId`/`causationId` = `reservation.Saga.CorrelationId`, `totalAmount`
  string F2, `currency`, `requestedAt`, headers `x-correlation-id`/`x-causation-id`, `cloudEventType
  com.localizestay.booking.payment_requested.v1` — fiel a `api-contract.yaml`. Em seguida
  `reservation.Saga.MarkPaymentRequestSent(DateTime.UtcNow)` + `IReservationRepository.UpdateAsync`.
  Coberto por `RequestReservationCommandHandlerTests.HandleAsync_when_payment_requested_publisher_succeeds_marks_saga_and_updates_once`
  e por `PaymentRequestedPublishingTests` (integração real, RabbitMQ + Postgres via Testcontainers).
- **Cenário de falha best-effort**: publisher lança exceção → capturada em
  `catch (Exception ex) when (ex is not OperationCanceledException)`, apenas logada (`LogError`), sem
  propagar; `MarkPaymentRequestSent`/`UpdateAsync` não executam (estão depois da chamada que lançou, no
  mesmo bloco `try`); `Reservation` retorna normalmente em `Solicitada`. Coberto por
  `HandleAsync_when_payment_requested_publisher_fails_does_not_mark_saga_nor_update_nor_propagate`
  (assere `Status == Solicitada`, `PaymentRequestSentAt == null`, `UpdateAsync` `Times.Never`) — bate
  com DP-02/RF-01 (segundo critério de aceite) e com a limitação conhecida da Fase 0 (sem retry/Outbox).
- **Reserva rejeitada por F01**: nenhum código novo desta feature é alcançado (o bloco novo só roda após
  o `AddAsync` bem-sucedido) — garantido pela própria estrutura do handler; os testes de rejeição já
  existentes de F01 (`RequestReservationCommandHandlerTests`) continuam verdes sem alteração
  (diff estritamente aditivo, 0 deleções nesse arquivo).
- **Isolamento de domínio**: payload publicado carrega somente `correlationId`, `causationId`,
  `totalAmount`, `currency`, `requestedAt` — nenhum dado de Accommodation/Guest, consistente com a
  Restrição Técnica do PRD e com `api-contract.yaml`.
- **`SagaState` não alterado**: `ReservationSaga.PaymentRequestSentAt` é atributo independente
  (`DateTime?`), sem nova transição de estado — confirmado em `ReservationSagaTests` (3 testes: null
  inicial, gravação do instante exato, `State` permanece `PaymentPending`) e coerente com o Termo
  Canônico do PRD.

Todos os 3 cenários do AC de RF-01 (sucesso, falha de publicação, reserva rejeitada) e a fidelidade de
valor/moeda (RN-06) estão implementados e testados, com asserções lidas (não apenas nomes de teste) —
reconfirmado nesta revisão full sobre o diff completo, coerente com o `1_task_review.md` (focused) já
aprovado.

## Confirmação de escopo

Diff limitado exatamente ao inventário de artefatos da TechSpec/task 1.0:
`ReservationSaga.cs`, `IPaymentRequestedPublisher.cs` (novo), `IReservationRepository.cs` (+UpdateAsync),
`RequestReservationCommandHandler.cs`, `PaymentRequestedRmqPublisher.cs` (novo), migration
`AddPaymentRequestSentAtToReservationSagas` (+ Designer + snapshot), `ReservationSagaConfiguration.cs`,
`ReservationRepository.cs`, `MessagingExtensions.cs`, `ReservationSagaTests.cs` (novo),
`RequestReservationCommandHandlerTests.cs` (+2 cenários), `PaymentRequestedPublishingTests.cs` (novo).
Os únicos arquivos fora de código de produção/teste são `1_task.md`/`tasks.md` (status do plano) e
`1_task_review.md` (evidência da revisão focused já aprovada) — estado operacional do fluxo, permitido
pelo guia full. Nenhuma mudança em Catalog, Payment, Notification, frontend ou infraestrutura
compartilhada além da nova exchange/coluna aditiva já previstas na TechSpec. Nenhum arquivo fora do
escopo do PRD foi incluído no commit único.

## Verificação de rebase / regressão

Base (`4d1ab4c`) e branch alvo coincidem (rebase feito diretamente sobre o tip local de `main`), sem
conflitos e mesmo diff — não há divergência a reconciliar. `--all-tests` do gate rodou a suíte .NET
completa (não só o filtro da task) contra o código pós-rebase e todas as soluções do monorepo
compilaram sem warnings/erros, incluindo os testes de F01 (`RequestReservationCommandHandlerTests`)
que permanecem verdes sem modificação — sem regressão observável em relação ao `main` atual.

## Achados

Nenhum bloqueante.

- **[Recomendação / baixa severidade]** `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/ReservationSagaConfiguration.cs:19-21`
  — `PaymentRequestSentAt` usa `HasColumnType("timestamptz")` explícito, enquanto a coluna irmã
  `CreatedAt` na mesma entidade não declara `HasColumnType` (default do Npgsql). Equivalente em
  Postgres, não afeta comportamento nem gate; apenas inconsistência estilística entre colunas da mesma
  tabela. Já registrada na revisão focused (`1_task_review.md`); reconfirmada aqui sem mudança. Não
  atribuída a nenhuma task futura deste plano (F03 está concluído) — fica como nota de estilo para quem
  tocar `ReservationSagaConfiguration.cs` novamente.

Nenhuma pressão de design concreta identificada que justifique comparar padrões (extensão pequena e
localizada de um handler já existente, decisão já fundamentada na TechSpec).

## Veredito

**FULL VALIDATION APROVADA** (0 bloqueantes, 1 recomendação de baixa severidade, reconfirmada da
revisão focused).

- Gate full (`--base` + `--all-tests`) executado de forma independente nesta revisão: exit 0, suíte
  .NET completa verde, todas as soluções do monorepo compilando.
- RF-01 completo (sucesso, falha best-effort, reserva rejeitada) rastreado ponta a ponta do PRD até o
  código e os testes, com asserções reais conferidas.
- Escopo confirmado: commit único (`ab5bb57`) contém apenas o que a task 1.0 descreve, mais estado
  operacional do fluxo (tasks.md/1_task.md/1_task_review.md).
- Rebase sobre `main` não introduziu regressão: base e target coincidem, sem conflitos, suíte completa
  verde.
- HEAD e árvore de trabalho estáveis durante toda a revisão: `ab5bb57aea356784d5c3682b3125762a9c8d32cd`.
