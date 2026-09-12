# API Contract — Solicitação de Reserva (Booking F01)

> **Gerado a partir de:** `tasks/prd-solicitacao-reserva/prd.md`
> **Data:** 2026-09-12
> **Status:** Em Revisão
> **Versão do contrato:** 1.0.0
> **YAML técnico:** [`api-contract.yaml`](./api-contract.yaml)

---

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---------|---------|--------|
| Autenticação | Nenhuma na Fase 0 (`security: []` em toda operação) | `context/architecture-baseline.md` — Fase 0 não implementa autenticação/autorização |
| Paginação | Não se aplica | RF-01 tem apenas uma operação de criação, sem listagem |
| Formato de datas | `checkIn`/`checkOut` como data (`YYYY-MM-DD`); `createdAt` como data-hora ISO 8601 UTC | Período `[check-in, check-out)` é por dia calendário (RN-02); demais timestamps seguem ISO 8601 |
| Valores monetários | String decimal com 2 casas (ex.: `"350.00"`) | Decisão do usuário nesta sessão — evita ambiguidade de ponto flutuante e é legível no frontend de teste |
| Moeda | `BRL` fixa | PD-001 — moeda única do laboratório |
| Identificadores | `accommodationId` como UUID; `guestReference` (Guest de referência) como string livre sem formato | Decisão do usuário nesta sessão + PD-002 (sem validação de identidade real) |
| Nomenclatura de campos | `camelCase` | Padrão adotado por não haver convenção prévia no projeto (primeiro contrato) |
| Paths | Inglês, plural, kebab-case (`/reservations`, `/accommodations/{id}/availability-check`) | Skill `restful-api` (convenção obrigatória do projeto) |
| Versionamento | Prefixo `/v1` embutido em `servers.url` | `context/architecture-baseline.md` — versionamento por prefixo de rota; mesmo padrão do template da skill |
| Formato de erro | RFC 9457 (Problem Details), `Content-Type: application/problem+json`, com extensão `code` | Skill `restful-api` (convenção obrigatória do projeto) — não havia padrão de erro definido antes |
| Distinção rejeição de negócio vs. falha de infraestrutura | `422` para regra de negócio, `503` para falha ao consultar Catalog | Decisão do usuário nesta sessão — RF-01 exige que a falha de Catalog não seja tratada como rejeição |
| Escopo do contrato | Inclui a API pública de Booking **e** um contrato mínimo/provisório do endpoint síncrono de Catalog | Decisão do usuário nesta sessão — Catalog F04 (Consulta de Disponibilidade) ainda não tem PRD/contrato próprio, mas Booking F01 depende dele (RN-04) |

---

## Resumo de Endpoints

| Método | Path | Descrição | Auth | Consumido por | Status Possíveis |
|--------|------|-----------|------|----------------|-------------------|
| `POST` | `/v1/reservations` | Solicitar reserva | ❌ (Fase 0) | Frontend de teste (Guest) | 201, 400, 422, 503, 500 |
| `GET` | `/v1/accommodations/{accommodationId}/availability-check` | Verificar disponibilidade (dependência de Catalog) | ❌ (Fase 0) | Booking (backend-to-backend) | 200, 404, 500 |

---

## Endpoints Detalhados

### `POST /v1/reservations` — Solicitar reserva

**Propósito:** Guest solicita uma Reservation para uma Accommodation, informando período e número de
hóspedes. Booking valida sincronamente contra Catalog e, se válida, cria a Reservation em `solicitada`
com preço, moeda e valor total congelados.
**Consumido por:** Frontend de teste — formulário de solicitação de reserva.

#### Request Body

```json
{
  "accommodationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "guestReference": "guest-marina-alves",
  "checkIn": "2026-10-10",
  "checkOut": "2026-10-13",
  "guestsCount": 2
}
```

#### Response 201

