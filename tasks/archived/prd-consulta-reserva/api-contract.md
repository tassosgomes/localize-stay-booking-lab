# API Contract — Consulta de Reserva (Booking F02)

> **Gerado a partir de:** `tasks/prd-consulta-reserva/prd.md`
> **Data:** 2026-09-12
> **Status:** Em Revisão
> **Versão do contrato:** 1.0.0

---

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---------|---------|--------|
| Autenticação | Nenhuma (Fase 0) | `context/architecture-baseline.md` §Princípios de Segurança; PD-002 |
| Identificador da Reservation | UUID (`reservationId`) | Herda o mesmo `id` definido para Reservation em Booking F01 |
| Formato de datas | ISO 8601 (`date` para período, `date-time` para `createdAt`) | Consistência com o contrato de F01 |
| Valores monetários | String decimal com 2 casas (ex.: `"350.00"`) | Mesma convenção de F01; evita ponto flutuante |
| Moeda | BRL fixo | PD-001 — moeda única do laboratório |
| Nomenclatura de campos | camelCase | Mesma convenção de F01 |
| Versionamento | Prefixo `/v1/` | Mesma convenção de F01 |
| Erros | RFC 9457 (Problem Details) | Mesma convenção de F01 |
| Situação da saga | Campo `sagaStatus` com `pendente`/`autorizado`/`rejeitado` | Termos Canônicos do PRD ("Situação da saga") |
| Identificador de correlação | Campo `correlationId` (UUID) | DP-02 do PRD — permite correlação manual com logs do baseline |
| Motivo de cancelamento | Campo `cancellationReason` nullable | Presente apenas quando `status = cancelada`; nesta fase reflete RN-08 |

---

## Resumo de Endpoints

| Método | Path | Descrição | Auth | Status Possíveis |
|--------|------|-----------|------|-----------------|
| `GET` | `/v1/reservations/{reservationId}` | Consultar reserva por identificador | ❌ (Fase 0 sem auth) | 200, 400, 404, 500 |

---

## Endpoints Detalhados

### `GET /v1/reservations/{reservationId}` — Consultar reserva por identificador

**Propósito:** Retornar os dados congelados de uma Reservation, seu estado atual do ciclo de vida e a
situação observável da saga de pagamento, permitindo acompanhar o desfecho de F01/F03/F04 sem acesso
direto ao banco. Operação somente leitura.
**Consumido por:** Frontend de teste — tela de detalhes da Reservation; autor/arquiteto em estudo
(consulta manual/curl).

#### Path Parameters

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `reservationId` | UUID | ✅ | Identificador único da Reservation, recebido na resposta de F01 |

#### Response 200 — Reservation solicitada, pagamento pendente

```json
{
  "id": "8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e",
  "accommodationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "guestReference": "guest-marina-alves",
  "checkIn": "2026-10-10",
  "checkOut": "2026-10-13",
  "guestsCount": 2,
  "status": "solicitada",
  "pricePerNight": "350.00",
  "currency": "BRL",
  "totalAmount": "1050.00",
  "createdAt": "2026-09-12T14:22:00Z",
  "sagaStatus": "pendente",
  "correlationId": "b2c4e6a8-1234-4abc-9def-0123456789ab",
  "cancellationReason": null
}
```

#### Response 200 — Reservation confirmada, pagamento autorizado

```json
{
  "id": "8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e",
  "accommodationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "guestReference": "guest-marina-alves",
  "checkIn": "2026-10-10",
  "checkOut": "2026-10-13",
  "guestsCount": 2,
  "status": "confirmada",
  "pricePerNight": "350.00",
  "currency": "BRL",
  "totalAmount": "1050.00",
  "createdAt": "2026-09-12T14:22:00Z",
  "sagaStatus": "autorizado",
  "correlationId": "b2c4e6a8-1234-4abc-9def-0123456789ab",
  "cancellationReason": null
}
```

#### Response 200 — Reservation cancelada, pagamento rejeitado

```json
{
  "id": "8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e",
  "accommodationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "guestReference": "guest-marina-alves",
  "checkIn": "2026-10-10",
  "checkOut": "2026-10-13",
  "guestsCount": 2,
  "status": "cancelada",
  "pricePerNight": "350.00",
  "currency": "BRL",
  "totalAmount": "1050.00",
  "createdAt": "2026-09-12T14:22:00Z",
  "sagaStatus": "rejeitado",
  "correlationId": "b2c4e6a8-1234-4abc-9def-0123456789ab",
  "cancellationReason": "Pagamento rejeitado pela simulação de Payment."
}
```

#### Erros Possíveis

| Código HTTP | code | Quando ocorre |
|-------------|------|---------------|
| 400 | `VALIDATION_ERROR` | `reservationId` ausente, vazio ou fora do formato UUID esperado |
| 404 | `RESERVATION_NOT_FOUND` | Nenhuma Reservation existe com o `reservationId` informado |
| 500 | `INTERNAL_ERROR` | Erro interno inesperado — verificar `traceId` nos logs |

