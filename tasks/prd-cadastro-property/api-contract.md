# API Contract — Cadastro de Property

> **Gerado a partir de:** `tasks/prd-cadastro-property/prd.md`  
> **Data:** 2026-09-12  
> **Status:** Em revisão  
> **Versão do contrato:** 1.0.0

---

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Autenticação | Nenhuma na Fase 0 | Baseline arquitetural e PD-002 adiam identidade real. |
| Identificação do ator | Header `X-Host-Reference-Id` obrigatório, UUID fictício | Separa o solicitante dos dados editáveis e permite exercitar ownership sem autenticação. |
| Paginação e filtros | Não aplicável | F01 não lista nem consulta Properties; consulta pertence à F03. |
| Formato de erros | RFC 9457 `ProblemDetails`, com `code`, `details` e `traceId` | Mantém erro legível e tratamento programático consistente. |
| Nomenclatura | Campos `camelCase` e paths plurais | Convenção confirmada para o contrato. |
| Versionamento | Prefixo `/v1` | Herdado do baseline; breaking changes exigem nova versão publicada em paralelo. |
| Identificadores | UUID | Evita expor chaves sequenciais internas e fornece referência estável. |
| Limites | `name`: 120; `location`: 500 caracteres | Limites confirmados para tornar as validações do PRD observáveis. |
| Atualização | `PATCH` parcial e atômico | RF-02 permite alterar nome e/ou localização preservando os demais dados. |

Não há uploads, webhooks, eventos assíncronos ou endpoints de saúde/métricas na F01. Arrays de detalhes de
erro retornam `[]`, nunca `null`.

---

## Resumo de Endpoints

| Método | Path | Descrição | Auth | Status possíveis |
|---|---|---|---|---|
| `POST` | `/v1/properties` | Cadastrar Property ativa | Nenhuma; Host fictício no header | 201, 400, 500 |
| `PATCH` | `/v1/properties/{propertyId}` | Editar nome e/ou localização | Nenhuma; ownership pelo header | 200, 400, 403, 404, 500 |

---

## Endpoints Detalhados

### `POST /v1/properties` — Cadastrar Property

**Propósito:** cadastrar uma hospedagem ativa, atribuindo ao Host informado no header a responsabilidade pelo
registro. Cadastros com o mesmo nome e a mesma localização de outra Property são aceitos.

**Consumido por:** frontend de teste — formulário de cadastro de hospedagem.

#### Headers

| Header | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `X-Host-Reference-Id` | UUID | Sim | Identificador fictício do Host solicitante; não autentica o ator. |

#### Request Body

```json
{
  "name": "Pousada Dunas do Sol",
  "location": "Cumbuco, Caucaia - CE"
}
```

#### Response 201

O header `Location` contém `/v1/properties/8ce29b2c-e67e-4a7d-bb3d-2ed87310f28a`.

```json
{
  "id": "8ce29b2c-e67e-4a7d-bb3d-2ed87310f28a",
  "name": "Pousada Dunas do Sol",
  "location": "Cumbuco, Caucaia - CE",
  "hostReferenceId": "421ec5d4-3ba2-4d8e-8958-a51dd0711650",
  "status": "active"
}
```

#### Erros Possíveis

| HTTP | `code` | Quando ocorre |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Header ausente/inválido, campo ausente, em branco, acima do limite ou propriedade desconhecida no JSON. |
| 500 | `INTERNAL_ERROR` | Falha inesperada; nenhuma criação parcial permanece. |

---

### `PATCH /v1/properties/{propertyId}` — Editar dados da Property

**Propósito:** alterar atomicamente o nome e/ou a localização de uma Property existente. `id`,
`hostReferenceId` e `status` não fazem parte do schema aceito e não podem ser alterados pela F01.

**Consumido por:** frontend de teste — formulário de edição iniciado com o identificador devolvido no cadastro.

#### Parâmetros e Headers

