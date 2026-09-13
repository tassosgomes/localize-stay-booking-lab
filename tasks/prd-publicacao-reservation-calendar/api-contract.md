# Data Contract — `reservation_calendar_v1` (Booking F05)

> **Gerado a partir de:** `tasks/prd-publicacao-reservation-calendar/prd.md`  
> **Data:** 2026-09-12  
> **Status:** Em Revisão  
> **Versão do contrato:** v1  
> **Envelope de tooling:** OpenAPI 3.1, schema-only — não há API HTTP nesta feature

Este contrato descreve o snapshot atual das Reservations terminais publicado por Booking. O YAML
correspondente é a fonte de verdade dos tipos; este documento é a representação legível para revisão.
O contrato durável, previsto para consumidores e catalogação, está em
[`contracts/data-contracts/reservation_calendar_v1.md`](../../contracts/data-contracts/reservation_calendar_v1.md).

## Premissas e decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Tipo de integração | Data Contract de view/materialized view | Baseline §Padrões de Comunicação e Vision §3; não é API nem evento |
| Dataset | `reservation_calendar_v1` | Nome aprovado no PRD e no Domain Map |
| Snapshot | Estado terminal atual, uma linha por Reservation | DP-02; não publicar histórico de transições |
| Elegibilidade | `confirmada` e `cancelada` | DP-01; `solicitada` não cria ocupação nem entra no dataset |
| Chave estável | `reservation_id` (UUID) | DP-02; evita depender de chave interna diferente |
| Período | `[check_in, check_out)` | RN-02; check-in inclusivo e check-out exclusivo |
| Nomenclatura | `snake_case` nas colunas | Interface SQL; segue as colunas PostgreSQL do Booking, sem alterar APIs JSON |
| Schema de origem | `booking` / entidade `Reservation` | Booking é dono do estado da Reservation |
| Schema publicado | `integration` / objeto `reservation_calendar_v1` | Baseline; consumidores não leem `booking.*` |
| Acesso | Somente leitura; `catalog_role`, `booking_role` e `payment_role` têm `SELECT` em `integration` | `db/bootstrap/004-grants.sql`; consumidores não escrevem no dataset |
| Atualização | Visível na próxima leitura bem-sucedida após persistência terminal | DP-03; sem lote diário, ação manual ou push |
| Auditoria | `created_at` e `updated_at` obrigatórios | `contracts/data-contracts/TEMPLATE.md`; `updated_at` é gravado no momento da transição terminal e não no refresh/leitura |
| Compatibilidade | Mudança incompatível cria `reservation_calendar_v2` | Baseline; v1 permanece disponível |

## Resumo de endpoints

F05 não cria endpoint HTTP, portanto não há operações, parâmetros, paginação, autenticação HTTP ou
status de resposta. A interface é consumida como dataset publicado e contratado.

| Método | Path | Descrição | Auth | Status |
|---|---|---|---|---|
| — | — | Nenhuma operação HTTP; leitura do dataset `integration.reservation_calendar_v1` | N/A — role PostgreSQL | N/A |

## Interface publicada

**Propósito:** permitir que consumidores autorizados consultem os períodos e estados terminais das
Reservations sem acesso às tabelas internas de Booking.

**Publicado por:** Booking, sob ownership lógico do domínio e com lineage a partir de `booking.Reservation`.

**Consumido por:** consumidores futuros, como a Busca da Fase 2. Catalog não usa este dataset para
validar disponibilidade ou criar Availability Block; seu fluxo de Fase 0 continua baseado na API e
nos eventos próprios.

**Formato físico:** view ou materialized view no schema PostgreSQL `integration`, com o nome
`reservation_calendar_v1`. A escolha exata e o mecanismo de atualização pertencem à TechSpec.

**Acesso:** leitura somente. As roles de serviço existentes possuem `USAGE` no schema e `SELECT`
nas tabelas de `integration`; nenhuma role consumidora recebe `CREATE` ou escrita nesse schema.

### Exemplo de snapshot

O exemplo abaixo é uma serialização legível das linhas; não representa um response HTTP.

```json
[
  {
    "reservation_id": "8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e",
    "accommodation_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "check_in": "2026-10-10",
    "check_out": "2026-10-13",
    "status": "confirmada",
    "created_at": "2026-09-12T14:22:00Z",
    "updated_at": "2026-09-12T14:22:05Z"
  },
  {
    "reservation_id": "0d7f2c9a-6b2e-4c17-9d55-1e8d12f3a401",
    "accommodation_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "check_in": "2026-11-02",
    "check_out": "2026-11-05",
    "status": "cancelada",
    "created_at": "2026-09-13T09:10:00Z",
    "updated_at": "2026-09-13T09:10:04Z"
  }
]
```

Uma leitura sem Reservations elegíveis retorna `[]`. A linha `confirmada` representa ocupação ativa;
`cancelada` permanece visível como desfecho terminal, mas não bloqueia datas. Uma Reservation
`solicitada` não possui linha.

## Schema de entidade

### `ReservationCalendarRecord`

| Campo | Tipo | Obrigatório | Nulo | Descrição |
|---|---|---|---|---|
| `reservation_id` | `uuid` | Sim | Não | Chave estável e única da Reservation publicada |
| `accommodation_id` | `uuid` | Sim | Não | Accommodation referenciada pela Reservation |
| `check_in` | `date` | Sim | Não | Início inclusivo do período |
| `check_out` | `date` | Sim | Não | Fim exclusivo do período; posterior a `check_in` |
| `status` | `varchar(12)` enum | Sim | Não | `confirmada` (ocupação ativa) ou `cancelada` (sem ocupação); `solicitada` é excluída |
| `created_at` | `timestamptz` | Sim | Não | Criação da Reservation em UTC |
| `updated_at` | `timestamptz` | Sim | Não | Instante UTC da transição persistida para `confirmada` ou `cancelada`; estável após reprocessamentos |

