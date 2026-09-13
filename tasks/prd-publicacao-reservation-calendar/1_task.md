---
status: done
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: []
---

<task_context>
<domain>services/booking</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"2.0"</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests"` retorna `GATE: APROVADO`; o filtro encontra pelo menos um teste e todos os cenários de elegibilidade/cardinalidade/período/atualização passam contra PostgreSQL 16 real (Testcontainers)</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests"</gate_command>
<gate_test_selector>Classe `LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests` (todos os métodos criados nesta task; os métodos `ServiceRoles_*` de V-02 ainda não existem neste ponto)</gate_test_selector>
<gate_expected_result>O filtro seleciona pelo menos um teste; todos passam; a migration de origem (`AddTerminalTransitionAtToReservations`, produzida por `tasks/prd-conclusao-saga` task 1.0) já está aplicada no ambiente/branch de trabalho; `dotnet build` e o format scoped do gate ficam verdes</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Uma leitura de `integration.reservation_calendar_v1` retorna exatamente uma linha por Reservation em estado `confirmada` ou `cancelada`, com as sete colunas contratadas (`reservation_id`, `accommodation_id`, `check_in`, `check_out`, `status`, `created_at`, `updated_at`), sem nenhuma linha para Reservation `solicitada` e sem nenhum campo de Guest/Payment/Saga; uma transição terminal persistida por F04 aparece na próxima leitura bem-sucedida, sem lote, refresh ou notificação.</vertical_slice>
</task_context>

# Tarefa 1.0: Publicar `integration.reservation_calendar_v1` e provar elegibilidade, cardinalidade, período e atualização pós-commit (V-01)

## Relacionada às User Stories

- Consumidor futuro de Booking consulta períodos e estados de Reservations por Accommodation sem acessar tabelas internas (cobertura direta — RF-01).
- Consumidor de dados distingue `confirmada` de `cancelada` para considerar somente confirmações como ocupação ativa (cobertura direta — RF-01/RF-02, DP-01).
- Autor/arquiteto em estudo observa um dataset publicado com semântica de snapshot atual, sem histórico de transições (cobertura direta — RF-02, DP-02/DP-03).

## Visão Geral

Esta task implementa a fatia V-01 da TechSpec: cria a view `integration.reservation_calendar_v1`
como projeção explícita e filtrada de `booking.reservations`, e prova — contra PostgreSQL 16 real —
que ela cumpre RF-01 e RF-02. Não há código de aplicação C# novo: a "lógica de negócio" desta
feature é inteiramente a definição SQL da view (filtro de estado + allow-list de colunas).

**Pré-requisito externo, não produzido por esta task:** `booking.reservations.terminal_transition_at`
e `Reservation.Confirm(DateTime)`/`Cancel(string, DateTime)` são produzidos pela migration
`AddTerminalTransitionAtToReservations` de `tasks/prd-conclusao-saga` task 1.0 (EN-01, emenda de
2026-09-13 alinhada com ADR-005). Confirme que essa migration já foi aplicada no branch/ambiente de
trabalho antes de iniciar esta task — sem ela, a view não tem `updated_at` para projetar e a fixture
desta task falha ao migrar o banco descartável.

## Entrega Observável

- **Entrada ou gatilho:** execução do DDL versionado `db/integration/001-reservation-calendar-v1.sql` contra um banco com as migrations de Booking (incluindo `AddTerminalTransitionAtToReservations`) já aplicadas; leitura subsequente via `SELECT` na view.
- **Resultado esperado:** para cada Reservation `confirmada`/`cancelada` existe exatamente uma linha com as sete colunas contratadas, período `[check_in, check_out)` preservado e `updated_at` igual ao `terminal_transition_at` persistido; Reservations `solicitada` não aparecem; nenhuma linha contém Guest, Payment, preço, moeda, correlation/causation ou campos da Saga; uma Reservation que transiciona para terminal e tem commit bem-sucedido aparece na leitura seguinte feita por uma conexão nova.
- **Checkpoint de feedback:** `gate_command` desta task retorna `GATE: APROVADO`, com pelo menos um teste selecionado e todos os cenários abaixo verdes.
- **Seletor focalizado:** `gate_test_selector` acima.
- **Fora deste checkpoint:** grants explícitos por role e negação de escrita/acesso cruzado (V-02, task 2.0); catalogação no OpenMetadata (V-02); qualquer consumidor real do dataset (Busca da Fase 2, fora do escopo de F05).

