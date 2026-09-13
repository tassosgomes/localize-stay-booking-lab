---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: ["1.0"]
---

<task_context>
<domain>services/booking</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>""</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests.ServiceRoles"` retorna `GATE: APROVADO`; o filtro encontra pelo menos um teste e todos os cenários de grants/negação passam contra PostgreSQL 16 real (Testcontainers)</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests.ServiceRoles"</gate_command>
<gate_test_selector>Métodos com prefixo `ServiceRoles_` na classe `LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests` (criada em 1.0, estendida nesta task)</gate_test_selector>
<gate_expected_result>O filtro seleciona pelo menos um teste; todos passam, comprovando `SELECT` permitido e `INSERT`/`UPDATE`/`DELETE`/`CREATE` negados para as três roles, além de negação de `SELECT` direto em `booking.reservations`; `dotnet build` e format scoped do gate ficam verdes</gate_expected_result>
<static_evidence>N/A — behavioral; a catalogação no OpenMetadata é um checkpoint manual documentado nesta task, explicitamente fora do gate automatizado (decisão da TechSpec)</static_evidence>
<vertical_slice>`catalog_role`, `booking_role` e `payment_role` conseguem apenas `SELECT` em `integration.reservation_calendar_v1`; qualquer tentativa de `INSERT`/`UPDATE`/`DELETE` na view, `CREATE` em `integration` ou `SELECT` direto em `booking.reservations` por essas roles é negada pelo PostgreSQL. O procedimento de ingestão/confirmação manual do dataset, owner e lineage no OpenMetadata fica documentado para o operador do laboratório.</vertical_slice>
</task_context>

# Tarefa 2.0: Proteger o acesso por role e documentar a catalogação no OpenMetadata (V-02)

## Relacionada às User Stories

- Autor/arquiteto em estudo observa um dataset publicado com Data Contract, ownership e lineage catalogados, verificando na prática a fronteira de dados entre domínios (cobertura direta — RF-03).
- Booking compartilha somente o mínimo necessário para o calendário, preservando a evolução interna sem quebrar consumidores (cobertura de suporte — RF-03, restrições de acesso do baseline).

## Visão Geral

Esta task implementa a fatia V-02: prova que a proteção de acesso definida no DDL de 1.0 (`GRANT
SELECT` para as três roles, sem `CREATE`) é efetiva contra tentativas de escrita e de acesso cruzado
a `booking.*`, e documenta — sem automatizar — o procedimento de ingestão/confirmação no
OpenMetadata que RF-03 exige (owner Booking, descrição, qualidade, compatibilidade, lineage). A
verificação de grants é behavioral (métodos de teste); a catalogação no OpenMetadata é
explicitamente um checkpoint manual, não substituível por asserção automatizada, porque depende de
uma instância OpenMetadata real fora do escopo do gate determinístico.

## Entrega Observável

- **Entrada ou gatilho:** a mesma fixture/banco descartável de 1.0, já com a view e as roles descartáveis criadas; execução dos novos testes `ServiceRoles_*`.
- **Resultado esperado:** para cada uma das três roles, `SELECT` na view retorna as linhas esperadas; `INSERT`/`UPDATE`/`DELETE` na view e `CREATE` no schema `integration` são negados pelo PostgreSQL (erro de permissão, não exceção não tratada); `SELECT` direto em `booking.reservations` pelas mesmas roles é negado. Um script SQL complementar (`verify-reservation-calendar.sql`) roda manualmente contra Postgres descartável como segunda evidência. `scripts/openmetadata/README.md` ganha o procedimento F05.
- **Checkpoint de feedback:** `gate_command` desta task retorna `GATE: APROVADO`.
- **Seletor focalizado:** `gate_test_selector` acima.
- **Fora deste checkpoint:** a confirmação real de owner/lineage na instância OpenMetadata do homelab (checklist manual, anexado à revisão da task, não ao gate automatizado); qualquer mudança de schema ou versão v2 do contrato.

## Requisitos

- Os testes `ServiceRoles_*` usam a conexão Npgsql do superusuário do container para `SET ROLE <role>` (o superusuário pode assumir qualquer role sem exigir `LOGIN`/senha) antes de cada asserção, e `RESET ROLE` depois — sem abrir conexões/credenciais adicionais.
- Cobrir, para cada uma das três roles (`catalog_role`, `booking_role`, `payment_role`):
  - `SELECT * FROM integration.reservation_calendar_v1` funciona e retorna as linhas esperadas.
  - `INSERT INTO integration.reservation_calendar_v1 ...`, `UPDATE integration.reservation_calendar_v1 ...` e `DELETE FROM integration.reservation_calendar_v1 ...` falham com erro de permissão do PostgreSQL (`42501` / `InsufficientPrivilege`), capturado e verificado, não relançado como falha de teste.
  - `CREATE TABLE integration.qualquer_coisa (...)` falha com erro de permissão (sem `CREATE` no schema `integration`).
  - `SELECT * FROM booking.reservations` falha com erro de permissão (sem `USAGE`/`SELECT` em `booking`).
