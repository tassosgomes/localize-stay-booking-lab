# Data Contract — `reservation_calendar_v1`

> Contrato durável da feature Booking F05. A especificação tipada e validável
> está em [`tasks/prd-publicacao-reservation-calendar/api-contract.yaml`](../../tasks/prd-publicacao-reservation-calendar/api-contract.yaml).

## 1. Identificação

| Campo | Valor |
|-------|-------|
| Nome do dataset | `reservation_calendar_v1` |
| Versão | `v1` |
| Domínio dono | `booking` |
| Time responsável | Domínio Booking — responsável nominal a definir |
| Schema PostgreSQL de origem | `booking` (`Reservation`) |
| Schema de exposição | `integration` |
| Estado | `rascunho` |

## 2. Descrição

`reservation_calendar_v1` é o snapshot atual das Reservations que chegaram a um estado terminal.
Cada Reservation `confirmada` ou `cancelada` aparece uma única vez com a Accommodation de referência
e o período `[check_in, check_out)`; Reservations `solicitada` não aparecem. Uma linha cancelada
permanece identificável, mas não representa ocupação ativa.

## 3. Schema

| Campo | Tipo | Nulo? | Descrição |
|-------|------|-------|-----------|
| `reservation_id` | `uuid` | `NÃO` | Chave estável e única da Reservation publicada. |
| `accommodation_id` | `uuid` | `NÃO` | Accommodation referenciada; a verdade da entidade pertence ao Catalog. |
| `check_in` | `date` | `NÃO` | Início inclusivo da estadia. |
| `check_out` | `date` | `NÃO` | Fim exclusivo da estadia; deve ser posterior a `check_in`. |
| `status` | `varchar(12)` | `NÃO` | Enum contratado: `confirmada` ou `cancelada`; `solicitada` é excluída. |
| `created_at` | `timestamptz` | `NÃO` | Instante UTC de criação da Reservation. |
| `updated_at` | `timestamptz` | `NÃO` | Instante UTC da transição persistida para `confirmada` ou `cancelada`; permanece estável após reprocessamentos. |

Regras:

- A chave primária lógica é `reservation_id` e é estável entre versões.
- `status = confirmada` é a única condição que representa ocupação ativa.
- O período sempre usa a semântica `[check_in, check_out)`.
- Campos de auditoria obrigatórios: `created_at timestamptz NOT NULL` e `updated_at timestamptz NOT NULL`.
- Não há dados pessoais (PII), Guest, Payment, preço, moeda, IDs de correlação/causação ou detalhes da Saga.
- O dataset retorna uma coleção vazia como `[]` quando não há linhas elegíveis; nunca `null`.

## 4. Qualidade (expectativas verificáveis)

| Regra | Severidade | Como verificar |
|-------|-----------|----------------|
| `reservation_id` único e não nulo | bloqueante | Constraint/view check e contagem por chave sem duplicatas. |
| Uma linha por Reservation terminal | bloqueante | Comparar com as Reservations `confirmada`/`cancelada` persistidas. |
| Nenhuma Reservation `solicitada` | bloqueante | Anti-join entre `booking.reservations` e o dataset. |
| `status` em `{confirmada, cancelada}` | bloqueante | Validação do enum e teste de contrato. |
| `check_out > check_in` | bloqueante | Validação do intervalo em todas as linhas. |
| Período e Accommodation fiéis à Reservation | bloqueante | Comparação da projeção com a origem `booking.Reservation`. |
| Atualização após transição terminal persistida | bloqueante | Cenários de confirmação/cancelamento e leitura seguinte bem-sucedida. |
| `updated_at` é o instante da transição e não muda em reprocessamentos | bloqueante | Comparar o timestamp antes/depois de entradas duplicadas ou fora de ordem. |
| Estados terminais monotônicos | bloqueante | Repetição com entradas duplicadas, fora de ordem ou conflitantes. |
| Allow-list sem campos proibidos | bloqueante | Inspeção de colunas e validação contra o schema deste documento. |

## 5. Acesso

| Campo | Valor |
|-------|-------|
| Leitura concedida a (roles) | `catalog_role`, `booking_role` e `payment_role` (`SELECT` no schema `integration`); consumidores externos exigem grant explícito futuro. |
| Escrita concedida a (role) | Nenhuma role consumidora. O schema `integration` permanece sem role de escrita de serviço; a role de implantação/publicação será definida na TechSpec. |
| Formato de exposição | View ou materialized view `integration.reservation_calendar_v1`. |
| Frequência de atualização | Após a persistência do estado terminal, visível na próxima leitura bem-sucedida; sem lote diário, push ou republicação manual. |
| Ownership | Booking é owner de negócio e da lineage; o owner técnico nominal no catálogo ainda será definido. |

Consumidores não acessam `booking.*`, não escrevem no dataset e não usam este ativo para validar
disponibilidade síncrona nem para criar/remover Availability Blocks.

## 6. Retenção e privacidade

- Retenção: snapshot atual enquanto a Reservation existir; exclusão, arquivamento e retenção comercial
  não fazem parte da Fase 0 e exigem decisão posterior.
- Dados sensíveis: nenhum. Guest, Payment, preço, moeda, IDs de correlação/causação e detalhes
  internos da Reservation Saga são deliberadamente excluídos.

## 7. Lineage e compatibilidade

- **Lineage:** `booking.Reservation` → projeção contratada `integration.reservation_calendar_v1`.
- `ReservationSaga` não é publicado; apenas determina, no domínio Booking, se o estado terminal foi alcançado.
- Mudanças compatíveis preservam nome, tipos, semântica e allow-list de v1.
- Mudanças incompatíveis devem criar `reservation_calendar_v2` ao lado de v1, mantendo v1 para consumidores existentes.

## 8. Histórico de versões

| Versão | Data | Mudança | Compatível? |
|--------|------|---------|-------------|
| `v1` | `2026-09-12` | Criação do contrato do snapshot terminal de Reservations. | — |

## Questões em aberto

- Confirmar view versus materialized view e o mecanismo de atualização.
- Definir o mecanismo físico que persiste `updated_at` na mesma transação da transição terminal.
- Definir role de implantação/publicação e o responsável nominal no OpenMetadata.
- Confirmar a política de retenção e os grants para consumidores externos futuros.