## Requisitos

- A view usa somente `booking.reservations` como fonte, sem `SELECT *`, com a ordem de colunas do contrato: `reservation_id, accommodation_id, check_in, check_out, status, created_at, updated_at`.
- O filtro de elegibilidade é `WHERE status IN ('confirmada', 'cancelada')`; `solicitada` nunca aparece.
- `updated_at` da view é `r.terminal_transition_at`, sem recálculo em tempo de leitura.
- O DDL usa `CREATE OR REPLACE VIEW`, é transacional, idempotente (rodar duas vezes converge sem erro) e inclui `COMMENT ON VIEW`/`COMMENT ON COLUMN` para a semântica de cada campo (elegibilidade, período semiaberto, `updated_at`).
- O DDL concede `GRANT SELECT` explícito na view para `catalog_role`, `booking_role` e `payment_role` (mesmo que o bootstrap já preveja default privileges) — mas a prova comportamental da negação de escrita/CREATE/acesso cruzado pertence à task 2.0; esta task só precisa que o `GRANT SELECT` exista no script e não falhe ao aplicar (as três roles existem no ambiente real via `db/bootstrap/002-roles.sql`; na fixture desta task, ver "Detalhes de Implementação" sobre como supri-las).
- A fixture de teste usa PostgreSQL 16 real via Testcontainers (nunca SQLite/InMemory, nunca o `postgres-main` do homelab), aplica as migrations reais de Booking (via `BookingDbContext.Database.MigrateAsync()`) e depois executa o DDL desta task no mesmo banco.
- Os testes semeiam Reservations usando os métodos de domínio reais (`Reservation.Create` + `Confirm(DateTime)`/`Cancel(string, DateTime)`, já existentes após o pré-requisito externo), não `INSERT` manual que contorne os invariantes.
- Os testes cobrem, no mínimo: (1) `relkind` é view e colunas/tipos/ordem batem com o contrato; (2) uma Reservation `solicitada` não aparece; (3) uma `confirmada` e uma `cancelada` com datas distintas aparecem exatamente uma vez cada, com Accommodation/período/status fiéis e intervalo `[check_in, check_out)`; (4) transicionar uma Reservation para terminal, commitar, e ler por uma conexão nova mostra a linha com o mesmo `updated_at`; (5) uma entrada duplicada/tardia que RN-11 já impede de mudar o estado (via o guard de domínio de F04) mantém no máximo uma linha e o mesmo `updated_at`; (6) `information_schema.columns` não contém Guest/Payment/preço/moeda/correlation/causation/Saga; (7) aplicar o DDL duas vezes converge sem erro e sem relação duplicada.

## Arquivos Envolvidos

- **Criar:**
  - `db/integration/001-reservation-calendar-v1.sql`
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationCalendarFixture.cs`
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationCalendarTestCollection.cs`
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationCalendarContractTests.cs`
- **Modificar:** Nenhum arquivo de código de aplicação. Esta task não altera `Reservation.cs`, `ReservationConfiguration.cs` nem qualquer migration — esses artefatos já existem via o pré-requisito externo (`tasks/prd-conclusao-saga` task 1.0).
- **Referência:**
  - `tasks/prd-publicacao-reservation-calendar/techspec.md` (§Design de Implementação, §Modelos de Dados) — DDL normativo, allow-list de colunas, regras de versionamento.
  - `tasks/prd-publicacao-reservation-calendar/api-contract.yaml`/`api-contract.md` — tipos e exemplos do contrato (documentos de referência, `Em Revisão`, não alterados por esta task).
  - `tasks/prd-conclusao-saga/1_task.md` e `tasks/prd-conclusao-saga/techspec.md` (emenda de 2026-09-13) — assinatura de `Confirm`/`Cancel`, coluna `terminal_transition_at` e a migration `AddTerminalTransitionAtToReservations` que este teste depende de já ter rodado.
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Reservation.cs` — após o pré-requisito externo, ler a assinatura real de `Create`/`Confirm`/`Cancel` para semear os testes.
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/BookingDbContext.cs` e `Configurations/ReservationConfiguration.cs` — schema/mapeamento real de `booking.reservations` a projetar na view.
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/CustomWebApplicationFactory.cs` — padrão de Testcontainers Postgres 16 + `MigrateAsync()` a seguir na fixture nova (sem reusar o `WebApplicationFactory`/RabbitMQ, desnecessários para este teste — TechSpec autoriza fixture dedicada).
  - `db/bootstrap/003-schemas.sql`, `db/bootstrap/004-grants.sql` — convenção real de ownership do schema `integration` e do padrão de `GRANT SELECT`/`ALTER DEFAULT PRIVILEGES` a espelhar no DDL desta task.
  - `context/architecture-baseline.md`, `domains/booking/domain.md` — fronteiras de Data Contract e RN-01/02/09/10/11.
  - `docs/adr/adr-004-reservation-calendar-live-view.md`, `docs/adr/adr-005-terminal-transition-timestamp.md` — decisões Accepted que esta task implementa.
