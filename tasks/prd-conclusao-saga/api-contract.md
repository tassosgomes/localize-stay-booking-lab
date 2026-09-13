# API Contract — Conclusão da Saga (Booking F04)

> **Gerado a partir de:** `tasks/prd-conclusao-saga/prd.md`
> **Data:** 2026-09-12
> **Status:** Aprovado
> **Versão do contrato:** 1.0.0
> **Formato:** AsyncAPI 3.1 (evento assíncrono via RabbitMQ) — não OpenAPI/REST. Esta feature não
> introduz nenhum endpoint HTTP (PRD, Restrições Técnicas de Alto Nível); a skill padrão
> `tsg-flow-contract-creator` gera OpenAPI, mas foi adaptada para AsyncAPI por decisão do usuário —
> mesma decisão já tomada em `tasks/prd-solicitacao-pagamento/api-contract.md` (F03), seguindo o
> padrão em produção em `contracts/asyncapi/diagnostics-v1.yaml`.

---

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Protocolo | RabbitMQ/AMQP, vhost `/localize-stay` | ADR-002; mesmo broker de `contracts/asyncapi/diagnostics-v1.yaml` e F03 |
| Versionamento | Versão embutida no tipo do evento (`ReservationConfirmed.v1`, `ReservationCancelled.v1`) | `context/architecture-baseline.md` §Padrões de Comunicação |
| Exchange / routing key (publicação) | `booking.reservation_confirmed` / `booking.reservation_cancelled` (topic, durable), 1:1 com o evento | ADR-002; mesmo padrão de `booking.payment_requested` (F03) |
| Fila / binding (consumo) | Fila própria de Booking (`booking.payment_authorized` / `booking.payment_rejected`) ligada à exchange homônima mantida por Payment | Mesmo padrão de canal duplo de `contracts/asyncapi/diagnostics-v1.yaml` (`diagnostics.topic` → `notification.diagnostics`) |
| Contrato de consumo | Provisório quanto ao produtor (Payment ainda não tem `domain.md`/contrato próprio) | PRD, Questões em Aberto; mesma natureza do rascunho de Catalog em F01 |
| Identificador de correlação | `correlationId` = UUID da Reservation Saga (reutilizado de F02/F03, não redefinido) | `domains/booking/domain.md` §3 |
| Causation (publicação) | `causationId == correlationId` — não há identificador de evento próprio em `payment.payment_authorized`/`payment.payment_rejected` ainda | Mesma convenção de auto-causação de `diagnosticPing` e `paymentRequested`, aplicada por ausência de alternativa |
| Minimização de payload | Só `reservationId`, `accommodationId`, `guestReference`, `checkIn`, `checkOut` (+ `cancellationReason` no cancelamento) | Restrições Técnicas do PRD — Catalog e Notification não precisam de valor monetário nem de detalhe de Payment |
| Motivo de cancelamento | Texto de negócio fixo, nunca o payload técnico de Payment | DP-01 do PRD; mesmo texto já exposto em `tasks/prd-consulta-reserva/api-contract.md` |
| Retry/DLQ de consumo | Comportamento padrão já da biblioteca (`Rmq.CloudEvents`/`AddRmqTopicConsumer`): retry exponencial + DLQ automática (`<queue>.dlq`) | Não é dedup store/Outbox/reprocessamento manual — isso pertence à F06; retry/DLQ da biblioteca é reaproveitado, não construído nesta feature |
| Nomenclatura | `camelCase` no payload | Consistência com F01/F02/F03 |

---

## Resumo dos Eventos

| Evento | Direção (Booking) | Exchange / Fila | Protocolo | Produtor | Consumidor |
|---|---|---|---|---|---|
| `PaymentAuthorized.v1` (`payment.payment_authorized`) | Consome (`receive`) | Fila `booking.payment_authorized` ← exchange `payment.payment_authorized` (topic) | AMQP (RabbitMQ) | Payment (schema provisório) | Booking |
| `PaymentRejected.v1` (`payment.payment_rejected`) | Consome (`receive`) | Fila `booking.payment_rejected` ← exchange `payment.payment_rejected` (topic) | AMQP (RabbitMQ) | Payment (schema provisório) | Booking |
| `ReservationConfirmed.v1` (`booking.reservation_confirmed`) | Publica (`send`) | Exchange `booking.reservation_confirmed` (topic) | AMQP (RabbitMQ) | Booking | Catalog, Notification (binding a definir por cada feature própria) |
| `ReservationCancelled.v1` (`booking.reservation_cancelled`) | Publica (`send`) | Exchange `booking.reservation_cancelled` (topic) | AMQP (RabbitMQ) | Booking | Catalog, Notification (binding a definir por cada feature própria) |

---

## Eventos Consumidos (Provisórios)

### `payment.payment_authorized` — Autorização de pagamento (RF-01)

