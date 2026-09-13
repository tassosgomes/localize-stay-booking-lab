# Resumo de Tarefas de Implementação — Publicação do dataset `reservation_calendar_v1` (Booking F05)

> **TechSpec de origem:** [`techspec.md`](techspec.md) (rev. 2026-09-13, Status: Aprovado, handoff `approved`)
> **PRD de origem:** [`prd.md`](prd.md)
> **API Contract:** [`api-contract.yaml`](api-contract.yaml) (OpenAPI 3.1 como envelope de schema, paths vazio, Status: Em Revisão — documento de referência, não alterado por esta implementação); [`api-contract.md`](api-contract.md)
> **ADRs pertinentes:** [ADR-004: view PostgreSQL ao vivo](../../docs/adr/adr-004-reservation-calendar-live-view.md), [ADR-005: persistência do instante da transição terminal](../../docs/adr/adr-005-terminal-transition-timestamp.md)
> **Baseline e domínio:** [`context/architecture-baseline.md`](../../context/architecture-baseline.md), [`domains/booking/domain.md`](../../domains/booking/domain.md)
> **Status do plano:** Em revisão
> **Regra de entrega:** a task de comportamento é uma fatia vertical validável isoladamente

## Visão Geral

F05 publica `integration.reservation_calendar_v1` como uma view PostgreSQL regular sobre
`booking.reservations`, expondo somente Reservations em estado terminal (`confirmada`/`cancelada`)
com sete colunas contratadas, sem histórico de transições e sem dados de Guest/Payment/Saga. Não há
endpoint HTTP, evento novo ou UI: a interface publicada é a própria relation SQL, protegida por
grants somente leitura e catalogada no OpenMetadata.

O plano tem duas fatias verticais sequenciais: V-01 publica a view e prova o contrato de dados
(elegibilidade, cardinalidade, período, timestamp estável, atualização pós-commit); V-02 prova a
proteção de acesso (grants/negação de escrita/acesso cruzado) e documenta a catalogação. Ambas
reutilizam PostgreSQL 16 real via Testcontainers, nunca o `postgres-main` do homelab.

### Pré-requisito externo (EN-01) — já resolvido em `tasks/prd-conclusao-saga`, não uma task deste plano

A TechSpec desta feature define um habilitador inevitável, EN-01: `booking.reservations` precisa de
uma coluna `terminal_transition_at` (gravada no mesmo commit da transição terminal por
`Reservation.Confirm(DateTime)`/`Cancel(string, DateTime)`), porque só o owner do aggregate
(Booking/F04) pode fornecer esse instante sem inventar um valor histórico. Como esse código pertence
à mesma superfície que F04 (`tasks/prd-conclusao-saga`) já ia criar (`Reservation.Confirm`/`Cancel`,
ainda inexistentes em `main` no momento deste plano), o EN-01 **não é uma task deste plano**: em
2026-09-13, `tasks/prd-conclusao-saga/techspec.md` e `tasks/prd-conclusao-saga/1_task.md` (task 1.0,
status `pending`) foram emendados para já incluir `TerminalTransitionAt`, a assinatura
`Confirm(DateTime)`/`Cancel(string, DateTime)`, a migration `AddTerminalTransitionAtToReservations` e
a constraint de consistência — ver a "Emenda de alinhamento (2026-09-13)" no topo de cada um desses
arquivos.

