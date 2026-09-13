# TechSpec: Conclusão da Saga (Booking F04)

> **Modo de operação:** API-First (contrato AsyncAPI — sem endpoint HTTP novo)
> **PRD de origem:** `tasks/prd-conclusao-saga/prd.md`
> **API Contract:** `tasks/prd-conclusao-saga/api-contract.yaml` (AsyncAPI 3.1, v1.0.0, status "Aprovado")
> **Data:** 2026-09-12
> **Status:** Aprovado
> **Handoff:** approved — pode alimentar o Task Creator
>
> Aprovado pelo autor nesta revisão, incluindo as quatro decisões novas explicitamente confirmadas:
> (1) consumo via `AddRmqTopicConsumer<TMessage,THandler>` (`Rmq.CloudEvents`) em vez de um
> `BackgroundService` manual; (2) guarda de domínio via exceção em `Reservation.Confirm()`/`Cancel()`
> como rede de segurança, com a decisão de ignorar duplicado/tardio (RN-11) tomada no handler antes de
> chamar esses métodos; (3) `[JsonPropertyName]` explícito nos payloads publicados/consumidos, para não
> repetir a divergência `camelCase`(docs)/`PascalCase`(implementação real) já observada em
> `booking.reservation_requested` (F01); (4) `IReservationRepository.GetByCorrelationIdAsync` usa
> `FirstOrDefaultAsync`, não `SingleOrDefaultAsync`, por `correlation_id` não ter índice único no
> schema. Dependência bloqueante confirmada: o habilitador de F02 (`SagaState.Authorized/Rejected`,
> `ReservationSaga.CancellationReason`) precisa estar disponível no ambiente de implementação; F03
> (`IReservationRepository.UpdateAsync`) é reutilizada se disponível, mas não bloqueia.

---

## Resumo Executivo

Esta TechSpec fecha a Reservation Saga coreografada: Booking passa a consumir `payment.payment_authorized`
e `payment.payment_rejected` (publicados por Payment, ainda sem contrato próprio) e, para cada evento
correlacionado a uma saga que ainda aguarda resultado, transiciona a `Reservation` para o estado terminal
correspondente (`confirmada`/`cancelada`), registra o resultado na `ReservationSaga` e publica
`booking.reservation_confirmed`/`booking.reservation_cancelled`. A implementação estende exatamente o
mesmo desenho já usado por F01/F03 — CQRS nativo (comando + handler), porta de publicação + adapter
`Rmq.CloudEvents`, publish best-effort sem outbox — e reutiliza, sem redefinir, o vocabulário que a
TechSpec de F02 já preparou como habilitador (`SagaState.Authorized`/`Rejected`,
`ReservationSaga.CancellationReason`). O lado de consumo é novo neste serviço: em vez de um consumidor
manual como `DiagnosticPingConsumer` (fundação, V-03), esta TechSpec usa
`Rmq.CloudEvents.Extensions.AddRmqTopicConsumer<TMessage, THandler>` — recurso já disponível na mesma
biblioteca já adotada desde a fundação, que provê declaração de tópico/fila, unwrap de CloudEvents,
retry exponencial e DLQ automática sem nenhum código de baixo nível novo.

**Trade-off primário:** a verificação de estado atual da `Reservation` antes de transicionar (DP-02) é
suficiente para RN-11 (estados terminais monotônicos) nesta fase, mas não elimina uma janela teórica de
corrida entre duas entregas verdadeiramente concorrentes do mesmo evento antes que a primeira termine de
persistir — um dedup store/dedução por `eventId` (F06) resolveria isso; esta TechSpec aceita o risco
residual, documentado no PRD (Riscos e Mitigações) e replicado aqui em "Riscos Conhecidos", em troca de
não introduzir nenhuma peça de infraestrutura nova além do que a Fase 0 já decidiu não resolver agora.

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|-------|---------|------------------------|
| `dotnet-architecture` | `.claude/skills/dotnet-architecture` | Dois comandos CQRS novos (`ConfirmReservationCommand`/`CancelReservationCommand`), portas de publicação seguindo o padrão já usado por F01/F03; consumidor de evento como adapter "driving" fino em `Api/Messaging` que só invoca `IDispatcher` — mesmo papel arquitetural de um endpoint HTTP, análogo ao `ReservationEndpoints`/`DiagnosticsEndpoints` já existentes |
| `dotnet-dependency-config` | `.claude/skills/dotnet-dependency-config` | Reaproveita `Rmq.CloudEvents` (já configurado desde a fundação) via `AddRmqTopicConsumer<TMessage,THandler>` em vez de um `BackgroundService` manual; duas novas exchanges de publicação (`booking.reservation_confirmed`/`booking.reservation_cancelled`) registradas em `MessagingExtensions`, mesmo padrão de F03 |
| `dotnet-testing` | `.claude/skills/dotnet-testing` | Unitário para `Reservation.Confirm/Cancel`, `ReservationSaga.MarkAuthorized/MarkRejected`, os dois command handlers e os dois consumidores (mock de `IDispatcher`); integração ponta a ponta publicando um evento simulado de Payment contra um broker real (Testcontainers) e lendo o evento final publicado, mesmo padrão de `ReservationEndpointTests`/`ReservationPersistenceTests` |

Não foram lidas `dotnet-code-quality`, `dotnet-observability`, `dotnet-performance`, `restful-api` e
`design-patterns`: esta feature não introduz endpoint HTTP, não tem requisito de performance/observabilidade
além do logging estruturado já padrão da fundação, e a variação de comportamento (autorizar vs. rejeitar)
já é resolvida por dois comandos distintos — não há necessidade de um Design Pattern de variação de
algoritmo/estado adicional.

---

## Arquitetura do Sistema

### Visão Geral dos Componentes

- **`LocalizeStay.Booking.Domain` (estendido):**
  - `Reservation` ganha `Confirm()` e `Cancel(string cancellationReason)` — cada um só transiciona a
    partir de `Solicitada` (guarda de invariante; lança `InvalidOperationException` fora desse estado,
    como rede de segurança, já que o Application layer verifica o estado antes de chamar — DP-02) e
    delega a mutação da saga ao próprio `ReservationSaga`.
  - `ReservationSaga` ganha `MarkAuthorized()` e `MarkRejected(string cancellationReason)` — usam
    `SagaState.Authorized`/`Rejected` e `CancellationReason`, que já são artefatos previstos e
    aprovados pela TechSpec de F02 (`tasks/prd-consulta-reserva/techspec.md` §Habilitadores
    inevitáveis) como reutilizáveis por F04, não redefinidos aqui.
  - **Nenhuma coluna nova é necessária:** nem `Reservation` nem `ReservationSaga` precisam persistir
    o instante de confirmação/cancelamento — `confirmedAt`/`cancelledAt` do contrato são calculados no
    momento da publicação (`DateTimeOffset.UtcNow`), mesmo padrão já usado por F01/F03 para
    `requestedAt`, e F02 não expõe esse timestamp na consulta.