**Nota importante:** 400 e 404 são conceitualmente distintos e nunca se sobrepõem (RF-01 do PRD) — 400
é entrada malformada (nem chega a buscar um registro); 404 é entrada bem formada sem registro
correspondente.

---

## Schemas de Entidades

### ReservationDetail

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|-------|------|-------------|----------|-----------|
| `id` | UUID | ✅ | ❌ | Identificador único da Reservation |
| `accommodationId` | UUID | ✅ | ❌ | Identificador da Accommodation reservada (Catalog) |
| `guestReference` | string | ✅ | ❌ | Guest de referência informado na solicitação (PD-002) |
| `checkIn` | date | ✅ | ❌ | Data de check-in congelada na solicitação |
| `checkOut` | date | ✅ | ❌ | Data de check-out congelada na solicitação |
| `guestsCount` | integer | ✅ | ❌ | Quantidade de hóspedes congelada na solicitação |
| `status` | enum | ✅ | ❌ | `solicitada`, `confirmada`, `cancelada` |
| `pricePerNight` | string (decimal) | ✅ | ❌ | Preço por noite congelado (RN-05) |
| `currency` | enum | ✅ | ❌ | `BRL` (PD-001) |
| `totalAmount` | string (decimal) | ✅ | ❌ | Valor total congelado (RN-06) |
| `createdAt` | datetime | ✅ | ❌ | Data e hora de criação da Reservation (ISO 8601) |
| `sagaStatus` | enum | ✅ | ❌ | `pendente`, `autorizado`, `rejeitado` — situação observável da saga |
| `correlationId` | UUID | ✅ | ❌ | Identificador de correlação da Reservation Saga (DP-02) |
| `cancellationReason` | string | ✅ | ✅ | Motivo do cancelamento; presente apenas quando `status = cancelada` |

---

## Códigos de Erro

| HTTP | code | Descrição |
|------|------|-----------|
| 400 | `VALIDATION_ERROR` | `reservationId` inválido (ausente, vazio ou fora do formato UUID) |
| 404 | `RESERVATION_NOT_FOUND` | Nenhuma Reservation existe com o identificador informado |
| 500 | `INTERNAL_ERROR` | Erro interno — verificar `traceId` nos logs |

### Formato Padrão de Erro (RFC 9457 — Problem Details)

```json
{
  "type": "https://localize-stay.lab/problems/reservation-not-found",
  "title": "Reservation não encontrada",
  "status": 404,
  "detail": "Nenhuma Reservation existe com o identificador informado.",
  "instance": "/v1/reservations/8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e",
  "code": "RESERVATION_NOT_FOUND"
}
```

`Content-Type: application/problem+json` em toda resposta de erro — mesma convenção do contrato de
Booking F01 (`tasks/prd-solicitacao-reserva/api-contract.yaml`).

---

## Como usar este contrato

### Backend
Implemente o endpoint exatamente conforme descrito. Consulte `x-backend-notes` no YAML: `sagaStatus`
deve ser derivado da mesma leitura consistente que produz `status`, evitando combinações inválidas
(ex.: `confirmada` com `sagaStatus=pendente`).

### Frontend
1. Gere tipos TypeScript a partir dos schemas:
   ```bash
   npx openapi-typescript tasks/prd-consulta-reserva/api-contract.yaml -o src/types/api-consulta-reserva.ts
   ```
2. Use o Prism para mockar a API durante desenvolvimento:
   ```bash
   npx @stoplight/prism-cli mock tasks/prd-consulta-reserva/api-contract.yaml
   # API mock disponível em http://localhost:4010
   ```
3. Distinga sempre 400 de 404 na UI (ver `x-frontend-notes` no YAML) e exiba `cancellationReason`
   somente quando `status = cancelada`.

### Testes de Contrato
```bash
npx dredd tasks/prd-consulta-reserva/api-contract.yaml http://localhost:5000
```

---

## Questões em Aberto

- **Composição de `cancellationReason`**: nesta fase, o único motivo possível é a rejeição de pagamento
  (RN-08); o texto exato retornado por Payment ainda não foi definido em contrato próprio de Payment —
  a TechSpec de F02 deve decidir se `cancellationReason` é um texto livre repassado por Payment ou uma
  mensagem fixa de Booking. Não bloqueia esta entrega.
- **Tipo de `correlationId`**: assumido como UUID por analogia aos demais identificadores do domínio;
  se o contrato AsyncAPI da saga (ainda não escrito) definir outro formato, este campo deve ser
  ajustado em conjunto.
- Demais questões em aberto do PRD (listagem por Guest, formato de identificador) já estão registradas
  em `tasks/prd-consulta-reserva/prd.md` e não afetam este contrato.