- **Skills para consultar durante implementação:**
  - `.agents/skills/dotnet-dependency-config/SKILL.md` — DDL versionado fora do boot, grants explícitos, Testcontainers.
  - `.agents/skills/dotnet-testing/SKILL.md` — teste de integração com Postgres real, fixture/coleção xUnit, AAA.

## Subtarefas

- [ ] 1.1 Escrever `db/integration/001-reservation-calendar-v1.sql`: `CREATE OR REPLACE VIEW`, comments, `GRANT SELECT` para as três roles; script transacional e idempotente.
- [ ] 1.2 Criar `ReservationCalendarFixture` (Testcontainers PostgreSQL 16, `IAsyncLifetime`): sobe o container, aplica `BookingDbContext.Database.MigrateAsync()`, garante que `catalog_role`/`booking_role`/`payment_role` existem no banco descartável (ver "Detalhes de Implementação"), executa o DDL desta task, expõe a connection string e um `BookingDbContext` para semear Reservations via domínio; criar `ReservationCalendarTestCollection` (`[CollectionDefinition]`) ligando a fixture à classe de teste.
- [ ] 1.3 Escrever `ReservationCalendarContractTests` cobrindo os 7 cenários da seção "Requisitos" (schema, elegibilidade, cardinalidade/fidelidade/período, atualização pós-commit, duplicidade/tardio sem regressão, ausência de dados proibidos, DDL idempotente).
- [ ] 1.4 Rodar `gate_command`, confirmar `GATE: APROVADO` e registrar a evidência (saída do gate) no checkpoint da task.

## Sequenciamento

- **Bloqueado por (dentro deste plano):** Nenhum.
- **Bloqueado por (externo, fora deste plano):** `tasks/prd-conclusao-saga` task 1.0 — produz `Reservation.TerminalTransitionAt`, `Confirm(DateTime)`/`Cancel(string, DateTime)` e a migration `AddTerminalTransitionAtToReservations` que esta task consome. Confirmar `status: done` (ou equivalente disponível no branch de trabalho) antes de iniciar.
- **Desbloqueia:** 2.0.
- **Paralelizável:** Não. A fixture e a classe de teste criadas aqui são a base modificada por 2.0.

## Rastreabilidade

- **Esta tarefa cobre:** RF-01, RF-02; RN-01, RN-02, RN-09, RN-10, RN-11; DP-01, DP-02, DP-03.
- **Evidência esperada:** `ReservationCalendarContractTests` verde contra Postgres real, provando elegibilidade, cardinalidade, fidelidade de período/status, atualização pós-commit e ausência de dados internos; DDL idempotente confirmado por execução dupla no mesmo teste/setup.

## Detalhes de Implementação

### DDL normativo (equivalente ao exigido pela TechSpec)

```sql
CREATE OR REPLACE VIEW integration.reservation_calendar_v1 AS
SELECT
    r.id AS reservation_id,
    r.accommodation_id,
    r.check_in,
    r.check_out,
    r.status,
    r.created_at,
    r.terminal_transition_at AS updated_at
FROM booking.reservations AS r
WHERE r.status IN ('confirmada', 'cancelada');

GRANT SELECT ON integration.reservation_calendar_v1 TO catalog_role, booking_role, payment_role;

COMMENT ON VIEW integration.reservation_calendar_v1 IS
  'Snapshot atual das Reservations em estado terminal (confirmada/cancelada); confirmada = ocupação ativa, cancelada = desfecho terminal sem ocupação. Owner: Booking.';
-- + COMMENT ON COLUMN para cada uma das 7 colunas, descrevendo a semântica do contrato.
```