- **`LocalizeStay.Booking.Application` (estendido):**
  - Novas portas de publicação `IReservationConfirmedPublisher`/`IReservationCancelledPublisher`
    (mesmo formato de `IReservationRequestedPublisher`/`IPaymentRequestedPublisher`).
  - `IReservationRepository` ganha `GetByCorrelationIdAsync(Guid correlationId, CancellationToken)`
    (novo, localiza a saga a partir do `correlationId` do evento de Payment — não pelo `Id` da
    Reservation, mesmo os dois compartilhando valor hoje, ver "Modelos de Dados"). Reaproveita
    `UpdateAsync(Reservation, CancellationToken)` já especificado por F03
    (`tasks/prd-solicitacao-pagamento/techspec.md`); se F03 ainda não estiver implementada quando F04
    for construída, esta TechSpec adiciona `UpdateAsync` com a mesma assinatura já aprovada por F03,
    em vez de desenhar um método equivalente próprio.
  - `ConfirmReservationCommand`/`ConfirmReservationCommandHandler` e
    `CancelReservationCommand`/`CancelReservationCommandHandler` — cada handler busca a saga por
    `correlationId`, decide entre confirmar/cancelar, ignorar (não correlacionável) ou ignorar (já
    terminal), persiste via `UpdateAsync` e publica o evento final best-effort (mesmo padrão try/catch
    de log de F01/F03, sem outbox — RF-03/DP-04).
- **`LocalizeStay.Booking.Infra` (estendido):** `ReservationConfirmedRmqPublisher`/
  `ReservationCancelledRmqPublisher` (adaptam `IRmqPublisher` do `Rmq.CloudEvents`, mesmo padrão de
  `ReservationRequestedRmqPublisher`); `ReservationRepository.GetByCorrelationIdAsync` (e
  `UpdateAsync`, se ainda não existir); mapeamento EF de `CancellationReason`, se ainda não existir
  (reutilizando a especificação de F02).
- **`LocalizeStay.Booking.Api` (estendido):** dois consumidores novos em `Api/Messaging` —
  `PaymentAuthorizedConsumer`/`PaymentRejectedConsumer`, registrados via
  `services.AddRmqTopicConsumer<TMessage, THandler>(...)` — cada um só desserializa o envelope, resolve
  `correlationId` (tratamento defensivo — Questão em Aberto do PRD) e invoca `IDispatcher.SendAsync`
  com o comando correspondente; nenhuma regra de negócio nesta camada, mesmo papel de um endpoint
  Minimal API. `MessagingExtensions` registra as duas novas exchanges de publicação e os dois
  consumidores de tópico.
- **Nenhum componente de Payment ou Catalog é criado ou modificado aqui** — o lado de publicação de
  Payment e o lado de consumo de Catalog/Notification são decisão das próprias features desses domínios
  (Questões em Aberto do `api-contract.md`).

### Diagrama de Componentes

```text
Payment (fora do escopo) ──▶ exchange payment.payment_authorized (topic)
                                          │ routing key payment.payment_authorized
                                          ▼
                          fila booking.payment_authorized (Booking, AddRmqTopicConsumer)
                                          │
                                          ▼
                    PaymentAuthorizedConsumer (Api/Messaging) — adapter fino
                          │ dispatcher.SendAsync(ConfirmReservationCommand)
                          ▼
        ConfirmReservationCommandHandler (Application)
          1. repository.GetByCorrelationIdAsync(correlationId)
             ├─ null                      → log + Outcome.NotCorrelatable (DP-03)      [fim]
             ├─ Status != Solicitada      → log + Outcome.AlreadyTerminal (DP-02/RN-11) [fim]
             └─ Status == Solicitada:
                2. reservation.Confirm()  → Status=Confirmada, Saga.State=Authorized (RN-07/RN-10)
                3. repository.UpdateAsync(reservation)
                4. publisher.PublishAsync(reservation)
                   ├─ sucesso → log
                   └─ falha   → log de erro, sem exceção, sem retry (RF-03/DP-04)
                                              │
                                              ▼
                          exchange booking.reservation_confirmed (topic, durable)
                                    ← consumidores: Catalog, Notification (fora do escopo)

(payment.payment_rejected → PaymentRejectedConsumer → CancelReservationCommandHandler é o caminho
 simétrico: Status=Cancelada, Saga.State=Rejected + CancellationReason, publica
 booking.reservation_cancelled — RN-08/RN-10, DP-01)
```

---

## Estratégia de Entrega Incremental

### Mapa de Fatias Verticais

O PRD marca a feature como única e indivisível para efeito de entrega (Plano de Rollout Faseado): a
confirmação, o cancelamento e a proteção de estados terminais monotônicos (RN-11) só demonstram o
objetivo de estudo (fechamento da saga coreografada) juntos — mesma natureza de decisão já registrada
por F03 para sua própria fatia única. Por isso há uma única fatia vertical, cobrindo os dois caminhos
(autorização/rejeição) e os três cenários de AC de cada um (sucesso, duplicado/tardio, não
correlacionável) mais RF-03 (isolamento de falha de publicação).

| Slice | Comportamento observável | RF/RN cobertos | Entrada → processamento → saída | Artefatos principais | Evidência / checkpoint | Bloqueado por |
|-------|--------------------------|-----------------|----------------------------------|-----------------------|--------------------------|----------------|
| V-01 | Toda `Reservation`/`ReservationSaga` aguardando resultado atinge exatamente o estado terminal correspondente ao evento de pagamento correlacionado recebido (`confirmada`+`booking.reservation_confirmed` ou `cancelada`+`booking.reservation_cancelled`, com motivo de negócio); um evento duplicado/tardio para saga já terminal ou não correlacionável não altera nada e é logado, sem interromper outras mensagens; uma falha de publicação do evento final não desfaz a transição já persistida | RF-01, RF-02, RF-03, RN-07, RN-08, RN-09, RN-10, RN-11 | `payment.payment_authorized`/`payment.payment_rejected` (fila própria) → `PaymentAuthorizedConsumer`/`PaymentRejectedConsumer` → `IDispatcher.SendAsync` → `ConfirmReservationCommandHandler`/`CancelReservationCommandHandler` → `Reservation.Confirm/Cancel` → `IReservationRepository.UpdateAsync` → `IReservationConfirmedPublisher`/`IReservationCancelledPublisher` → `booking.reservation_confirmed`/`booking.reservation_cancelled` | `Domain/Reservations/{Reservation,ReservationSaga}.cs` (estendidos); `Application/Reservations/{IReservationConfirmedPublisher,IReservationCancelledPublisher,ConfirmReservationCommand(Handler),CancelReservationCommand(Handler),IReservationRepository (estendido)}.cs`; `Infra/Messaging/{ReservationConfirmedRmqPublisher,ReservationCancelledRmqPublisher}.cs`; `Infra/Persistence/ReservationRepository.cs` (estendido); `Api/Messaging/{PaymentAuthorizedTopology,PaymentAuthorizedMessage,PaymentAuthorizedConsumer,PaymentRejectedTopology,PaymentRejectedMessage,PaymentRejectedConsumer}.cs`; `Api/Extensions/MessagingExtensions.cs` (estendido) | `dotnet test --filter FullyQualifiedName~Booking.UnitTests.Reservations` cobrindo os cenários novos de `Reservation`/`ReservationSaga`/os 2 handlers/os 2 consumidores; `dotnet test --filter FullyQualifiedName~Booking.IntegrationTests.Reservations.PaymentAuthorizedConsumptionTests` e `...PaymentRejectedConsumptionTests` publicando um evento simulado de Payment contra RabbitMQ real (Testcontainers) e lendo o evento final publicado + o estado persistido | F02 (habilitador `SagaState.Authorized/Rejected` + `CancellationReason`) disponível no ambiente de implementação — ver "Dependências Técnicas Bloqueantes" |