Header `Location` aponta para a URI da Reservation criada (consulta entregue por F02, fora deste contrato).

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
  "createdAt": "2026-09-12T14:22:00Z"
}
```

#### Erros Possíveis

| Código HTTP | `code` | Quando ocorre |
|-------------|--------|----------------|
| 400 | `VALIDATION_ERROR` | Requisição malformada — campo obrigatório ausente ou fora do formato |
| 422 | `PERIODO_INVALIDO` | Check-out não é posterior ao check-in |
| 422 | `QUANTIDADE_HOSPEDES_INVALIDA` | Número de hóspedes ≤ 0 |
| 422 | `ACOMODACAO_INDISPONIVEL` | Accommodation não existe, ou Property/Accommodation inativa |
| 422 | `CAPACIDADE_EXCEDIDA` | Número de hóspedes excede a capacidade máxima informada por Catalog |
| 422 | `PERIODO_INDISPONIVEL` | Accommodation indisponível em algum trecho do período |
| 503 | `CATALOG_INDISPONIVEL` | Validação síncrona com Catalog não pôde ser concluída (retryable, não é rejeição) |
| 500 | `INTERNAL_ERROR` | Erro interno inesperado |

---

### `GET /v1/accommodations/{accommodationId}/availability-check` — Verificar disponibilidade (dependência de Catalog)

**Propósito:** Endpoint síncrono mínimo de Catalog, consumido internamente por Booking para obter os
fatos necessários (status ativo, capacidade, disponibilidade no período, preço vigente) e aplicar suas
próprias regras (RN-02 a RN-06). **Contrato provisório** — Catalog F04 (Consulta de Disponibilidade)
ainda não tem PRD/contrato próprio; este endpoint destrava a TechSpec de Booking F01 e deve ser
substituído quando Catalog especificar sua feature.
**Consumido por:** Booking (chamada backend-to-backend, não exposta ao frontend de teste).

#### Path Parameters

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `accommodationId` | UUID | Identificador único da Accommodation |

#### Query Parameters

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `checkIn` | date | Sim | Data de check-in (início, inclusivo) |
| `checkOut` | date | Sim | Data de check-out (fim, exclusivo) |
| `guestsCount` | integer | Sim | Número de hóspedes, para checagem de capacidade |

#### Response 200

```json
{
  "accommodationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "active": true,
  "maxGuests": 4,
  "availableForPeriod": true,
  "pricePerNight": "350.00",
  "currency": "BRL"
}
```

#### Erros Possíveis

| Código HTTP | `code` | Quando ocorre |
|-------------|--------|----------------|
| 404 | `ACCOMMODATION_NOT_FOUND` | Nenhuma Accommodation existe com o identificador informado |
| 500 | `INTERNAL_ERROR` | Erro interno inesperado em Catalog |

> Nota: os `code` deste endpoint pertencem ao vocabulário de erro de Catalog, não ao de Booking. É
> responsabilidade de Booking traduzir estes resultados (incluindo o 404) para seus próprios `code`
> de negócio (ex.: `ACOMODACAO_INDISPONIVEL`) na resposta de `POST /v1/reservations`.

---

## Schemas de Entidades

### Reservation

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|-------|------|-------------|----------|-----------|
| `id` | UUID | ✅ | ❌ | Identificador único da Reservation |
| `accommodationId` | UUID | ✅ | ❌ | Accommodation reservada (referência a Catalog) |
| `guestReference` | string | ✅ | ❌ | Guest de referência, sem validação de identidade (PD-002) |
| `checkIn` | date | ✅ | ❌ | Início do período (inclusivo) |
| `checkOut` | date | ✅ | ❌ | Fim do período (exclusivo) |
| `guestsCount` | integer | ✅ | ❌ | Quantidade de hóspedes |
| `status` | enum | ✅ | ❌ | `solicitada`, `confirmada`, `cancelada` (apenas `solicitada` é produzida por F01) |
| `pricePerNight` | string (decimal) | ✅ | ❌ | Preço por noite congelado no momento da solicitação |
| `currency` | enum | ✅ | ❌ | Sempre `BRL` |
| `totalAmount` | string (decimal) | ✅ | ❌ | Noites × preço por noite, congelado |
| `createdAt` | datetime | ✅ | ❌ | Data de criação (ISO 8601) |

### AvailabilityCheckResponse (Catalog — dependência)

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|-------|------|-------------|----------|-----------|
| `accommodationId` | UUID | ✅ | ❌ | Identificador da Accommodation consultada |
| `active` | boolean | ✅ | ❌ | Property e Accommodation ambas ativas (Catalog RN-02) |
| `maxGuests` | integer | ✅ | ❌ | Capacidade máxima (Catalog RN-03) |
| `availableForPeriod` | boolean | ✅ | ❌ | Sem Availability Block sobreposto ao período (Catalog RN-04) |
| `pricePerNight` | string (decimal) | ✅ | ❌ | Preço por noite vigente (Catalog RN-06) |
| `currency` | enum | ✅ | ❌ | Sempre `BRL` |

---

## Códigos de Erro

| HTTP | `code` | Descrição |
|------|--------|-----------|
| 400 | `VALIDATION_ERROR` | Requisição malformada (não é regra de negócio) |
| 404 | `ACCOMMODATION_NOT_FOUND` | (Catalog, dependência) Accommodation inexistente |
| 422 | `PERIODO_INVALIDO` | Check-out não posterior ao check-in |
| 422 | `QUANTIDADE_HOSPEDES_INVALIDA` | Hóspedes ≤ 0 |
| 422 | `ACOMODACAO_INDISPONIVEL` | Accommodation inexistente ou inativa |
| 422 | `CAPACIDADE_EXCEDIDA` | Hóspedes acima da capacidade máxima |
| 422 | `PERIODO_INDISPONIVEL` | Indisponibilidade em algum trecho do período |
| 503 | `CATALOG_INDISPONIVEL` | Falha ao validar sincronamente com Catalog — retryable, não é rejeição |
| 500 | `INTERNAL_ERROR` | Erro interno inesperado |

### Formato Padrão de Erro (RFC 9457 — Problem Details)

`Content-Type: application/problem+json`

```json
{
  "type": "https://localize-stay.lab/problems/periodo-invalido",
  "title": "Período inválido",
  "status": 422,
  "detail": "A data de check-out deve ser posterior à data de check-in.",
  "instance": "/v1/reservations",
  "code": "PERIODO_INVALIDO"
}
```

`code` é uma extensão de RFC 9457 usada neste projeto: campo estável e legível por máquina para o
frontend decidir como reagir, sem parsear `title`/`detail`.

---

## Como usar este contrato

### Backend (Booking)
Use a skill `tsg-flow-techspec-creator` referenciando este contrato como input adicional. Implemente
`POST /v1/reservations` exatamente conforme descrito; veja `x-backend-notes` no YAML para o requisito de
timeout na chamada síncrona a Catalog.

### Backend (Catalog)
O endpoint `GET /v1/accommodations/{accommodationId}/availability-check` é um contrato provisório —
quando Catalog F04 ganhar seu próprio PRD, formalize-o com sua própria skill de contrato e atualize esta
referência.

### Frontend
Use a skill `tsg-flow-frontend-techspec-creator` referenciando este contrato — os schemas são a fonte de
verdade para os tipos. Gere tipos TypeScript:
```bash
npx openapi-typescript tasks/prd-solicitacao-reserva/api-contract.yaml -o src/types/booking-api.ts
```
Ou rode um mock imediato com Prism:
```bash
npx @stoplight/prism-cli mock tasks/prd-solicitacao-reserva/api-contract.yaml
# API mock disponível em http://localhost:4010
```

### Testes de Contrato
```bash
npx dredd tasks/prd-solicitacao-reserva/api-contract.yaml http://localhost:5000
```

---

## Questões em Aberto

- [ ] Catalog F04 (Consulta de Disponibilidade) ainda não tem PRD nem contrato próprio; o endpoint
  `GET /v1/accommodations/{accommodationId}/availability-check` aqui é um rascunho mínimo apenas para
  destravar a TechSpec de Booking F01 — precisa ser ratificado ou substituído quando Catalog especificar
  essa feature.
- [ ] Este é o primeiro contrato OpenAPI do projeto — não havia convenção prévia de `camelCase`, formato
  de erro ou representação monetária; as escolhas aqui (RFC 9457, string decimal, UUID) tornam-se o
  precedente para os próximos contratos (Catalog, Payment) e devem ser reutilizadas, não reabertas.
- [ ] PD-001 e PD-002 estão registrados como `Accepted` (aprovados junto ao PRD); nenhuma pendência
  bloqueia este contrato.