**Consequência para este plano:** a task 1.0 abaixo (V-01) é bloqueada, fora deste plano, pela
conclusão de `tasks/prd-conclusao-saga` task 1.0. Sem essa coluna e essas assinaturas em `main`/no
branch de trabalho, o DDL de V-01 não tem `terminal_transition_at` para projetar e a migration de
Booking necessária para a view não existe. O orquestrador deste PRD deve confirmar que
`tasks/prd-conclusao-saga` task 1.0 está `done` (ou disponível no mesmo branch) antes de iniciar a
task 1.0 deste plano; nenhuma task deste plano recria esse habilitador.

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|-------|---------|------------|
| `dotnet-dependency-config` | `.agents/skills/dotnet-dependency-config/SKILL.md` | DDL versionado fora do boot, roles/grants explícitos, Testcontainers PostgreSQL real, sem pacote/versão nova |
| `dotnet-testing` | `.agents/skills/dotnet-testing/SKILL.md` | Testes de integração contra Postgres real (não SQLite/InMemory), fixture dedicada, isolamento por teste, xUnit + AAA |
| `dotnet-architecture` | `.agents/skills/dotnet-architecture/SKILL.md` | Confirma que a view não introduz repository, controller, endpoint ou camada de aplicação nova |

Não foram consultadas skills de frontend, REST, mensageria, performance ou observabilidade de
runtime: F05 não cria UI, endpoint, evento, broker, requisito de carga ou telemetria nova. A
catalogação no OpenMetadata é um checkpoint manual já previsto pela fundação, documentado mas fora do
gate automatizado.

## Fases de Implementação

As fases agrupam comportamento e feedback, não uma camada arquitetural.

### Fase 1 — Publicar e provar o snapshot do calendário (V-01)
Cria a view `integration.reservation_calendar_v1` e prova, contra Postgres real, que ela publica
exatamente as Reservations terminais com os sete campos contratados, sem `solicitada`, com
cardinalidade/fidelidade corretas e visível na leitura seguinte a um commit de transição terminal.

### Fase 2 — Proteger o acesso e catalogar (V-02)
Prova que as roles de serviço só leem a view (sem escrita, sem acesso a `booking.*`), adiciona o
script SQL complementar de verificação de grants e documenta o procedimento de
ingestão/confirmação manual no OpenMetadata.

## Mapa de Entrega e Feedback

| Slice | Task | Comportamento observável | Gate executável | Seletor focalizado | Bloqueado por |
|-------|------|---------------------------|------------------|----------------------|----------------|
| V-01 | 1.0 | Uma leitura de `integration.reservation_calendar_v1` retorna exatamente as Reservations `confirmada`/`cancelada`, com 7 colunas contratadas, sem `solicitada`, sem dados proibidos, e a transição terminal persistida aparece na leitura seguinte | `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests"` → `GATE: APROVADO` | Classe `ReservationCalendarContractTests` (namespace completo no `gate_command`) | Externo — `tasks/prd-conclusao-saga` task 1.0 (ver "Pré-requisito externo" acima) |
| V-02 | 2.0 | As roles de serviço só têm `SELECT` na view (negação de `INSERT`/`UPDATE`/`DELETE`/`CREATE` e de acesso direto a `booking.reservations`); `verify-reservation-calendar.sql` roda contra Postgres descartável; procedimento de ingestão/checkpoint manual do OpenMetadata documentado | `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests.ServiceRoles"` → `GATE: APROVADO` | Métodos com prefixo `ServiceRoles_` na classe `ReservationCalendarContractTests` | 1.0 |

### Habilitadores inevitáveis

Nenhum habilitador dentro deste plano. O único habilitador da TechSpec (EN-01) já foi absorvido pela
emenda de `tasks/prd-conclusao-saga` (ver "Pré-requisito externo" acima) — este plano não o
reintroduz nem o duplica.

## Tarefas

- [x] 1.0 Publicar `integration.reservation_calendar_v1` e provar elegibilidade, cardinalidade, período e atualização pós-commit (V-01)
- [x] 2.0 Proteger o acesso por role e documentar a catalogação no OpenMetadata (V-02)

## Rastreabilidade US → Tasks

| User Story | Tasks Relacionadas | Tipo de Cobertura |
|------------|---------------------|---------------------|
| Consumidor futuro de Booking consulta períodos/estados por Accommodation | 1.0 | Direta |
| Consumidor de dados distingue `confirmada` de `cancelada` | 1.0 | Direta |
| Autor/arquiteto observa dataset publicado com Data Contract, ownership e lineage | 2.0 | Direta |
| Booking compartilha somente o mínimo necessário, preservando evolução interna | 1.0, 2.0 | Suporte |