### Habilitadores inevitáveis

Nenhum habilitador novo nesta TechSpec. O único habilitador de vocabulário que esta feature precisaria
(`SagaState.Authorized`/`Rejected`, `ReservationSaga.CancellationReason`) já foi entregue como
habilitador da TechSpec de F02 (`tasks/prd-consulta-reserva/techspec.md` §Habilitadores inevitáveis,
task 1.0, já com checkpoint `enabling/static gate APROVADO` na branch `feature/prd-consulta-reserva`) —
F04 reutiliza, não reintroduz.

---

## Design de Implementação

### Interfaces Principais

```csharp
// Application — novas portas de publicação (mesmo padrão de
// IReservationRequestedPublisher/IPaymentRequestedPublisher)
public interface IReservationConfirmedPublisher
{
    Task PublishAsync(Reservation reservation, CancellationToken cancellationToken);
}

public interface IReservationCancelledPublisher
{
    Task PublishAsync(Reservation reservation, CancellationToken cancellationToken);
}

// Application — extensão da porta de persistência já existente
public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);

    // F02 (reutilizado, não redefinido aqui)
    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Novo nesta TechSpec: localiza pela Saga.CorrelationId (evento de Payment),
    // não pelo Id da Reservation — conceitos distintos mesmo com valores hoje iguais.
    Task<Reservation?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken);

    // F03 (reutilizado se já existir; adicionado aqui com a mesma assinatura caso contrário)
    Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken);
}
```

```csharp
// Domain — Reservation (estendido). Guarda de invariante: Application já verifica
// Status == Solicitada antes de chamar (DP-02); a exceção é rede de segurança, não
// o caminho normal de "ignorar duplicado/tardio" (isso o handler resolve sem chamar
// estes métodos — ver Considerações Técnicas).
public sealed class Reservation
{
    // ... membros existentes (F01) inalterados ...

    public void Confirm()
    {
        EnsurePending();
        Status = ReservationStatus.Confirmada;
        Saga.MarkAuthorized();
    }

    public void Cancel(string cancellationReason)
    {
        EnsurePending();
        Status = ReservationStatus.Cancelada;
        Saga.MarkRejected(cancellationReason);
    }

    private void EnsurePending()
    {
        if (Status != ReservationStatus.Solicitada)
        {
            throw new InvalidOperationException(
                $"Reservation {Id} não pode transicionar a partir do estado {Status} (RN-11).");
        }
    }
}

// Domain — ReservationSaga (F02 já adiciona CancellationReason; estendido aqui)
public sealed class ReservationSaga
{
    // ... membros existentes (F01/F02) inalterados ...

    public void MarkAuthorized()
    {
        State = SagaState.Authorized;
    }

    public void MarkRejected(string cancellationReason)
    {
        State = SagaState.Rejected;
        CancellationReason = cancellationReason;
    }
}
```

```csharp
// Application — ConfirmReservationCommandHandler (CancelReservationCommandHandler é
// o espelho exato, com Reason e Cancel/Rejected/Cancelled no lugar de
// Confirm/Authorized/Confirmed)
public sealed record ConfirmReservationCommand(Guid CorrelationId) : ICommand<ConfirmReservationOutcome>;

public enum ConfirmReservationOutcome { Confirmed, AlreadyTerminal, NotCorrelatable }

public sealed class ConfirmReservationCommandHandler(
    IReservationRepository repository,
    IReservationConfirmedPublisher publisher,
    ILogger<ConfirmReservationCommandHandler> logger)
    : ICommandHandler<ConfirmReservationCommand, ConfirmReservationOutcome>
{
    public async Task<ConfirmReservationOutcome> HandleAsync(
        ConfirmReservationCommand command, CancellationToken cancellationToken)
    {
        var reservation = await repository
            .GetByCorrelationIdAsync(command.CorrelationId, cancellationToken)
            .ConfigureAwait(false);

        if (reservation is null)
        {
            // DP-03: evento não correlacionável — ignorado para fins de negócio, logado.
            logger.LogWarning(
                "payment.payment_authorized não correlacionável: nenhuma saga para " +
                "correlationId={CorrelationId}", command.CorrelationId);
            return ConfirmReservationOutcome.NotCorrelatable;
        }

        if (reservation.Status != ReservationStatus.Solicitada)
        {
            // DP-02/RN-11: saga já terminal — ignorado, sem nova transição/publicação.
            logger.LogWarning(
                "payment.payment_authorized tardio/duplicado ignorado: Reservation {ReservationId} " +
                "já {Status} (correlationId={CorrelationId})",
                reservation.Id, reservation.Status, command.CorrelationId);
            return ConfirmReservationOutcome.AlreadyTerminal;
        }

        reservation.Confirm();
        await repository.UpdateAsync(reservation, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Reservation {ReservationId} confirmada (correlationId={CorrelationId})",
            reservation.Id, command.CorrelationId);

        try
        {
            await publisher.PublishAsync(reservation, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Evento booking.reservation_confirmed publicado para a Reservation {ReservationId}",
                reservation.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // RF-03/DP-04: falha best-effort não desfaz a transição já persistida.
            logger.LogError(
                ex,
                "Falha best-effort ao publicar booking.reservation_confirmed da Reservation " +
                "{ReservationId}; estado terminal já persistido",
                reservation.Id);
        }

        return ConfirmReservationOutcome.Confirmed;
    }
}
```

```csharp
// Api/Messaging — adapter fino (mesma responsabilidade de um endpoint), sem
// nenhuma regra de negócio. PaymentRejectedConsumer é o espelho, com a constante
// de motivo de negócio (DP-01) e CancelReservationCommand.
public sealed record PaymentAuthorizedMessage(
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("authorizedAt")] DateTimeOffset? AuthorizedAt);

public sealed class PaymentAuthorizedConsumer(
    IDispatcher dispatcher, ILogger<PaymentAuthorizedConsumer> logger)
    : IRmqMessageHandler<PaymentAuthorizedMessage>
{
    public async Task HandleAsync(
        PaymentAuthorizedMessage message, MessageContext context, CancellationToken cancellationToken)
    {
        // Tratamento defensivo do payload (Questão em Aberto do PRD): qualquer
        // campo além de correlationId é opaco; um correlationId ausente/inválido
        // é tratado como não correlacionável, não como erro técnico.
        if (!Guid.TryParse(message.CorrelationId, out var correlationId))
        {
            logger.LogWarning(
                "payment.payment_authorized com correlationId ausente/inválido na fila {Queue}: " +
                "ignorado (DP-03)", context.QueueName);
            return;
        }

        var outcome = await dispatcher
            .SendAsync(new ConfirmReservationCommand(correlationId), cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "payment.payment_authorized consumido (correlationId={CorrelationId}, outcome={Outcome})",
            correlationId, outcome);
    }
}
```