- `db/integration/verify-reservation-calendar.sql` é um script SQL autônomo (não chamado pelo gate automatizado) que repete, via `psql`, as mesmas verificações de `relkind`, grants e negação — evidência complementar para execução manual contra um Postgres descartável, conforme a TechSpec.
- `scripts/openmetadata/README.md` ganha uma seção nova (mesmo padrão da seção existente de RabbitMQ) descrevendo: como rodar `ingestion-postgres.yaml` depois que a view existe; o que confirmar na UI (`localize_stay.integration.reservation_calendar_v1`, owner Booking, tag `localize-stay`, descrição/compatibilidade do contrato, lineage até `booking.reservations`); que essa confirmação é manual e não é substituída pelo gate automatizado.

## Arquivos Envolvidos

- **Criar:**
  - `db/integration/verify-reservation-calendar.sql`
- **Modificar:**
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationCalendarContractTests.cs` — adicionar os métodos `ServiceRoles_*` descritos em "Requisitos", reutilizando a fixture/collection de 1.0 sem alterá-las.
  - `scripts/openmetadata/README.md` — adicionar a seção do procedimento F05 (mesmo padrão da seção de RabbitMQ já existente no arquivo).
- **Referência:**
  - `tasks/prd-publicacao-reservation-calendar/techspec.md` (§Testes de Integração, item 9; §OpenMetadata) — cenários normativos de grants e o checkpoint de catalogação.
  - `db/bootstrap/verify-grants.sql` — padrão existente de verificação SQL de grants a espelhar em `verify-reservation-calendar.sql`.
  - `db/bootstrap/004-grants.sql` — regra real de `GRANT`/`REVOKE` que a view deve respeitar (leitura em `integration`, sem `CREATE`, sem acesso cruzado a `booking`).
  - `scripts/openmetadata/ingestion-postgres.yaml` — já inclui o schema `integration` e `includeViews: true`; não precisa mudar.
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationCalendarFixture.cs`, `ReservationCalendarTestCollection.cs` (produzidos em 1.0) — reutilizar sem modificar a fixture nesta task.
- **Skills para consultar durante implementação:**
  - `.agents/skills/dotnet-dependency-config/SKILL.md` — grants somente leitura, ausência de `CREATE` para roles de serviço.
  - `.agents/skills/dotnet-testing/SKILL.md` — teste de integração com Postgres real, AAA.

## Subtarefas

- [ ] 2.1 Adicionar os testes `ServiceRoles_*` (SELECT permitido; INSERT/UPDATE/DELETE/CREATE negados; SELECT direto em `booking.reservations` negado) à classe criada em 1.0, usando `SET ROLE`/`RESET ROLE` na fixture existente.
- [ ] 2.2 Escrever `db/integration/verify-reservation-calendar.sql`, espelhando `db/bootstrap/verify-grants.sql`, cobrindo `relkind`, colunas/allow-list, grants e negação de escrita/acesso cruzado.
- [ ] 2.3 Atualizar `scripts/openmetadata/README.md` com a seção do procedimento F05 (ingestão, checklist manual de owner/tag/lineage, explicitação de que não é gate automatizado).
- [ ] 2.4 Rodar `gate_command`, confirmar `GATE: APROVADO` e registrar, junto à evidência do gate, o checklist manual pendente de confirmação real no OpenMetadata (não bloqueia o gate, mas deve ser anexado à revisão da task antes de marcá-la `done`).

## Sequenciamento

- **Bloqueado por:** 1.0 (view, fixture e classe de teste já criadas).
- **Desbloqueia:** Nenhuma task deste plano.
- **Paralelizável:** Não. Modifica o mesmo arquivo de teste criado em 1.0.

## Rastreabilidade

- **Esta tarefa cobre:** RF-03; restrições de acesso do baseline (Data Contract, ownership, leitura somente); catalogação/lineage no OpenMetadata.
- **Evidência esperada:** `ReservationCalendarContractTests.ServiceRoles_*` verde contra Postgres real, provando `SELECT` permitido e escrita/acesso cruzado negados; `verify-reservation-calendar.sql` executável manualmente; `scripts/openmetadata/README.md` com o procedimento documentado; checklist manual de confirmação real no OpenMetadata anexado à revisão.

## Detalhes de Implementação

### Padrão de teste por role

```csharp
await using var connection = await fixture.OpenSuperuserConnectionAsync();

await using (var setRole = connection.CreateCommand())
{
    setRole.CommandText = "SET ROLE catalog_role;";
    await setRole.ExecuteNonQueryAsync();
}

await using var select = connection.CreateCommand();
select.CommandText = "SELECT 1 FROM integration.reservation_calendar_v1 LIMIT 1;";
await select.ExecuteScalarAsync(); // não deve lançar

await using var insert = connection.CreateCommand();
insert.CommandText =
    "INSERT INTO integration.reservation_calendar_v1 (reservation_id) VALUES (gen_random_uuid());";
var ex = await Record.ExceptionAsync(() => insert.ExecuteNonQueryAsync());
ex.Should().BeOfType<PostgresException>()
    .Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);

await using (var resetRole = connection.CreateCommand())
{
    resetRole.CommandText = "RESET ROLE;";
    await resetRole.ExecuteNonQueryAsync();
}
```

