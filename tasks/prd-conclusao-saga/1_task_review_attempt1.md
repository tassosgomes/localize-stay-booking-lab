# Revisão focused — Task 1.0, attempt 1 (pós-implementação)

## Resultado

**FOCUSED VALIDATION APROVADA** (0 bloqueantes, 1 recomendação não-bloqueante)

- PRD_DIR: `/home/tsgomes/github-tassosgomes/localize-stay-booking-lab-prd-conclusao-saga/tasks/prd-conclusao-saga`
- Worktree: `/home/tsgomes/github-tassosgomes/localize-stay-booking-lab-prd-conclusao-saga`
- Branch: `feature/prd-conclusao-saga`
- Task: `1.0` (behavioral, V-01) — attempt 1
- Modo: `focused`
- HEAD revisado: `a77631ead4e549cb69fee805c694b05efd1a94af` (estável antes/depois; nenhuma mudança do validator no código)
- Nota: `1_task_review.md` existente é revisão pré-implementação reprovada (B-01); preservado. Esta é a revisão da tentativa atual em arquivo novo.

## Gate focalizado (executado antes da semântica, worker fresco)

Comando (de `1_task.md:17`):

```text
scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationSagaTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ConfirmReservationCommandHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.CancelReservationCommandHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Messaging.PaymentAuthorizedConsumerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Messaging.PaymentRejectedConsumerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentAuthorizedConsumptionTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentRejectedConsumptionTests"
```

Resultado: **exit 0 — `GATE: APROVADO`**

```text
GATE: APROVADO
format: dotnet format ok (28 arquivos)
build: dotnet build ok (0 Warning(s) 0 Error(s))
testes: ok (ReservationTests=27, ReservationSagaTests=5, ConfirmReservationCommandHandlerTests=5, CancelReservationCommandHandlerTests=5, PaymentAuthorizedConsumerTests=5, PaymentRejectedConsumerTests=5, PaymentAuthorizedConsumptionTests=3, PaymentRejectedConsumptionTests=3)
```

Nenhum filtro vazio (8/8 selecionaram testes). Veredito do implementer não reutilizado como prova.

## Revisão semântica (escopo focused: diff + untracked da task vs. specs aprovadas)

- **Domínio:** `Reservation.Confirm()`/`Cancel(reason)` só de `Solicitada`, delegam a `Saga.MarkAuthorized()`/`MarkRejected(reason)`; guarda privada lança `InvalidOperationException` (RN-11) — `Reservation.cs:123-144`; `ReservationSaga.cs:35-44` preserva `PaymentRequestSentAt` de F03.
- **Handlers/outcomes:** outcomes explícitos `Confirmed/Cancelled`, `AlreadyTerminal`, `NotCorrelatable`; sem publish nos dois últimos; `UpdateAsync` antes de publicar; `CancellationToken` propagado; catch `Exception when not OperationCanceledException`, loga best-effort sem rethrow/retry/Outbox — `ConfirmReservationCommandHandler.cs:14-62`, `CancelReservationCommandHandler.cs:13-61`.
- **Repositório:** `GetByCorrelationIdAsync` com `Include(Saga)` + `FirstOrDefaultAsync(Saga.CorrelationId)` — `ReservationRepository.cs:35-41` (nunca `SingleOrDefaultAsync`); `UpdateAsync` de F03 preservado.
- **Publishers CloudEvents:** exchanges/routing keys `booking.reservation_confirmed/cancelled`, types `com.localizestay.booking.reservation_confirmed.v1` / `...reservation_cancelled.v1`, payload mínimo em `camelCase` via `[JsonPropertyName]` (sem `totalAmount/currency/pricePerNight/guestsCount`), `confirmedAt/cancelledAt = UtcNow`, `cancellationReason` de `Saga`; headers `x-correlation-id`/`x-causation-id` = `Saga.CorrelationId` (auto-causação) — `ReservationConfirmedRmqPublisher.cs:29-76`, `ReservationCancelledRmqPublisher.cs:29-78`.
- **Consumers defensivos + DP-01:** messages desserializam `CorrelationId: string?` + timestamp opcional; `Guid.TryParse`, sem dispatch/NACK em ausente/vazio/inválido; adapter fino só despacha command; rejeição usa texto fixo `"Pagamento rejeitado pela simulação de Payment."` (confere com `api-contract.md:143`, `api-contract.yaml:572,587`, `techspec.md:422`), nunca motivo técnico — `PaymentAuthorizedConsumer.cs:14-32`, `PaymentRejectedConsumer.cs:14-34`.
- **Wiring:** `ApplicationExtensions.cs:15-16` registra os dois `ICommandHandler<,>`; `MessagingExtensions.cs:78-126` registra exchanges finais + consumo (`payment.payment_authorized/rejected`), os dois publishers e `AddRmqTopicConsumer` com exchange/fila (`booking.payment_authorized/rejected`)/binding 1:1; sem `BackgroundService`/`IServiceScopeFactory` manual.
- **Testes RF-01/RF-02/RF-03/RN-11:** unit cobrem transições/guards (`ReservationTests.cs:186-244`), `MarkAuthorized/Rejected` (`ReservationSagaTests.cs:53-73`), sucesso/update+publish 1x, não-correlacionável/terminal sem efeitos, publisher com exceção preserva estado sem rethrow (`Confirm/Cancel...HandlerTests.cs`), dispatch válido 1x e nulo/vazio/inválido sem dispatch + motivo fixo (`PaymentAuthorizedConsumerTests.cs`, `PaymentRejectedConsumerTests.cs`); integração com Postgres/RabbitMQ reais cobrem persistência terminal, `type`/`data`/headers, campos mínimos, duplicado/tardio/conflitante/não-correlacionável sem segunda mutação/publicação (`PaymentAuthorizedConsumptionTests.cs:54-128`, `PaymentRejectedConsumptionTests.cs:64-140`).
- **Restrições:** nenhuma migration nova, nenhum pacote/versão nova (`AwesomeAssertions` sem versão, usa central; `ProjectReference` ao Api é suporte aos testes de consumer, não pacote), nenhum endpoint/frontend/Outbox/dedup/DLQ manual/consumidor Catalog-Notification.

## Bloqueantes

Nenhum.

## Recomendações (não-bloqueantes, 1)

1. `LocalizeStay.Booking.UnitTests.csproj` adiciona `ProjectReference` ao Api além do `AwesomeAssertions` previsto na task — necessário aos testes de consumer e inócuo ao gate, mas a task só enumerava o `PackageReference`; considerar explicitar essa referência em planos futuros. Sem impacto nesta validação.

## Arquivos alterados pelo validator

- `tasks/prd-conclusao-saga/1_task_review_attempt1.md` — único arquivo criado; `1_task_review.md`, `flow-state.json`, `tasks.md`, `1_task.md`, código e commits intactos.