### Modelos de Dados

**Mapeamento Entidade do Domain Doc → Modelo Técnico:**

| Entidade (`domains/booking/domain.md` §3) | Modelo Técnico | Local |
|---|---|---|
| Reservation — estado final | `Reservation.Status` (`Confirmada`/`Cancelada`, já existentes em `ReservationStatus` desde F01, sem uso até esta feature) | `Domain/Reservations/Reservation.cs` → coluna `booking.reservations.status` |
| Reservation Saga — resultado recebido | `ReservationSaga.State` (`Authorized`/`Rejected`, adicionados por F02) | `Domain/Reservations/SagaState.cs` → coluna `booking.reservation_sagas.state` |
| Reservation Saga — motivo de cancelamento | `ReservationSaga.CancellationReason` (adicionado por F02) | `Domain/Reservations/ReservationSaga.cs` → coluna `booking.reservation_sagas.cancellation_reason` |

**Nenhuma migration nova é necessária nesta TechSpec** — `booking.reservations.status` já aceita
`confirmada`/`cancelada` desde F01 (conversão de enum já mapeada); `booking.reservation_sagas.state`
(`varchar(32)`) e `.cancellation_reason` (`varchar(500)`) já foram dimensionados e adicionados pela
migration habilitadora de F02. Isso pressupõe que essa migration já rodou no ambiente de implementação
(ver "Dependências Técnicas Bloqueantes") — se não tiver rodado, ela deve ser aplicada como parte da
disponibilização de F02, não recriada por F04.

**`Reservation.Id` vs. `ReservationSaga.CorrelationId`:** o Domain Doc modela os dois como conceitos
distintos (`domains/booking/domain.md` §3) — a implementação atual de F01 atribui o mesmo valor a
ambos na criação (`Reservation.Create`), mas `GetByCorrelationIdAsync` consulta explicitamente por
`Saga.CorrelationId`, não por `Reservation.Id`, para não depender dessa coincidência de implementação
como se fosse uma garantia de domínio.

### Endpoints de API

Não aplicável — esta feature não introduz nem modifica nenhum endpoint HTTP (Restrições Técnicas do
PRD). O contrato é inteiramente AsyncAPI.

**Mapeamento de implementação do contrato (AsyncAPI):**

| Operação do contrato | Caminho de Implementação |
|-------------|--------------------------|
| `consumePaymentAuthorized` (`tasks/prd-conclusao-saga/api-contract.yaml`) | Fila `booking.payment_authorized` → `PaymentAuthorizedConsumer` (`Api/Messaging`) → `IDispatcher` → `ConfirmReservationCommandHandler` |
| `consumePaymentRejected` | Fila `booking.payment_rejected` → `PaymentRejectedConsumer` (`Api/Messaging`) → `IDispatcher` → `CancelReservationCommandHandler` |
| `publishReservationConfirmed` | `ConfirmReservationCommandHandler` → `IReservationConfirmedPublisher` → `ReservationConfirmedRmqPublisher` (`Infra/Messaging`) → exchange `booking.reservation_confirmed` |
| `publishReservationCancelled` | `CancelReservationCommandHandler` → `IReservationCancelledPublisher` → `ReservationCancelledRmqPublisher` (`Infra/Messaging`) → exchange `booking.reservation_cancelled` |

**Mapeamento de payload do contrato → publisher (eventos publicados):**

| Campo do contrato | Origem no código |
|---|---|
| `correlationId` | `reservation.Saga.CorrelationId` |
| `causationId` | `reservation.Saga.CorrelationId` — mesma convenção de auto-causação já usada quando não há um identificador de evento distinto do lado causador (`payment.payment_authorized`/`payment.payment_rejected` ainda não definem um; ver `api-contract.yaml` premissa "Causation") |
| `reservationId` | `reservation.Id` |
| `accommodationId` | `reservation.AccommodationId` |
| `guestReference` | `reservation.GuestReference` |
| `checkIn` / `checkOut` | `reservation.CheckIn` / `reservation.CheckOut` |
| `confirmedAt` / `cancelledAt` | `DateTimeOffset.UtcNow` no momento da publicação (não persistido — ver "Modelos de Dados") |
| `cancellationReason` (só cancelamento) | `reservation.Saga.CancellationReason` (definido por `Cancel(reason)` antes da publicação) |

**Mapeamento de payload do contrato → consumidor (eventos consumidos, schema provisório):**

| Campo do contrato | Uso no código |
|---|---|
| `correlationId` | `PaymentAuthorizedMessage.CorrelationId`/`PaymentRejectedMessage.CorrelationId` → `Guid.TryParse` → `ConfirmReservationCommand`/`CancelReservationCommand` |
| `authorizedAt` / `rejectedAt` | Desserializados mas não utilizados na decisão (informativos, per contrato) |
| Qualquer outro campo | Ignorado (tratamento defensivo — Questão em Aberto do PRD/contrato) |

**Motivo de cancelamento (DP-01):** `PaymentRejectedConsumer` define
`private const string RejectionReason = "Pagamento rejeitado pela simulação de Payment.";` — mesmo
texto já usado como exemplo em `tasks/prd-consulta-reserva/api-contract.md` — e o passa como `Reason`
do `CancelReservationCommand`; o payload de `payment.payment_rejected` nunca é lido para compor esse
texto, mesmo que Payment publique algum campo técnico de motivo no futuro.