**Propósito:** Payment decide a autorização e publica este evento; Booking o consome para confirmar
a Reservation correlacionada (RN-07).
**Publicado por:** Payment (sem contrato/domain.md formal nesta etapa — schema provisório).
**Consumido por:** Booking, via fila própria `booking.payment_authorized`.

```json
{
  "correlationId": "b2c4e6a8-1234-4abc-9def-0123456789ab",
  "authorizedAt": "2026-09-12T14:25:10Z"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `correlationId` | string (uuid) | Sim | Único campo do qual Booking depende para localizar a Reservation Saga (RN-07). |
| `authorizedAt` | string (date-time) | Não | Informativo — Booking usa seu próprio `UtcNow` ao confirmar. |

> **Tratamento defensivo:** qualquer campo além de `correlationId` é tratado como opaco por Booking —
> não bloqueia o processamento se ausente ou se houver campos extras (Questão em Aberto do PRD).

### `payment.payment_rejected` — Rejeição de pagamento (RF-02)

**Propósito:** Payment decide a rejeição e publica este evento; Booking o consome para cancelar a
Reservation correlacionada com um motivo de negócio (RN-08, DP-01).
**Publicado por:** Payment (sem contrato/domain.md formal nesta etapa — schema provisório).
**Consumido por:** Booking, via fila própria `booking.payment_rejected`.

```json
{
  "correlationId": "c3d5f7b9-2345-4bcd-8ef0-123456789abc",
  "rejectedAt": "2026-09-12T14:25:10Z"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `correlationId` | string (uuid) | Sim | Único campo do qual Booking depende para localizar a Reservation Saga (RN-08). |
| `rejectedAt` | string (date-time) | Não | Informativo — Booking usa seu próprio `UtcNow` ao cancelar. |

> **Tratamento defensivo (DP-01):** um eventual campo técnico de motivo (ex.: `reason`) que Payment
> venha a publicar é ignorado — Booking nunca propaga o payload técnico de Payment como motivo de
> cancelamento, apenas seu próprio texto de negócio (ver `booking.reservation_cancelled` abaixo).

#### Headers AMQP (ambos os eventos consumidos)

| Header | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `x-correlation-id` | string (uuid) | Sim | Mesmo valor do `correlationId` do payload |
| `x-causation-id` | string | Sim | Causation ID do lado de Payment — opaco para Booking nesta fase |

---

## Eventos Publicados

### `booking.reservation_confirmed` — Confirmação final (RF-01)

**Propósito:** Publicado automaticamente ao concluir a transição da Reservation para `confirmada`
(RN-07, RN-10). Habilita Catalog a criar o Availability Block e Notification a notificar as partes.
**Publicado por:** Booking, ao reagir a `payment.payment_authorized`.
**Consumido por:** Catalog, Notification (fora do escopo deste contrato).

```json
{
  "correlationId": "b2c4e6a8-1234-4abc-9def-0123456789ab",
  "causationId": "b2c4e6a8-1234-4abc-9def-0123456789ab",
  "reservationId": "8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e",
  "accommodationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "guestReference": "guest-marina-alves",
  "checkIn": "2026-10-10",
  "checkOut": "2026-10-13",
  "confirmedAt": "2026-09-12T14:25:11Z"
}
```

> Exemplo correlacionado ao mesmo cenário "pagamentoPendente" de
> `tasks/prd-consulta-reserva/api-contract.md` (Reservation `8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e`).

### `booking.reservation_cancelled` — Cancelamento final (RF-02)

**Propósito:** Publicado automaticamente ao concluir a transição da Reservation para `cancelada`
(RN-08, RN-10). Habilita Catalog a manter o Availability Block livre e Notification a notificar as
partes, com o motivo de negócio do cancelamento (DP-01).
**Publicado por:** Booking, ao reagir a `payment.payment_rejected`.
**Consumido por:** Catalog, Notification (fora do escopo deste contrato).

```json
{
  "correlationId": "c3d5f7b9-2345-4bcd-8ef0-123456789abc",
  "causationId": "c3d5f7b9-2345-4bcd-8ef0-123456789abc",
  "reservationId": "1e2d3c4b-5a69-478f-9b0c-1d2e3f405162",
  "accommodationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "guestReference": "guest-joao-pereira",
  "checkIn": "2026-11-01",
  "checkOut": "2026-11-05",
  "cancelledAt": "2026-09-12T14:25:11Z",
  "cancellationReason": "Pagamento rejeitado pela simulação de Payment."
}
```

#### Headers AMQP (ambos os eventos publicados)

| Header | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `x-correlation-id` | string (uuid) | Sim | Mesmo valor do `correlationId` do payload |
| `x-causation-id` | string (uuid) | Sim | Igual a `correlationId` nesta publicação (ver premissa "Causation") |

#### Envelope de transporte (todos os 4 eventos)

Transporte via CloudEvents (Rmq.CloudEvents): o payload viaja no campo `data`, com
`type = com.localizestay.<evento>.v1` (`payment.payment_authorized.v1`, `payment.payment_rejected.v1`,
`booking.reservation_confirmed.v1`, `booking.reservation_cancelled.v1`) e `source = /booking` (nas
publicações) — mesma convenção de `contracts/asyncapi/diagnostics-v1.yaml`.

---

## Schema dos Payloads Publicados

| Campo | Tipo | Obrigatório | Em | Descrição |
|---|---|---|---|---|
| `correlationId` | string (uuid) | Sim | Ambos | Identificador da Reservation Saga (reutilizado de F02/F03). |
| `causationId` | string (uuid) | Sim | Ambos | Igual a `correlationId` (ver premissa "Causation"). |
| `reservationId` | string (uuid) | Sim | Ambos | Identificador da Reservation — conceitualmente distinto de `correlationId` (`domains/booking/domain.md` §3), hoje com o mesmo valor na implementação de F01. |
| `accommodationId` | string (uuid) | Sim | Ambos | Necessário a Catalog para o Availability Block (RN-05/RN-09). |
| `guestReference` | string | Sim | Ambos | Necessário a Notification para identificar a quem notificar. |
| `checkIn` | string (date) | Sim | Ambos | Necessário a Catalog para o Availability Block. |
| `checkOut` | string (date) | Sim | Ambos | Necessário a Catalog para o Availability Block. |
| `confirmedAt` | string (date-time) | Sim | `reservation_confirmed` | Instante UTC da confirmação. |
| `cancelledAt` | string (date-time) | Sim | `reservation_cancelled` | Instante UTC do cancelamento. |
| `cancellationReason` | string | Sim | `reservation_cancelled` | Motivo de negócio (DP-01), nunca o payload técnico de Payment. |

**Campos deliberadamente ausentes** (Restrições Técnicas do PRD): `totalAmount`, `currency`,
`pricePerNight`, `guestsCount` e qualquer detalhe interno do processamento de Payment — nem Catalog
nem Notification precisam de valor monetário para agir.

---

## Caminho de Falha (DP-04) — não modelado como mensagem

Se a publicação de `booking.reservation_confirmed`/`booking.reservation_cancelled` falhar depois que
a Reservation e a Saga já foram atualizadas internamente para o estado terminal (RF-03), **nenhuma
mensagem de compensação é emitida** — a transição de estado já é válida por si só; apenas a
notificação a Catalog/Notification não chega. A falha é registrada em log interno do Booking para
investigação manual, sem nova tentativa automática nesta fase (Outbox/retry ficam para F06).

---

## Premissas e Decisões Registradas no PRD

- **DP-01:** o motivo de cancelamento reflete o fato de negócio "pagamento rejeitado", nunca o
  payload técnico de Payment.
- **DP-02:** Booking verifica o estado atual da Reservation antes de transicionar; se já terminal, o
  evento consumido é ignorado (RN-11) — sem deduplicação robusta (Outbox/dedup store) nesta fase.
- **DP-03:** um evento consumido não correlacionável é ignorado para fins de negócio e logado, sem
  falhar o processamento nem interromper outras mensagens.
- **DP-04:** falha de publicação do evento final não desfaz a transição de estado já persistida.

---

## Questões em Aberto

- **Schema real de Payment:** este contrato declara o piso mínimo (`correlationId` +
  timestamp informativo) para `payment.payment_authorized`/`payment.payment_rejected`, pois Payment
  ainda não tem `domain.md` nem contrato próprio. Quando essa feature existir, revisar este documento
  para refletir o schema real publicado — sem quebrar Booking, que já trata campos extras de forma
  defensiva.
- **Nível de log para evento não correlacionável / resultado tardio:** fica para a TechSpec, seguindo
  o logging básico já previsto no baseline (não bloqueia este contrato — PRD, Questões em Aberto).
- **Binding de fila por Catalog/Notification:** decisão das próprias features desses domínios ao
  consumir `booking.reservation_confirmed`/`booking.reservation_cancelled` — não definida aqui, mesma
  postura já adotada em F03 para `booking.payment_requested`.

---

## Validação

- **Lint Spectral:** `npx --yes @stoplight/spectral-cli lint tasks/prd-conclusao-saga/api-contract.yaml --ruleset <wrapper com extends: ["spectral:asyncapi"]> --fail-severity=error` — **0 erros**, 3 warnings informativos (tags/license/contact ausentes — mesmo padrão de `contracts/asyncapi/diagnostics-v1.yaml` e de `tasks/prd-solicitacao-pagamento/api-contract.yaml`, que já convivem com os mesmos avisos). O ruleset empacotado da skill (`rulesets/openapi.yaml`) é específico para OpenAPI e não se aplica a este documento AsyncAPI.