Repetir o padrão para `UPDATE`/`DELETE` na view, `CREATE TABLE` em `integration` e `SELECT` direto em
`booking.reservations`. Sempre `RESET ROLE` ao final de cada bloco (inclusive em caminho de exceção,
via `try/finally` ou bloco equivalente), para não vazar o `SET ROLE` entre testes que compartilham a
mesma conexão/fixture.

### `verify-reservation-calendar.sql` (evidência complementar, fora do gate)

Espelhar a estrutura de `db/bootstrap/verify-grants.sql`: um script `psql` com `\set ON_ERROR_STOP
on`, que confirma via `information_schema`/`pg_class`/`has_table_privilege` que a view existe, tem o
schema/colunas esperados, e que as três roles têm `SELECT` mas não `INSERT`/`UPDATE`/`DELETE` nela
nem `CREATE` no schema `integration`. Este script não é chamado pelo `gate_command` — é evidência
manual complementar, como a TechSpec já define.

### `scripts/openmetadata/README.md`

Seguir o mesmo formato da seção existente ("Registro do RabbitMQ..."): título, contexto (V-01/V-02 de
F05), passo a passo de execução da ingestão (`ingestion-postgres.yaml`, já configurada), e uma lista
do que confirmar na UI do OpenMetadata — dataset `localize_stay.integration.reservation_calendar_v1`,
owner lógico Booking, tag `localize-stay`, descrição/compatibilidade herdadas dos `COMMENT` do DDL, e
lineage até `booking.reservations`. Deixar explícito que esse passo é manual, feito pelo dono do
homelab, e que a ausência de credenciais reais não invalida o gate automatizado desta task.

**Convenções da stack (das skills consultadas):**
- Testes de integração usam PostgreSQL real via Testcontainers (`dotnet-testing`).
- Grants somente leitura para roles de serviço, sem `CREATE` fora do próprio schema (`dotnet-dependency-config`).
- Nomes de teste descritivos, AAA (`dotnet-testing`).

## Prontidão para Implementação

- **Decisões fechadas:** verificação de grants via `SET ROLE` na conexão de superusuário (sem credenciais/roles `LOGIN` adicionais); os quatro cenários de negação (INSERT/UPDATE/DELETE/CREATE) mais a negação de acesso cruzado a `booking.reservations`; `verify-reservation-calendar.sql` é evidência complementar manual, não parte do gate; catalogação no OpenMetadata é checkpoint manual documentado, não automatizado.
- **Limites de decisão do implementer:** nomes exatos dos métodos `ServiceRoles_*`, organização interna do script SQL complementar, texto exato da seção nova do README (desde que cubra os itens exigidos).
- **Dependências disponíveis:** view, fixture (`ReservationCalendarFixture`) e classe de teste (`ReservationCalendarContractTests`) já criadas em 1.0; `db/bootstrap/verify-grants.sql` como padrão de referência; `scripts/openmetadata/ingestion-postgres.yaml` já configurado.
- **Artefatos exigidos pelo gate:** os métodos `ServiceRoles_*` são criados nesta task na classe existente; nenhum artefato do gate depende de uma task futura.
- **Dependências futuras:** Nenhuma. Esta é a última task do plano.
- **Ambiguidades bloqueantes:** Nenhuma. A confirmação real no OpenMetadata depende de um ambiente externo (credenciais/instância do homelab) e é tratada como checkpoint manual explícito, não como ambiguidade de implementação.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests.ServiceRoles"`.
- [ ] O seletor encontra pelo menos um teste e não reexecuta os cenários de 1.0 desnecessariamente (o filtro seleciona apenas os métodos `ServiceRoles_*`).
- [ ] Build compila sem erros: `dotnet build services/booking/LocalizeStay.Booking.sln`.
- [ ] Para as três roles, `SELECT` na view funciona e `INSERT`/`UPDATE`/`DELETE` na view são negados com erro de permissão do PostgreSQL.
- [ ] Para as três roles, `CREATE` em `integration` é negado.
- [ ] Para as três roles, `SELECT` direto em `booking.reservations` é negado.
- [ ] `db/integration/verify-reservation-calendar.sql` existe, é executável via `psql` contra um Postgres descartável e cobre relkind/colunas/grants/negação.
- [ ] `scripts/openmetadata/README.md` documenta o procedimento F05 (ingestão + checklist manual de owner/tag/lineage), deixando explícito que não é gate automatizado.
- [ ] Checkpoint de feedback executado: `gate_command` → `GATE: APROVADO`.
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela.
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task (não há task futura neste plano).
- [ ] A evidência acima prova somente V-02 (grants/negação) e não depende de reexecutar os cenários de elegibilidade/cardinalidade de V-01 para ser considerada válida.