Nenhuma exceção de domínio nova mapeada para HTTP — esta feature não tem caminho de rejeição observável
via API (não há chamador síncrono esperando resposta; a única "rejeição" observável é o log de
DP-02/DP-03).

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Fatia | Tipo | Skills Aplicáveis | Descrição |
|---------|-------|------|-------------------|-----------|
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationConfirmedPublisher.cs` | V-01 | Port | `dotnet-architecture` | Porta de publicação de `booking.reservation_confirmed` |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationCancelledPublisher.cs` | V-01 | Port | `dotnet-architecture` | Porta de publicação de `booking.reservation_cancelled` |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/ConfirmReservationCommand.cs` | V-01 | Command | `dotnet-architecture` | `ConfirmReservationCommand(Guid CorrelationId)` + `enum ConfirmReservationOutcome` |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/ConfirmReservationCommandHandler.cs` | V-01 | Handler | `dotnet-architecture` | Orquestra RF-01 (busca por correlação, guarda RN-11, transição, persistência, publicação best-effort) |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/CancelReservationCommand.cs` | V-01 | Command | `dotnet-architecture` | `CancelReservationCommand(Guid CorrelationId, string Reason)` + `enum CancelReservationOutcome` |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/CancelReservationCommandHandler.cs` | V-01 | Handler | `dotnet-architecture` | Orquestra RF-02 (espelho do handler de confirmação) |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/ReservationConfirmedRmqPublisher.cs` | V-01 | Infra | `dotnet-dependency-config` | Implementa a porta via `Rmq.CloudEvents`; `ReservationConfirmedTopology` (Exchange/RoutingKey=`booking.reservation_confirmed`, CloudEventType=`com.localizestay.booking.reservation_confirmed.v1`); payload com `[JsonPropertyName]` explícito para honrar o `camelCase` do contrato |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/ReservationCancelledRmqPublisher.cs` | V-01 | Infra | `dotnet-dependency-config` | Idem, exchange/routing key/CloudEventType `booking.reservation_cancelled` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentAuthorizedTopology.cs` | V-01 | Infra | `dotnet-dependency-config` | Consts: Exchange=`payment.payment_authorized`, Queue=`booking.payment_authorized`, RoutingKey=`payment.payment_authorized` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentAuthorizedMessage.cs` | V-01 | DTO | `dotnet-dependency-config` | Record de desserialização defensiva (`CorrelationId`, `AuthorizedAt?`), `[JsonPropertyName]` explícito |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentAuthorizedConsumer.cs` | V-01 | Infra (driving adapter) | `dotnet-architecture`, `dotnet-dependency-config` | `IRmqMessageHandler<PaymentAuthorizedMessage>`; sem regra de negócio, só invoca `IDispatcher` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentRejectedTopology.cs` | V-01 | Infra | `dotnet-dependency-config` | Consts: Exchange=`payment.payment_rejected`, Queue=`booking.payment_rejected`, RoutingKey=`payment.payment_rejected` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentRejectedMessage.cs` | V-01 | DTO | `dotnet-dependency-config` | Record de desserialização defensiva (`CorrelationId`, `RejectedAt?`) |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Messaging/PaymentRejectedConsumer.cs` | V-01 | Infra (driving adapter) | `dotnet-architecture`, `dotnet-dependency-config` | `IRmqMessageHandler<PaymentRejectedMessage>`; define o `RejectionReason` de negócio (DP-01) |
| `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/ConfirmReservationCommandHandlerTests.cs` | V-01 | Test | `dotnet-testing` | 4 cenários (confirmado, não correlacionável, já terminal, falha de publicação best-effort) |
| `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/CancelReservationCommandHandlerTests.cs` | V-01 | Test | `dotnet-testing` | Espelho, + assert do `cancellationReason` persistido |
| `services/booking/tests/LocalizeStay.Booking.UnitTests/Messaging/PaymentAuthorizedConsumerTests.cs` | V-01 | Test | `dotnet-testing` | `correlationId` válido → despacha comando; ausente/inválido → não despacha (mock de `IDispatcher`) |
| `services/booking/tests/LocalizeStay.Booking.UnitTests/Messaging/PaymentRejectedConsumerTests.cs` | V-01 | Test | `dotnet-testing` | Espelho |
| `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/PaymentAuthorizedConsumptionTests.cs` | V-01 | Test | `dotnet-testing` | Publica `payment.payment_authorized` simulado (Testcontainers RabbitMQ) e confere confirmação persistida + `booking.reservation_confirmed` publicado; cenários de duplicado/tardio e não correlacionável |
| `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/PaymentRejectedConsumptionTests.cs` | V-01 | Test | `dotnet-testing` | Espelho para `payment.payment_rejected`/`booking.reservation_cancelled`, incluindo `cancellationReason` |

**Se a implementação de F02 (habilitador) ainda não estiver disponível** quando F04 for construída,
criar também (com a mesma especificação já aprovada em `tasks/prd-consulta-reserva/techspec.md`, não
redesenhada aqui): `tests/.../ReservationSagaTests.cs` (cenários de `MarkAuthorized`/`MarkRejected`
desta TechSpec) e a migration `AddSagaCancellationReason` (ou nome equivalente já definido por F02).

### Arquivos a Modificar

| Caminho | Fatia | Skills Aplicáveis | Alteração |
|---------|-------|-------------------|-----------|
| `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Reservation.cs` | V-01 | `dotnet-architecture` | `+ Confirm()`, `+ Cancel(string cancellationReason)`, `+ EnsurePending()` (guarda privada) |
| `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/ReservationSaga.cs` | V-01 | `dotnet-architecture` | `+ MarkAuthorized()`, `+ MarkRejected(string cancellationReason)` (assume `CancellationReason` já existente de F02; adicionar a propriedade aqui, sem redefinir, se F02 ainda não tiver rodado) |
| `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/SagaState.cs` | V-01 | `dotnet-architecture` | Nenhuma alteração se F02 já rodou (`Authorized`/`Rejected` já existem); adicionar os dois valores, sem redefinir, se ainda não existirem |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationRepository.cs` | V-01 | `dotnet-architecture` | `+ GetByCorrelationIdAsync(Guid, CancellationToken)`; `+ UpdateAsync(Reservation, CancellationToken)` só se F03 ainda não o tiver adicionado |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/ReservationRepository.cs` | V-01 | `dotnet-dependency-config` | `+ GetByCorrelationIdAsync` (`Include(Saga)`, `FirstOrDefaultAsync` por `Saga.CorrelationId` — `FirstOrDefaultAsync`, não `SingleOrDefaultAsync`, porque `correlation_id` não tem índice único no schema); `+ UpdateAsync` só se F03 ainda não o tiver adicionado |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/ReservationSagaConfiguration.cs` | V-01 | `dotnet-dependency-config` | Nenhuma alteração se F02 já rodou (mapeamento de `CancellationReason` já existe); adicionar, sem redefinir, se ainda não existir |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/MessagingExtensions.cs` | V-01 | `dotnet-dependency-config` | `+` 2 exchanges de publicação (`ReservationConfirmedTopology`/`ReservationCancelledTopology`) em `options.Exchanges`; `+` 2 `services.AddRmqTopicConsumer<TMessage,THandler>(...)` (payment_authorized/payment_rejected); `+` `services.AddScoped<IReservationConfirmedPublisher,...>()` e `IReservationCancelledPublisher` |
| `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/ReservationTests.cs` | V-01 | `dotnet-testing` | `+4` cenários: `Confirm` transiciona e marca a saga; `Confirm` lança fora de `Solicitada`; `Cancel` transiciona, marca a saga e grava o motivo; `Cancel` lança fora de `Solicitada` |

### Arquivos de Referência (não alterar)

| Caminho | Motivo da Consulta |
|---------|-------------------|
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/RequestReservationCommandHandler.cs` | Padrão exato de orquestração (try/catch best-effort de publicação, logging estruturado) a replicar nos dois novos handlers |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/ReservationRequestedRmqPublisher.cs` | Modelo do adapter `Rmq.CloudEvents` (exchange/routing key/cloudEventType, headers de correlação) para os dois novos publishers |
| `services/notification-worker/.../Messaging/DiagnosticPingConsumer.cs` | Referência do que NÃO replicar aqui: aquele consumidor é manual (`BackgroundService` + `RabbitMQ.Client` cru) porque antecedeu o uso de `AddRmqTopicConsumer`; esta TechSpec usa o recurso de mais alto nível da própria biblioteca já adotada |
| `rmq.cloudevents` (pacote NuGet 1.1.1) `README.md`/`README.pt-BR.md` | `AddRmqTopicConsumer<TMessage,THandler>`, `IRmqMessageHandler<TMessage>`, `MessageContext`, comportamento de retry/DLQ/ACK automático |
| `tasks/prd-consulta-reserva/techspec.md` | Especificação já aprovada de `SagaState.Authorized/Rejected` e `ReservationSaga.CancellationReason` — reutilizar exatamente, não redefinir |
| `tasks/prd-solicitacao-pagamento/techspec.md` | Especificação já aprovada de `IReservationRepository.UpdateAsync` — reutilizar exatamente, não redefinir |
| `tasks/prd-conclusao-saga/api-contract.yaml` | Fonte única dos 4 schemas de evento, exchanges/filas, headers e envelope CloudEvents |
| `domains/booking/domain.md` | RN-07 a RN-11; F04 no roadmap; eventos produzidos/consumidos |