O `CREATE SCHEMA IF NOT EXISTS integration` **não** é responsabilidade deste script em produção
(`db/bootstrap/003-schemas.sql` já cria o schema com ownership do operador) — mas a fixture de teste
precisa criar o schema `integration` no banco descartável antes de aplicar este DDL, já que
`BookingDbContext.MigrateAsync()` só cria o schema `booking`.

### Fixture dedicada (não reusar `CustomWebApplicationFactory`)

`CustomWebApplicationFactory` sobe um `WebApplicationFactory<Program>` completo mais um container
RabbitMQ — desnecessário aqui, já que F05 não tem HTTP nem mensageria. Crie uma fixture mais enxuta:

1. `PostgreSqlBuilder("postgres:16-alpine")` com database/usuário/senha equivalentes ao padrão já
   usado em `CustomWebApplicationFactory` (mesma imagem, para consistência de ambiente).
2. Após `StartAsync()`, aplicar `BookingDbContext.Database.MigrateAsync()` (mesmo padrão de
   `CustomWebApplicationFactory.InitializeAsync`) — isso cria o schema `booking` e todas as tabelas,
   incluindo `terminal_transition_at` (pré-requisito externo já mesclado).
3. Via uma conexão Npgsql própria (superusuário do container, `postgres`), executar
   `CREATE SCHEMA IF NOT EXISTS integration;` e, como as roles `catalog_role`/`booking_role`/
   `payment_role` não existem em um Postgres novo, criá-las como `NOLOGIN` antes do `GRANT SELECT`
   do DDL (`CREATE ROLE catalog_role NOLOGIN; ...` — idempotente com `DO $$ ... EXCEPTION WHEN
   duplicate_object THEN NULL; END $$;` ou checagem via `pg_roles`). São roles descartáveis do banco
   de teste, não as roles reais do bootstrap; a prova comportamental de que essas roles só leem
   pertence à task 2.0 — aqui elas só precisam existir para o `GRANT` do script não falhar.
4. Executar o conteúdo de `db/integration/001-reservation-calendar-v1.sql` (ler o arquivo do disco,
   não duplicar o SQL inline) contra o mesmo banco.
5. Expor `ConnectionString` e um método para obter um `BookingDbContext` de escrita (para os testes
   semearem Reservations via domínio) e uma conexão Npgsql crua (para os testes lerem a view e
   `information_schema`).
6. `DisposeAsync` para o container.

### Testes — cenários mínimos

- **Schema:** consultar `information_schema.columns` para `integration.reservation_calendar_v1` e
  comparar nomes/ordem/tipos com o contrato; consultar `pg_class.relkind = 'v'`.
- **Elegibilidade:** semear uma Reservation via `Reservation.Create(...)` (permanece `Solicitada`) e
  confirmar que nenhuma linha aparece na view para ela.
- **Cardinalidade/fidelidade/período:** semear uma Reservation e chamar `Confirm(DateTime.UtcNow)`;
  semear outra e chamar `Cancel("motivo de teste", DateTime.UtcNow)`; persistir via
  `BookingDbContext.SaveChangesAsync()`; ler a view e confirmar exatamente uma linha por Reservation,
  `accommodation_id`/`check_in`/`check_out`/`status` fiéis, e `check_out > check_in` (semântica
  `[check_in, check_out)` já garantida pelo domínio, a view não recalcula).
- **Atualização pós-commit:** após o commit acima, abrir uma **nova** conexão/contexto e ler a view;
  confirmar que a linha aparece com `updated_at` igual ao `terminal_transition_at` persistido.
- **Duplicidade/tardio sem regressão:** chamar `Confirm`/`Cancel` novamente sobre uma Reservation já
  terminal deve lançar `InvalidOperationException` (guard de domínio de F04, RN-11) — o teste
  confirma que a exceção é lançada e que a linha na view permanece única com o `updated_at` original
  inalterado (não que a view "ignora" a chamada; o domínio nunca chega a persistir uma segunda vez).