## Validação de Cobertura

### Requisitos Funcionais

| Requisito | Task(s) | Status |
|-----------|---------|--------|
| RF-01 — publicar registros elegíveis do calendário | 1.0 | ✅ Coberto |
| RF-02 — manter o snapshot atual (atualização pós-commit, unicidade, monotonicidade) | 1.0 | ✅ Coberto |
| RF-03 — proteger e catalogar o contrato de dados | 2.0 | ✅ Coberto |

### Artefatos da TechSpec

| Artefato | Task | Status |
|----------|------|--------|
| `db/integration/001-reservation-calendar-v1.sql` | 1.0 | ✅ |
| `services/booking/tests/.../Reservations/ReservationCalendarFixture.cs` | 1.0 | ✅ |
| `services/booking/tests/.../Reservations/ReservationCalendarTestCollection.cs` | 1.0 | ✅ |
| `services/booking/tests/.../Reservations/ReservationCalendarContractTests.cs` | 1.0 (criado), 2.0 (estendido) | ✅ |
| `db/integration/verify-reservation-calendar.sql` | 2.0 | ✅ |
| `scripts/openmetadata/README.md` (procedimento F05) | 2.0 | ✅ |
| `docs/adr/adr-004-reservation-calendar-live-view.md`, `adr-005-terminal-transition-timestamp.md` | Referência — já Accepted, não recriados por este plano | ✅ Preexistente |
| `services/booking/src/.../Reservations/Reservation.cs` (`TerminalTransitionAt`, `Confirm`/`Cancel`) e a migration `AddTerminalTransitionAtToReservations` | Referência — produzidos por `tasks/prd-conclusao-saga` task 1.0 (EN-01), não por este plano | ✅ Externo |

### Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill Relacionada | Status |
|---|-----------|----------------|---------------------|--------|
| 1 | Setup / Configuração | 1.0 — DDL versionado fora do boot, fixture Testcontainers dedicada | `dotnet-dependency-config` | ✅ |
| 2 | Modelos de Dados | 1.0 — projeção de 7 colunas sobre `booking.reservations`; sem schema novo em Booking (a coluna de origem é do EN-01 externo) | `dotnet-architecture` | ✅ |
| 3 | Lógica de Negócio | N/A — a "lógica" é a definição SQL da view (filtro de estado + allow-list), sem código de aplicação | — | ✅ N/A justificado |
| 4 | Endpoints / Interfaces | N/A — a interface publicada é a relation SQL, não um endpoint HTTP (decisão explícita do PRD/TechSpec) | — | ✅ N/A justificado |
| 5 | Integrações Externas | N/A — sem broker, API externa ou chamada síncrona; PostgreSQL é o próprio datastore de Booking | — | ✅ N/A justificado |
| 6 | Validações e Erros | 1.0 — DDL falha se a coluna de origem não existir (sem snapshot parcial); 2.0 — negação de escrita/CREATE/acesso cruzado | `dotnet-testing` | ✅ |
| 7 | Testes | 1.0, 2.0 — `ReservationCalendarContractTests` contra Postgres real | `dotnet-testing` | ✅ |
| 8 | Observabilidade | N/A — sem logging/métricas/tracing de runtime; comments SQL + OpenMetadata são a visibilidade operacional (decisão da TechSpec) | — | ✅ N/A justificado |
| 9 | Documentação | 2.0 — `scripts/openmetadata/README.md` (procedimento F05) | — | ✅ |
| 10 | Segurança | 2.0 — grants somente leitura, negação de escrita e de acesso cruzado a `booking.*` | `dotnet-dependency-config` | ✅ |

### Coesão e Faixa de Tamanho