---

## Pontos de Integração

- **RabbitMQ (`ecad-dev-rabbitmq`, vhost `/localize-stay`):**
  - Publica `booking.reservation_confirmed`/`booking.reservation_cancelled` em exchanges topic
    dedicadas (durable) — nomes idênticos ao já aprovado em `api-contract.yaml`, mesma instância de
    `Rmq.CloudEvents` já configurada. Sem binding de fila de consumo declarado aqui (decisão de
    Catalog/Notification).
  - Consome `payment.payment_authorized`/`payment.payment_rejected` via filas próprias declaradas
    idempotentemente no boot (`AddRmqTopicConsumer`), ligadas por routing key às exchanges homônimas
    mantidas por Payment — contrato de consumo provisório (Payment ainda sem `domain.md`/contrato
    próprio).
- **Sem chamada síncrona:** esta feature não depende de nenhum serviço externo síncrono.
- **Payment (domínio, fora do escopo):** publica os dois eventos consumidos aqui; quando Payment
  formalizar seu próprio contrato, revisar o schema provisório desta TechSpec/contrato sem quebrar
  Booking (tratamento defensivo já cobre campos extras).

---

## Análise de Impacto

| Componente Afetado | Tipo de Impacto | Descrição & Risco | Ação Requerida |
|--------------------|-----------------|-------------------|-----------------|
| `booking.reservations`/`booking.reservation_sagas` (Postgres) | Nenhum novo (reutiliza colunas de F01/F02) | Nenhuma migration nova; risco baixo | Confirmar que a migration habilitadora de F02 já rodou no ambiente de destino |
| Vhost `/localize-stay` (RabbitMQ) | Modificado | 2 exchanges novas de publicação + 2 filas novas de consumo (com DLQ automática `<queue>.dlq` da biblioteca); risco baixo, topologia idempotente no boot | Nenhuma ação manual |
| `IReservationRepository` (porta) | Modificado (aditivo) | `+ GetByCorrelationIdAsync`; qualquer implementação alternativa futura precisa implementá-lo | Nenhuma ação além da implementação em `ReservationRepository` |
| `ReservationStatus.Confirmada`/`Cancelada` (enum) | Usado por escrita pela primeira vez | Definido desde F01, nunca atribuído até esta feature; sem mudança de schema | Nenhuma |
| Payment (domínio) | Nenhum nesta TechSpec | Continua sem contrato próprio; esta TechSpec já trata o payload de forma defensiva | Nenhuma ação agora |
| Catalog / Notification (domínios) | Nenhum nesta TechSpec | Passam a ter exchanges reais de onde consumir (`booking.reservation_confirmed`/`cancelled`); o binding de fila é decisão de cada feature própria | Nenhuma ação agora |
| F05 (`reservation_calendar_v1`, futura) | Nenhum nesta TechSpec | Passa a ter estados terminais reais para expor | Nenhuma ação agora |
| F06 (Resiliência da Saga, futura) | Nenhum nesta TechSpec | Vai aprofundar dedup/timeout/compensação sobre o mesmo fluxo desta TechSpec | Registrar como referência quando F06 for especificada |

---

## Abordagem de Testes

### Testes Unitários

- `ReservationTests` (estendido): `Confirm()`/`Cancel(reason)` a partir de `Solicitada` transicionam
  `Status` e delegam corretamente a `Saga.MarkAuthorized()`/`MarkRejected(reason)`; chamar qualquer um
  dos dois a partir de `Confirmada`/`Cancelada` lança `InvalidOperationException` (RN-11).
- `ReservationSagaTests` (novo, ou estendido se F03 já criou o arquivo): `MarkAuthorized()` define
  `State = Authorized`; `MarkRejected(reason)` define `State = Rejected` e grava `CancellationReason`
  exatamente com o valor recebido.
- `ConfirmReservationCommandHandlerTests`/`CancelReservationCommandHandlerTests` (mocks de
  `IReservationRepository` e do publisher correspondente):
  - Reservation encontrada e `Solicitada` → transiciona, chama `UpdateAsync` uma vez, chama
    `PublishAsync` uma vez, retorna `Confirmed`/`Cancelled`.
  - Reservation não encontrada (`GetByCorrelationIdAsync` retorna `null`) → retorna
    `NotCorrelatable`; `UpdateAsync`/`PublishAsync` nunca são chamados (DP-03).
  - Reservation encontrada mas já `Confirmada`/`Cancelada` → retorna `AlreadyTerminal`;
    `UpdateAsync`/`PublishAsync` nunca são chamados, nenhuma exceção de domínio escapa do handler
    (DP-02/RN-11).
  - Publisher lança excecão → handler ainda retorna `Confirmed`/`Cancelled` (a transição já foi
    persistida antes da publicação — RF-03/DP-04), sem relançar.
- `PaymentAuthorizedConsumerTests`/`PaymentRejectedConsumerTests` (mock de `IDispatcher`):
  `correlationId` válido → `IDispatcher.SendAsync` chamado exatamente uma vez com o comando esperado;
  `correlationId` nulo/vazio/não-GUID → `IDispatcher.SendAsync` nunca é chamado.

### Testes de Integração

- **`PaymentAuthorizedConsumptionTests`/`PaymentRejectedConsumptionTests` (novo, mesma
  `CustomWebApplicationFactory`/coleção de `ReservationEndpointTests`):** usa Postgres + RabbitMQ
  Testcontainers já existentes; cria uma `Reservation` válida via `POST /v1/reservations` (F01) para
  obter um `correlationId` real; publica, via `RabbitMQ.Client` cru (mesmo mecanismo de
  `BindReservationEventsQueueAsync`), um evento simulando Payment na exchange
  `payment.payment_authorized`/`payment.payment_rejected` (a mesma exchange que o próprio boot do
  Booking já declarou via `AddRmqTopicConsumer`) com o `correlationId` da Reservation criada; aguarda
  (poll com timeout curto, já que o consumo é assíncrono) até `booking.reservations.status` mudar no
  banco; confere `status`/`state`/`cancellation_reason` persistidos e lê a mensagem publicada em
  `booking.reservation_confirmed`/`cancelled` (fila de teste ligada à exchange, mesmo padrão de
  `ReservationEndpointTests`), conferindo todos os campos do contrato.
  - Cenário duplicado/tardio: publica o mesmo evento novamente após a confirmação/cancelamento já
    persistido → confere que nenhuma segunda mensagem aparece na fila de teste e que o estado no banco
    não muda.
  - Cenário não correlacionável: publica com um `correlationId` aleatório sem Reservation
    correspondente → confere que nenhuma mensagem é publicada e nenhuma Reservation existente é
    alterada (implícito ao restante da suíte continuar passando).