### Estados permitidos

| Estado | Significado para o consumidor |
|---|---|
| `confirmada` | Estado terminal que representa ocupação ativa no período `[check_in, check_out)` |
| `cancelada` | Estado terminal sem ocupação ativa; a linha permanece para distinguir cancelamento de ausência |

`solicitada` não é um valor permitido no dataset. Estados terminais não regridem nem alternam entre
si, conforme RN-10 e RN-11.

## Regras de qualidade verificáveis

| Regra | Severidade | Verificação esperada |
|---|---|---|
| `reservation_id` é único e não nulo | Bloqueante | Contagem por chave não retorna duplicatas; `COUNT(*) = COUNT(reservation_id)` |
| Cada Reservation terminal tem exatamente uma linha | Bloqueante | Comparar o snapshot com Reservations `confirmada`/`cancelada` |
| Nenhuma Reservation `solicitada` aparece | Bloqueante | Consulta de anti-join não retorna linhas |
| `status` pertence a `{confirmada, cancelada}` | Bloqueante | Constraint/view check e teste de contrato |
| `check_out > check_in` | Bloqueante | Validação do intervalo em todas as linhas |
| O período preserva `[check_in, check_out)` | Bloqueante | Comparar datas publicadas com Reservation de origem |
| Mudança terminal aparece na próxima leitura bem-sucedida | Bloqueante | Teste de transição persistida para confirmação e cancelamento |
| Não há colunas de Guest, Payment, preço, moeda, correlation/causation ID ou Saga | Bloqueante | Allow-list de schema e inspeção de metadados |
| Não há regressão ou alternância de estado terminal | Bloqueante | Repetir publicação/leitura após entradas duplicadas ou fora de ordem |
| `updated_at` representa a transição terminal e não muda em reprocessamentos | Bloqueante | Comparar o timestamp antes e depois de duplicidades ou mensagens fora de ordem |

## Códigos de erro

Não há códigos HTTP nem payload de erro neste contrato, porque F05 não define uma operação de
transporte. Falhas de acesso, indisponibilidade do banco e violações de qualidade são condições
operacionais/testes de contrato; não são colunas do dataset nem respostas públicas padronizadas por F05.

| Situação | Tratamento contratual |
|---|---|
| Role sem `SELECT` | Acesso deve ser negado pela infraestrutura; conceder acesso exige decisão de governança |
| Dataset indisponível | Leitura falha operacionalmente; não produzir um snapshot parcial como se fosse válido |
| Violação de qualidade | Marcar a validação como bloqueante e corrigir a publicação antes de liberar consumidores |

## Dados deliberadamente ausentes

O contrato publica somente o mínimo necessário para um calendário. Estão fora da allow-list:

- Guest ou qualquer referência de identidade pessoal;
- preço por noite, total, moeda ou Payment;
- `correlationId`, `causationId` e detalhes técnicos da Reservation Saga;
- motivo técnico de rejeição, eventos, histórico de transições e auditoria operacional;
- dados de Property/Accommodation além da referência `accommodation_id`;
- qualquer chave, nome de tabela ou detalhe interno não listado no schema.

## Questões em aberto

- [ ] Confirmar na TechSpec se a publicação será uma view ou materialized view.
- [ ] Definir o mecanismo físico de atualização que persiste `updated_at` na mesma transação da transição terminal.
- [ ] Definir a role de implantação/publicação sem conceder escrita às roles consumidoras.
- [ ] Definir o responsável nominal pelo ativo no OpenMetadata; o owner de negócio é Booking.
- [ ] Confirmar a política de retenção/arquivamento para além do snapshot atual; exclusão não faz parte da Fase 0.
- [ ] Definir grants para consumidores externos futuros sem alterar a leitura atual das roles de serviço.

## Como usar este contrato

### Backend

Use a skill `tsg-flow-techspec-creator` referenciando
[`api-contract.yaml`](./api-contract.yaml) como input adicional. A TechSpec deve fechar view versus
materialized view, atualização, grants, qualidade e catalogação no OpenMetadata sem ampliar a allow-list.

### Frontend e consumidores

Use a skill `tsg-flow-frontend-techspec-creator` referenciando este contrato quando um consumidor de
UI ou busca for criado. Os schemas do YAML são a fonte de verdade para os tipos; não implemente busca,
paginação ou polling que não estejam no contrato.

### Mocks

Prism não se aplica: o YAML não declara endpoints HTTP. Para desenvolvimento, use uma fixture de
dataset que respeite `ReservationCalendarSnapshot` e valide as regras de qualidade acima.

## Validação

- **Lint Spectral executado:** `npx --yes @stoplight/spectral-cli lint tasks/prd-publicacao-reservation-calendar/api-contract.yaml --ruleset .agents/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error`.
- **Resultado:** passou com 0 erros e 0 warnings (`No results with a severity of 'error' found!`).
- O ruleset utilizado é `.agents/skills/tsg-flow-contract-creator/rulesets/openapi.yaml`, que estende
  `spectral:oas` e valida especificamente OpenAPI 3.1 e exemplos em arrays nos Schema Objects.