| Nome | Local | Tipo | Obrigatório | Descrição |
|---|---|---|---|---|
| `propertyId` | Path | UUID | Sim | Identificador estável da Property. |
| `X-Host-Reference-Id` | Header | UUID | Sim | Deve coincidir com o Host responsável persistido. |

#### Request Body

Envie ao menos um dos campos editáveis.

```json
{
  "name": "Pousada Dunas do Sol Boutique",
  "location": "Praia de Cumbuco, Caucaia - CE"
}
```

#### Response 200

```json
{
  "id": "8ce29b2c-e67e-4a7d-bb3d-2ed87310f28a",
  "name": "Pousada Dunas do Sol Boutique",
  "location": "Praia de Cumbuco, Caucaia - CE",
  "hostReferenceId": "421ec5d4-3ba2-4d8e-8958-a51dd0711650",
  "status": "active"
}
```

#### Erros Possíveis

| HTTP | `code` | Quando ocorre |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Identificador/header inválido, body vazio, campo em branco/acima do limite ou tentativa de enviar campo não editável. |
| 403 | `HOST_OWNERSHIP_FORBIDDEN` | O Host fictício informado não é o responsável atual. |
| 404 | `PROPERTY_NOT_FOUND` | Não existe Property com o identificador informado. |
| 500 | `INTERNAL_ERROR` | Falha inesperada; os dados anteriores são preservados. |

---

## Schemas de Entidades

### Property

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|---|---|---|---|---|
| `id` | UUID | Sim | Não | Identificador público e estável. |
| `name` | string (1–120) | Sim | Não | Nome com ao menos um caractere não branco. |
| `location` | string (1–500) | Sim | Não | Localização textual livre, sem geolocalização avançada. |
| `hostReferenceId` | UUID | Sim | Não | Identificador fictício do Host responsável. |
| `status` | enum | Sim | Não | `active`: utilizável; `inactive`: reservado à F07. |

### CreatePropertyRequest

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|---|---|---|---|---|
| `name` | string (1–120) | Sim | Não | Nome da hospedagem. |
| `location` | string (1–500) | Sim | Não | Localização textual livre. |

### UpdatePropertyRequest

| Campo | Tipo | Obrigatório | Nullable | Descrição |
|---|---|---|---|---|
| `name` | string (1–120) | Condicional | Não | Novo nome. Ao menos um campo do schema deve ser enviado. |
| `location` | string (1–500) | Condicional | Não | Nova localização. Ao menos um campo do schema deve ser enviado. |

---

## Códigos de Erro

| HTTP | `code` | Descrição |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Entrada ausente, malformada ou fora dos limites. |
| 403 | `HOST_OWNERSHIP_FORBIDDEN` | Host informado não pode editar a Property. |
| 404 | `PROPERTY_NOT_FOUND` | Property não encontrada. |
| 500 | `INTERNAL_ERROR` | Erro inesperado; correlacione pelo `traceId`. |

### Formato Padrão de Erro

O content type é `application/problem+json`.

```json
{
  "type": "https://localize-stay.example/problems/validation-error",
  "title": "Dados inválidos",
  "status": 400,
  "detail": "Corrija os campos indicados e tente novamente.",
  "instance": "/v1/properties",
  "code": "VALIDATION_ERROR",
  "details": [
    {
      "field": "name",
      "message": "O nome não pode conter apenas espaços."
    }
  ],
  "traceId": "4f75b9ff793eef884ad28e7d65c2832b"
}
```

---

## Questões em Aberto

Nenhuma decisão de contrato permanece aberta para a F01. Endereços e portas dos ambientes serão definidos na
configuração de implantação; o contrato usa o server relativo `/v1` para preservar o path versionado.

---

## Como usar este contrato

### Backend

Implemente as operações e validações conforme `api-contract.yaml`, incluindo atomicidade e ownership.

### Frontend

Gere os tipos TypeScript a partir dos schemas:

```bash
npx openapi-typescript api-contract.yaml -o src/types/api.ts
```

### Mock

```bash
npx @stoplight/prism-cli mock api-contract.yaml
```