- Não há teste de integração dedicado à falha de publicação do evento final (broker indisponível) —
  mesma decisão já tomada por F03 para `booking.payment_requested` (derrubar o broker no meio do teste
  é complexidade desproporcional ao valor já coberto pelo teste unitário do handler).
- Não há teste de integração para envelope CloudEvents malformado (JSON inválido) — é responsabilidade
  já coberta pela suíte própria do pacote `Rmq.CloudEvents`, não desta feature.

### Testes de Contrato

- Não aplicável neste ciclo (mesma decisão de F03): o `api-contract.yaml` já foi validado via Spectral
  na etapa de contrato (`api-contract.md` §Validação, 0 erros). A garantia de fidelidade ao schema do
  evento vem dos testes de integração acima, que leem a mensagem real publicada e comparam campo a
  campo com os exemplos do contrato.

---

## Sequenciamento de Desenvolvimento

### Build Order

1. **Domain** (`Reservation.Confirm/Cancel`, `ReservationSaga.MarkAuthorized/MarkRejected`, e — só se
   F02 ainda não tiver rodado — `SagaState.Authorized/Rejected`/`ReservationSaga.CancellationReason`)
   — depende apenas de F01 já mesclado (e, para o vocabulário reutilizado, de F02 disponível).
2. **Application** (`IReservationConfirmedPublisher`/`IReservationCancelledPublisher`,
   `IReservationRepository` estendido, os dois comandos/handlers) — depende de 1.
3. **Infra** (`ReservationConfirmedRmqPublisher`/`ReservationCancelledRmqPublisher`,
   `ReservationRepository` estendido, — e só se F03 ainda não tiver rodado — `UpdateAsync`) — depende
   de 1 e 2.
4. **Api/Messaging** (topologias, mensagens, os dois consumidores) e **Wiring**
   (`MessagingExtensions`) — depende de 1, 2 e 3.
5. **Testes** (unitários de 1-2, integração ponta a ponta de 1-4) — depende de todos os anteriores.

Como há uma única fatia vertical, esta ordem é interna a V-01, não uma sequência de fatias
independentes.

### Dependências Técnicas Bloqueantes

- **F02 (habilitador `SagaState.Authorized/Rejected` + `ReservationSaga.CancellationReason` +
  migration) precisa estar disponível no ambiente onde esta feature será implementada.** Hoje esse
  habilitador existe apenas no worktree/branch `feature/prd-consulta-reserva` (checkpoint
  `enabling/static gate APROVADO`, task 1.0), ainda não mesclado em `main`. Sem ele, `SagaState` só tem
  `PaymentPending` e `ReservationSaga` não tem `CancellationReason` — os métodos `MarkAuthorized`/
  `MarkRejected` desta TechSpec não têm vocabulário para gravar o resultado. Se esse habilitador não
  estiver disponível, esta TechSpec autoriza adicioná-lo com a especificação exata já aprovada por F02
  (não uma nova decisão), em vez de bloquear a implementação — mesma postura que F03 registrou para a
  dependência em F01.
- **F03 (`IReservationRepository.UpdateAsync`) não é bloqueante, mas é reutilizado se disponível.** Se
  a implementação de F03 ainda não tiver adicionado `UpdateAsync`, esta TechSpec o adiciona com a
  mesma assinatura já especificada em `tasks/prd-solicitacao-pagamento/techspec.md` — nenhuma decisão
  nova, apenas evita desenhar dois métodos equivalentes caso as duas features avancem fora de ordem.
- Nenhuma dependência de Catalog: esta feature não chama Catalog sincronamente, e o lado de consumo de
  Catalog/Notification sobre os eventos publicados aqui é responsabilidade de features próprias
  desses domínios, ainda não especificadas.
- Nenhuma dependência real de Payment: o lado de publicação de Payment está fora do escopo e ainda sem
  contrato — o schema provisório desta TechSpec/contrato já assume isso.

---

## Monitoramento e Observabilidade

- Logging estruturado (mesmo padrão de F01/F03) nos handlers, em quatro pontos por caminho
  (autorização/rejeição): confirmação/cancelamento efetivado, evento ignorado por não-correlação
  (DP-03), evento ignorado por saga já terminal (DP-02/RN-11), e falha best-effort de publicação
  (RF-03/DP-04) — todos com `ReservationId`/`CorrelationId` nos campos estruturados, permitindo
  localizar manualmente qualquer caso via log correlacionado do baseline.
- `Rmq.CloudEvents` expõe métricas (`rmq.consume.*`) e tracing (`ActivitySource`) próprios para os dois
  novos consumidores automaticamente, sem código adicional — mesma infraestrutura de observabilidade já
  disponível desde a fundação, se/quando o projeto ligar o exportador OpenTelemetry (não obrigatório
  nesta fase, baseline).
- Nenhuma métrica de negócio customizada nesta fase, conforme baseline (igual a F01/F03).

---

## Considerações Técnicas

### Decisões Principais

- **Decisão:** consumir `payment.payment_authorized`/`payment.payment_rejected` via
  `Rmq.CloudEvents.Extensions.AddRmqTopicConsumer<TMessage,THandler>` em vez de um `BackgroundService`
  manual com `RabbitMQ.Client` cru (padrão usado por `DiagnosticPingConsumer` na fundação).
  **Racional:** a biblioteca já está adotada desde a fundação (publisher) e oferece exatamente o que
  esta feature precisa — declaração idempotente de tópico/fila, unwrap de CloudEvents, retry
  exponencial e DLQ automática — como recurso de mais alto nível, sem nenhum código de baixo nível
  novo (`dotnet-dependency-config`: reaproveitar antes de construir).
  **Trade-offs:** menos controle fino sobre o ciclo de vida do consumo do que o padrão manual; aceitável
  porque esta feature não precisa de nenhum comportamento que a biblioteca não ofereça.
  **Alternativas rejeitadas:** replicar o padrão manual de `DiagnosticPingConsumer` — rejeitada por
  reintroduzir código (backoff, ACK/NACK, declaração de topologia) que a própria biblioteca já
  resolve, contrariando a razão de já ter adotado `Rmq.CloudEvents` desde a fundação.

- **Decisão:** a verificação de "saga já terminal" (RN-11) acontece no `CommandHandler` (Application),
  antes de chamar `Reservation.Confirm()`/`Cancel()`; o método de domínio só relança
  `InvalidOperationException` como rede de segurança, não como o caminho normal de "ignorar
  duplicado".
  **Racional:** o handler precisa distinguir três desfechos (`Confirmed`/`AlreadyTerminal`/
  `NotCorrelatable`) para decidir se publica ou não — um `try/catch` sobre uma exceção de domínio para
  o caso normal e esperado de "evento duplicado" tornaria o fluxo de controle menos claro do que uma
  verificação explícita de estado antes de agir (DP-02 do PRD já descreve exatamente essa verificação).
  **Trade-offs:** nenhum significativo — o domínio ainda impede a transição inválida por si só, caso
  algum chamador futuro esqueça de verificar.

