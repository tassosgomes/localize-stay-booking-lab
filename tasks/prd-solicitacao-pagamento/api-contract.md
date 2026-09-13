# API Contract — Solicitação de Pagamento (Booking F03)

> **Gerado a partir de:** `tasks/prd-solicitacao-pagamento/prd.md`
> **Data:** 2026-09-12
> **Status:** Aprovado
> **Versão do contrato:** 1.0.0
> **Formato:** AsyncAPI 3.1 (evento assíncrono via RabbitMQ) — não OpenAPI/REST. Esta feature não
> introduz nenhum endpoint HTTP (PRD, Restrições Técnicas de Alto Nível); a skill padrão
> `tsg-flow-contract-creator` gera OpenAPI, mas foi adaptada para AsyncAPI por decisão do usuário,
> seguindo o padrão já em produção em `contracts/asyncapi/diagnostics-v1.yaml`.

---

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Protocolo | RabbitMQ/AMQP, vhost `/localize-stay` | ADR-002; mesmo broker de `contracts/asyncapi/diagnostics-v1.yaml` |
| Versionamento | Versão embutida no tipo do evento (`PaymentRequested.v1`) | `context/architecture-baseline.md` §Padrões de Comunicação |
| Exchange / routing key | `booking.payment_requested` (topic, durable), 1:1 com o evento | ADR-002; `domains/booking/domain.md` §7 |
| Moeda | BRL fixo | PD-001; mesma convenção de F01/F02 |
| Valores monetários | String decimal, 2 casas (ex. `"1050.00"`) | Mesma convenção de F01/F02 — evita ponto flutuante |
| Identificador de correlação | `correlationId` = UUID da Reservation Saga (≠ `id`/`reservationId`) | `domains/booking/domain.md` §3; mesmo campo exposto por F02 |
| Causation | `causationId == correlationId` nesta publicação | Este evento origina a etapa assíncrona da saga (sem evento anterior a reagir) |
| Nomenclatura | `camelCase` no payload | Consistência com F01/F02 |
| Isolamento de domínio | Payload só traz `correlationId`, `totalAmount`, `currency`, `requestedAt` | Restrições Técnicas do PRD — Payment não precisa de dados de Accommodation/Guest |

---

## Resumo do Evento

| Evento | Exchange / Routing Key | Protocolo | Produtor | Consumidor |
|---|---|---|---|---|
| `PaymentRequested.v1` (`booking.payment_requested`) | `booking.payment_requested` (topic) | AMQP (RabbitMQ) | Booking | Payment (fila/binding a definir na feature própria de Payment) |

---

## Evento Detalhado

### `booking.payment_requested` — Solicitação de autorização de pagamento

**Propósito:** Assim que Booking cria com sucesso uma Reservation em `solicitada` (F01), publica
automaticamente este evento para iniciar a etapa assíncrona da saga — sem nenhuma ação do Guest ou
de outro solicitante (DP-01). Carrega o identificador de correlação da Reservation Saga e o valor
total/moeda já congelados na Reservation (RN-06), nunca recalculados na publicação.

**Publicado por:** Booking, na sequência da criação da Reservation (F01 → F03).
**Consumido por:** Payment (para decidir autorização — fora do escopo deste PRD/contrato; F04 reage
ao resultado).

#### Payload

```json
{
  "correlationId": "b2c4e6a8-1234-4abc-9def-0123456789ab",
  "causationId": "b2c4e6a8-1234-4abc-9def-0123456789ab",
  "totalAmount": "1050.00",
  "currency": "BRL",
  "requestedAt": "2026-09-12T14:22:05Z"
}
```

> Exemplo correlacionado ao cenário "pagamentoPendente" de `tasks/prd-consulta-reserva/api-contract.md`
> (Reservation `8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e`, `totalAmount` `"1050.00"`).

#### Headers AMQP

| Header | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `x-correlation-id` | string (uuid) | Sim | Mesmo valor do `correlationId` do payload |
| `x-causation-id` | string (uuid) | Sim | Mesmo valor do `causationId` do payload |

#### Envelope de transporte

Transporte via CloudEvents (Rmq.CloudEvents): o payload acima viaja no campo `data`, com
`type = com.localizestay.booking.payment_requested.v1` e `source = /booking` — mesma convenção de
`contracts/asyncapi/diagnostics-v1.yaml`.

---

## Schema do Payload

| Campo | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `correlationId` | string (uuid) | Sim | Identificador da Reservation Saga — **não** é o `id` da Reservation; mesmo valor exposto por Booking F02. |
| `causationId` | string (uuid) | Sim | Igual a `correlationId` nesta publicação (origina a etapa assíncrona da saga). |
| `totalAmount` | string (decimal, 2 casas) | Sim | Valor total congelado na Reservation (RN-06), idêntico ao de F01/F02 — nunca recalculado. |
| `currency` | string (enum: `BRL`) | Sim | Moeda fixa do laboratório (PD-001). |
| `requestedAt` | string (date-time, UTC, ISO 8601) | Sim | Instante da publicação. |

**Campos deliberadamente ausentes** (Restrições Técnicas do PRD): `reservationId`,
`accommodationId`, `guestReference`, `pricePerNight`, `checkIn`/`checkOut`, `guestsCount` — Payment
decide a autorização apenas a partir de valor, moeda e correlação, sem acessar dados internos de
Booking.

---

## Caminho de Falha (DP-02) — não modelado como mensagem

Se a publicação falhar (ex.: broker indisponível), **nenhuma mensagem é emitida** — não há evento de
compensação ou de erro nesta fase. A Reservation permanece `solicitada`, a Reservation Saga
permanece sem registro de solicitação enviada, e a falha é registrada apenas em log interno do
Booking para investigação manual. Retry automático, Outbox e DLQ ficam para F06 (Resiliência da
Saga) — fora do escopo deste contrato.

---

## Premissas e Decisões Registradas no PRD

- **DP-01:** publicação sempre automática, sem gatilho externo — não existe operação para
  "solicitar pagamento" manualmente.
- **DP-02:** falha de publicação não desfaz a Reservation nem aciona retry automático nesta fase.

---

## Questões em Aberto

- **Consumo por Payment:** este contrato define apenas o lado de publicação (`send`) do Booking. O
  binding de fila, política de DLQ/retry no consumo e o schema de reação (`payment.payment_authorized`
  / `payment.payment_rejected`, consumidos por F04) pertencem à própria feature de Payment que
  reagirá a `PaymentRequested.v1` — ainda sem PRD/contrato próprio nesta etapa. Quando essa feature
  existir, deve declarar seu canal de consumo referenciando esta mesma mensagem, sem alterar seu
  payload unilateralmente (mudança incompatível exige nova versão, `PaymentRequested.v2`).
- **Retry/Outbox:** confirmado no PRD que ficam reservados para F06; não antecipados aqui.

---

## Validação

- **Lint Spectral:** `npx --yes @stoplight/spectral-cli lint tasks/prd-solicitacao-pagamento/api-contract.yaml --ruleset <wrapper com extends: ["spectral:asyncapi"]> --fail-severity=error` — **0 erros**, 3 warnings informativos (tags/license/contact ausentes — mesmo padrão de `contracts/asyncapi/diagnostics-v1.yaml`, que já convive com os mesmos avisos). O ruleset empacotado da skill (`rulesets/openapi.yaml`) é específico para OpenAPI e não se aplica a este documento AsyncAPI.