| Task | slice_type | Criar | Modificar | Subtarefas | Fatias | Faixa | Justificativa |
|------|------------|-------|-----------|------------|--------|-------|----------------|
| 1.0 | vertical | 4 | 0 | 4 | 1 | ✅ Dentro da faixa budget | View + fixture dedicada + testes de contrato formam uma única jornada observável (DDL → leitura → elegibilidade/cardinalidade/período/timestamp); nenhuma modificação em código de aplicação porque a fonte (`Reservation.TerminalTransitionAt`) já existe via o pré-requisito externo. |
| 2.0 | vertical | 1 | 2 | 4 | 1 | ✅ Dentro da faixa budget | Estende a mesma suíte de contrato com os cenários de grants/negação, adiciona o script SQL complementar e documenta o checkpoint manual do OpenMetadata; depende do objeto criado em 1.0. |

### Integridade dos Gates

| Task | Gate | Teste/fixture disponível | Filtro isolado | Repo compilável | Dependência futura | Status |
|------|------|---------------------------|------------------|-------------------|----------------------|--------|
| 1.0 | `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~...ReservationCalendarContractTests"` | Fixture e teste criados na própria task; migrations de Booking (incl. EN-01) e Testcontainers Postgres 16 preexistentes/externos | Sim — filtro por classe; o gate reprova seleção vazia | Sim, desde que `tasks/prd-conclusao-saga` task 1.0 já tenha rodado (ver dependência externa) | Não, dentro deste plano | ✅ |
| 2.0 | `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~...ReservationCalendarContractTests.ServiceRoles"` | Métodos `ServiceRoles_*` adicionados nesta task à classe criada em 1.0 | Sim — filtro por prefixo de método | Sim | Não | ✅ |

### Ciclo de Vida de Artefatos Compartilhados

| Artefato | Primeira task produtora | Tasks consumidoras | Dependências consistentes | Status |
|----------|---------------------------|------------------------|------------------------------|--------|
| `db/integration/001-reservation-calendar-v1.sql` | 1.0 | 2.0 (grants já criados nele são verificados), `verify-reservation-calendar.sql` | Sim | ✅ |
| `ReservationCalendarFixture`/`ReservationCalendarTestCollection` | 1.0 | 2.0 | Sim | ✅ |
| `ReservationCalendarContractTests` | 1.0 | 2.0 (estendida) | Sim | ✅ |
| `Reservation.TerminalTransitionAt`, `Confirm(DateTime)`/`Cancel(string, DateTime)`, migration `AddTerminalTransitionAtToReservations` | Externo — `tasks/prd-conclusao-saga` task 1.0 | 1.0, 2.0 (leem a coluna via a view) | Sim, condicionado à conclusão externa antes do início de 1.0 | ⚠️ Externo — ver "Pré-requisito externo" |

Nenhuma task deste plano valida com teste/fixture futuro interno ao plano. A única dependência
futura é externa (outro PRD) e está registrada explicitamente, não escondida como se já estivesse
disponível.

## Análise de Paralelização

### Lanes de Execução Paralela

Não há lane paralela dentro deste plano: 2.0 estende o mesmo arquivo de teste e o mesmo objeto SQL
criados em 1.0. O executor standard permanece sequencial (1.0 → 2.0).

### Caminho Crítico

`tasks/prd-conclusao-saga` task 1.0 (externo, produz `terminal_transition_at`) → 1.0 (publica e prova
o snapshot) → 2.0 (prova a proteção e documenta a catalogação). Cada etapa libera evidência própria
antes da seguinte.

### Diagrama de Dependências

```
[externo] prd-conclusao-saga 1.0 (EN-01: TerminalTransitionAt, Confirm/Cancel, migration)
        │
        ▼
      1.0 (V-01: view + contrato de dados)
        │
        ▼
      2.0 (V-02: grants/negação + verify SQL + OpenMetadata)
```