- **Decisão:** `[JsonPropertyName]` explícito em todos os records de payload desta feature
  (publicados e consumidos), independente da política global de serialização de `Rmq.CloudEvents`.
  **Racional:** o evento `booking.reservation_requested` (F01) já publicado tem seu payload real em
  `PascalCase` (verificado em `ReservationEndpointTests.BasicGetOneAsync`, que lê
  `data.GetProperty("ReservationId")`), enquanto a documentação de convenções do projeto (F01/F02/F03)
  descreve os payloads em `camelCase` — uma divergência preexistente entre documentação e implementação
  que não é desta TechSpec para corrigir (fora de escopo, evento diferente), mas que esta TechSpec não
  deve repetir: o `api-contract.yaml` de F04, já aprovado, declara `camelCase`, então os records aqui
  usam atributos explícitos para garantir que o payload real corresponda exatamente ao contrato, sem
  depender de nenhuma configuração global não verificada.
  **Trade-offs:** nenhum — é estritamente mais explícito que o padrão anterior (F01), sem quebrar
  compatibilidade com nada existente.
  **Alternativas rejeitadas:** confiar na política de serialização padrão da biblioteca sem verificar —
  rejeitada porque já se comprovou (no evento de F01) que o resultado não é necessariamente
  `camelCase`, e um payload que não corresponde ao contrato aprovado quebraria Catalog/Notification
  silenciosamente quando essas features forem implementadas.

- **Decisão:** `IReservationRepository.GetByCorrelationIdAsync` usa `FirstOrDefaultAsync`, não
  `SingleOrDefaultAsync` (padrão usado por `GetByIdAsync`, F02).
  **Racional:** `Saga.CorrelationId` não tem índice/constraint único declarado no schema (diferente do
  `Id`, chave primária) — `FirstOrDefaultAsync` não lança se, por qualquer causa futura, mais de uma
  linha coincidir; `SingleOrDefaultAsync` lançaria uma exceção não tratada dentro de um consumidor de
  evento, o que é pior do que processar a primeira ocorrência encontrada.
  **Trade-offs:** nenhum dado real de negócio hoje permite duplicidade (`Reservation.Create` sempre
  gera um `correlationId` novo); esta é uma defesa barata, não uma feature nova.

### Riscos Conhecidos

- **Janela de corrida entre entregas verdadeiramente concorrentes do mesmo evento** (duas mensagens
  idênticas processadas em paralelo antes que a primeira termine de persistir): a verificação de
  estado atual (DP-02) só protege contra duplicados que chegam APÓS a primeira transição já ter sido
  persistida; um dedup store por `eventId` (F06) resolveria a concorrência verdadeira — aceito como
  risco residual já documentado no PRD (Riscos e Mitigações) e no Domain Doc (`domains/booking/domain.md`
  §8).
- **Confirmar em tempo de implementação se `AddRmqTopicConsumer` cria um `IServiceScope` por mensagem
  automaticamente** (esperado, dado o padrão "DI-first" documentado da biblioteca, já que
  `IDispatcher`/`BookingDbContext`/handlers são `Scoped`) — se não criar, os consumidores precisam
  injetar `IServiceScopeFactory` e criar o escopo manualmente antes de resolver `IDispatcher`, em vez
  de recebê-lo diretamente no construtor como no desenho acima.
- **Divergência preexistente `camelCase` (docs) vs. `PascalCase` (payload real) em
  `booking.reservation_requested`** (F01): observação registrada em "Decisões Principais" acima, fora
  do escopo desta TechSpec corrigir — sinalizar para uma revisão futura de F01/F03 se o projeto quiser
  unificar.

### Requisitos Especiais

Não aplicável — sem requisito de performance, segurança adicional (Fase 0 não implementa autenticação)
ou conformidade regulatória.

### Conformidade com Skills

- Segue `dotnet-architecture` (Clean Architecture, CQRS nativo sem MediatR, porta+adapter para as
  quatro integrações assíncronas desta feature, consumidor como driving adapter fino análogo a um
  endpoint).
- Segue `dotnet-dependency-config` (reaproveita `Rmq.CloudEvents` já adotado, sem introduzir
  dependência nova; EF Core com `IEntityTypeConfiguration` já existente apenas estendido se necessário).
- Segue `dotnet-testing` (unitário para regra/orquestração/adapters finos, integração com
  Testcontainers reais para o broker e o banco).

**Desvios identificados:** nenhum.

---

## Questões em Aberto

- [ ] Confirmar, no momento da implementação, se o habilitador de F02 (`SagaState.Authorized/Rejected`,
  `ReservationSaga.CancellationReason`, migration) já está mesclado em `main` ou disponível no ambiente
  de trabalho — se não, aplicar a especificação já aprovada por F02 como parte desta implementação
  (não uma nova decisão), conforme "Dependências Técnicas Bloqueantes".
- [ ] Confirmar, no momento da implementação, se `IReservationRepository.UpdateAsync` (F03) já existe;
  se não, adicioná-lo com a assinatura já aprovada por F03.
- [ ] Verificar em tempo de implementação o comportamento real de criação de escopo por mensagem de
  `AddRmqTopicConsumer` (ver "Riscos Conhecidos") — ajustar o desenho do consumidor para
  `IServiceScopeFactory` se necessário.
- Nenhum conflito identificado com o API Contract (`api-contract.yaml`, já "Aprovado") — esta TechSpec
  não propõe nenhuma mudança de schema, exchange ou routing key além do que já está lá.

---

## Architecture Decision Records

Nenhuma ADR nova é necessária: esta TechSpec aplica decisões já aceitas (stack, broker, convenção de
correlação e nomenclatura de eventos, biblioteca de mensageria já adotada) sem introduzir escolha
arquitetural nova. Usar `AddRmqTopicConsumer` em vez de um consumidor manual é uma decisão de
implementação local (qual recurso de uma biblioteca já aprovada usar), não uma decisão estrutural
durável.

- [ADR-001: Stack de backend — .NET / C# (ASP.NET Core)](../../docs/adr/adr-001-backend-stack-dotnet.md)
- [ADR-002: Broker de eventos da Fase 0 — RabbitMQ](../../docs/adr/adr-002-broker-fase0-rabbitmq.md) — já lista `PaymentAuthorized`/`PaymentRejected`/`ReservationConfirmed`/`ReservationCancelled` como exchanges modeladas 1:1 com os eventos desde a decisão original.

---

## Próximos Passos

1. **Confirmar o estado de F02** (habilitador mesclado ou acessível) e, secundariamente, de F03
   (`UpdateAsync`) antes de acionar o Task Creator — pré-requisito bloqueante apenas para F02; F03 é
   reutilizado se disponível, não bloqueante.
2. **Aprovação do usuário** sobre este draft (decisões novas desta TechSpec: uso de
   `AddRmqTopicConsumer`, `GetByCorrelationIdAsync` com `FirstOrDefaultAsync`, `[JsonPropertyName]`
   explícito, guarda de domínio via exceção como rede de segurança) antes de promover para
   `techspec.md` (`Aprovado`).
3. **Implementação:** usar `tsg-flow-task-creator` referenciando esta TechSpec após promoção.
4. **Frontend:** não aplicável — o PRD confirma que esta feature não introduz nenhuma tela ou ação
   nova (observável apenas indiretamente via F02).