- **Ausência de dados proibidos:** `information_schema.columns` não contém `guest_reference`,
  `guests_count`, `price_per_night`, `total_amount`, `currency`, `correlation_id`, `causation_id`,
  `cancellation_reason` ou qualquer coluna de `booking.reservation_sagas`.
- **Idempotência do DDL:** executar o script duas vezes no mesmo banco (ex.: no `InitializeAsync` da
  fixture, ou em um teste dedicado) sem erro, com a view convergindo para a mesma definição.

**Convenções da stack (das skills consultadas):**
- Testes de integração usam PostgreSQL real via Testcontainers, nunca SQLite/InMemory (`dotnet-testing`).
- DDL versionado fora do boot da aplicação, nomes totalmente qualificados, sem `SELECT *` (`dotnet-dependency-config`).
- Nomes de teste descritivos (`MethodOrScenario_Condition_ExpectedBehavior`), AAA (`dotnet-testing`).

## Prontidão para Implementação

- **Decisões fechadas:** DDL exato (7 colunas, ordem, filtro de status, `updated_at = terminal_transition_at`); view regular (não materializada); sem `SELECT *`; comments obrigatórios; `GRANT SELECT` explícito no script; fixture dedicada (não `CustomWebApplicationFactory`); sem lote/refresh/push. Nenhuma dessas decisões pode ser reaberta por esta task.
- **Limites de decisão do implementer:** nome exato dos métodos de teste, organização interna da fixture (ex.: helpers privados), forma de garantir idempotência da criação das roles descartáveis no banco de teste, timeout de polling se necessário.
- **Dependências disponíveis:** PostgreSQL 16 via Testcontainers já usado em `CustomWebApplicationFactory` (padrão a seguir); `scripts/ai-flow/gate.sh`; `dotnet-ef`/EF Core já pinados. **Dependência externa obrigatória:** `Reservation.TerminalTransitionAt`, `Confirm(DateTime)`, `Cancel(string, DateTime)` e a migration `AddTerminalTransitionAtToReservations`, produzidos por `tasks/prd-conclusao-saga` task 1.0 — sem isso, a fixture não migra e esta task não pode ser validada.
- **Artefatos exigidos pelo gate:** os 4 arquivos desta task são criados por ela; a migration/coluna de origem é preexistente **somente se** a task externa já rodou — caso contrário, este gate falha por design (não é uma falha de implementação desta task, é a dependência externa não satisfeita).
- **Dependências futuras:** Nenhuma para compilar/validar esta task. A task 2.0 deste plano modifica os arquivos criados aqui.
- **Ambiguidades bloqueantes:** Nenhuma dentro do escopo desta task. A única incerteza é operacional (se a dependência externa já rodou no ambiente de trabalho), não uma decisão de produto ou arquitetura em aberto.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests"`.
- [ ] O seletor encontra pelo menos um teste e não executa casos sem relação com esta task (ex.: não roda `ReservationTests`/`ConfirmReservationCommandHandlerTests` de F04).
- [ ] Build compila sem erros: `dotnet build services/booking/LocalizeStay.Booking.sln`.
- [ ] `integration.reservation_calendar_v1` existe como view (`relkind = 'v'`), com as 7 colunas na ordem e tipos do contrato.
- [ ] Uma Reservation `solicitada` não aparece na view; uma `confirmada` e uma `cancelada` aparecem exatamente uma vez cada, com período/estado fiéis.
- [ ] Uma transição terminal committed é visível na próxima leitura por uma nova conexão, com `updated_at` igual ao `terminal_transition_at` persistido.
- [ ] Repetir uma transição terminal sobre a mesma Reservation lança `InvalidOperationException` (RN-11) sem criar segunda linha nem alterar `updated_at`.
- [ ] `information_schema.columns` da view não contém nenhum campo de Guest, Payment, preço, moeda, correlação/causação ou Saga.
- [ ] Executar `db/integration/001-reservation-calendar-v1.sql` duas vezes no mesmo banco converge sem erro.
- [ ] Checkpoint de feedback executado: `gate_command` → `GATE: APROVADO`.
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados nela; a dependência externa (migration EN-01) é referenciada, não recriada.
- [ ] Nenhum arquivo produzido por task futura (2.0) é necessário para compilar ou validar esta task.
- [ ] A evidência acima prova somente V-01 (snapshot/elegibilidade/atualização) e não depende dos cenários de grants/negação de 2.0.
