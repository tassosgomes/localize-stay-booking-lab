---
status: done
slice_type: enabling
verification_type: static
parallelizable: true
blocked_by: []
---

<task_context>
<domain>engine/infra/database-provisioning</domain>
<type>implementation</type>
<scope>configuration</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"3.0, 4.0, 5.0"</unblocks>
<feedback_checkpoint>Rodar os 4 scripts SQL duas vezes seguidas contra um Postgres 16-alpine descartável (container local, não `postgres-main`) sem erro; query final confirma grants exatos por role</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --static</gate_command>
<gate_test_selector>N/A — habilitador static; provisionamento de infraestrutura (roles/schemas de banco), não comportamento de aplicação (ver `references/vertical-slicing.md`: "migration compartilhada que precisa existir antes de qualquer leitura")</gate_test_selector>
<gate_expected_result>Os 4 scripts rodam 2x seguidas contra um container Postgres 16-alpine efêmero sem erro (idempotência); `verify-grants.sql` confirma: cada role tem USAGE+CREATE apenas no próprio schema, USAGE+SELECT em `integration`, e falha (permission denied) ao tentar escrever/ler schema de outro domínio</gate_expected_result>
<static_evidence>`verify-grants.sql` usando `has_schema_privilege`/`has_table_privilege` por role, executado após a segunda rodada dos scripts</static_evidence>
<vertical_slice>N/A — enabling</vertical_slice>
</task_context>

# Tarefa 2.0: Bootstrap de banco — database, roles, schemas e grants (EN-02)

## Relacionada as User Stories

- N/A — TechSpec Standalone. Cobre a fatia V-02 da TechSpec **no que se refere a provisionamento de
  banco** (ver "Nota de desvio" em `tasks.md`): o inventário de artefatos da TechSpec atribui os 4
  scripts de `db/bootstrap` a V-02, mas a seção "Sequenciamento de Desenvolvimento" exige que
  database + roles + schema `catalog` já estejam criados **antes** de V-01. Esta task resolve essa
  inconsistência consolidando o provisionamento completo (as 4 roles/schemas, não só o de Catalog)
  como um único habilitador que roda antes de qualquer serviço, evitando editar os mesmos scripts SQL
  em duas tasks diferentes.

## Visão Geral

Cria o database `localize_stay` em `postgres-main` (`infra`, Coolify), seguindo a convenção "database
+ role por projeto" já registrada em `infra/roteiro.md` (Regra 4), com os quatro schemas
(`catalog`, `booking`, `payment`, `integration`) e as três roles de serviço (`catalog_role`,
`booking_role`, `payment_role`) com os grants mínimos: escrita restrita ao próprio schema, leitura
restrita ao schema `integration`, sem acesso cruzado entre schemas de domínio. Não é uma migration de
serviço — é provisionamento manual, executado uma única vez por um operador com privilégio suficiente,
fora do ciclo de deploy de qualquer aplicação.

## Entrega Observável

- **Entrada ou gatilho:** operador roda `psql -f 001-database.sql`, depois `002-roles.sql`,
  `003-schemas.sql`, `004-grants.sql`, nessa ordem, contra uma instância Postgres 16 (o gate
  automatizado usa um container descartável; a execução real contra `postgres-main` é verificação
  manual — ver "Fora deste checkpoint").
- **Resultado esperado:** database `localize_stay` com 4 schemas e 3 roles, cada role só conseguindo
  `CREATE`/escrita no próprio schema e `SELECT` em `integration`.
- **Checkpoint de feedback:** rodar os 4 scripts 2x contra um container Postgres 16-alpine efêmero
  (idempotência) e `verify-grants.sql` confirmando os grants exatos.
- **Seletor focalizado:** N/A (enabling static) — evidência é a execução determinística dos scripts +
  a query de verificação, não um teste de framework.
- **Fora deste checkpoint:** a execução real contra `postgres-main` (o Postgres compartilhado do
  `infra`) é manual, feita pelo operador com revisão humana, nunca automatizada num pipeline sem
  confirmação (decisão explícita da TechSpec, seção "Análise de Impacto"). O gate automatizado só
  prova que os scripts são corretos e idempotentes contra um Postgres genérico.

## Requisitos

- `001-database.sql` cria o database `localize_stay` de forma idempotente (`IF NOT EXISTS` ou
  equivalente).
- `002-roles.sql` cria `catalog_role`, `booking_role`, `payment_role` com `LOGIN`; senha lida de
  variável de ambiente do `psql` (`\set` ou `PGPASSWORD` via script wrapper), nunca hardcoded no
  arquivo versionado.
- `003-schemas.sql` cria `catalog`, `booking`, `payment`, `integration` dentro de `localize_stay`,
  com owner = role do próprio domínio; `integration` não tem role de escrita própria (nenhum
  consumidor escreve nele nesta fase).
- `004-grants.sql` concede `USAGE, CREATE` no schema próprio para cada role; concede `USAGE, SELECT`
  no schema `integration` para as três roles; revoga qualquer acesso cruzado entre schemas de
  domínio (ex.: `booking_role` não pode ler/escrever em `catalog`).
- Todos os 4 scripts são idempotentes (podem rodar mais de uma vez sem erro nem duplicar objetos).

## Arquivos Envolvidos

- **Criar:**
  - `db/bootstrap/001-database.sql`
  - `db/bootstrap/002-roles.sql`
  - `db/bootstrap/003-schemas.sql`
  - `db/bootstrap/004-grants.sql`
  - `db/bootstrap/verify-grants.sql` (query de verificação usada pelo gate e pela conferência manual)
- **Modificar:**
  - Nenhum.
- **Referência:**
  - `/home/tsgomes/github-tassosgomes/infra/roteiro.md` — "Regra 4 — PostgreSQL compartilhado por
    padrão" (convenção "database + role por projeto")
  - `/home/tsgomes/github-tassosgomes/infra/AGENTS.md` — limites de segurança para operar contra o
    Postgres compartilhado do `infra`
  - `context/architecture-baseline.md` — seção de Ownership de Dados (schema por domínio + schema de
    integração)
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — convenção de credenciais via variável de ambiente, nunca hardcoded

## Subtarefas

- [ ] 2.1 Criar `001-database.sql` (`CREATE DATABASE localize_stay` idempotente)
- [ ] 2.2 Criar `002-roles.sql` (`catalog_role`, `booking_role`, `payment_role`, senha via variável de
      ambiente do `psql`)
- [ ] 2.3 Criar `003-schemas.sql` (`catalog`, `booking`, `payment`, `integration`, owner por domínio)
- [ ] 2.4 Criar `004-grants.sql` (grants mínimos por role + revogação cruzada explícita)
- [ ] 2.5 Criar `verify-grants.sql` e validar idempotência rodando os 4 scripts 2x contra um Postgres
      16-alpine descartável, confirmando negação de acesso cruzado entre schemas de domínio
- [ ] 2.6 Documentar em `db/bootstrap/README.md` o passo a passo de execução manual, única vez, contra
      `postgres-main` (ordem dos scripts, quem tem privilégio, como passar a senha)

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 3.0, 4.0, 5.0 (nenhum serviço conecta ao Postgres real sem role/schema já existentes)
- Paralelizável: Sim (independente de 1.0 — não há sobreposição de arquivo; ambos podem rodar em
  paralelo)

## Rastreabilidade

- Esta tarefa cobre: Habilitador EN-02 (consolidação desta TechSpec, ver nota de desvio em
  `tasks.md`), que sustenta os requisitos de Ownership de Dados de V-01 e V-02.
- Evidência esperada: os 4 scripts existem e são idempotentes; `verify-grants.sql` prova os grants
  exatos; `db/bootstrap/README.md` documenta a execução manual contra `postgres-main`.

## Detalhes de Implementação

Da TechSpec (`techspec.md`, "Design de Implementação" → "Bootstrap de banco"):

> 1. `001-database.sql` — cria o database `localize_stay` em `postgres-main`, seguindo a convenção já
>    em uso ("database + role por projeto", `infra/roteiro.md`).
> 2. `002-roles.sql` — cria `catalog_role`, `booking_role`, `payment_role` (login, senha via variável
>    de ambiente do `psql`, nunca hardcoded no script versionado).
> 3. `003-schemas.sql` — dentro de `localize_stay`, cria `catalog`, `booking`, `payment`,
>    `integration`, com owner = role do próprio domínio (schema `integration` não tem role de escrita
>    própria).
> 4. `004-grants.sql` — concede `USAGE, CREATE` no schema próprio para cada role; concede `USAGE,
>    SELECT` no schema `integration` para as três roles de serviço; revoga qualquer acesso cruzado
>    entre schemas de domínio.

As migrations EF Core de cada serviço (tasks 3.0/4.0/5.0) rodam depois, conectando diretamente no
database `localize_stay`, autenticadas com a role do próprio domínio, e só têm permissão de atuar
dentro do seu schema — reforço automático da regra de ownership no nível de banco.

**Convenções da stack (das skills consultadas):**
- Credenciais nunca hardcoded em script versionado — sempre via variável de ambiente do `psql`,
  conforme princípio de menor privilégio já registrado no baseline.
- Scripts idempotentes (`CREATE ... IF NOT EXISTS` ou equivalente em `DO $$ ... $$` com checagem de
  existência prévia para objetos que não suportam `IF NOT EXISTS` nativamente, como `ROLE`).

## Prontidão para Implementação

- **Decisões fechadas:** nomes exatos de database (`localize_stay`), roles (`catalog_role`,
  `booking_role`, `payment_role`) e schemas (`catalog`, `booking`, `payment`, `integration`) — não
  reabrir; `integration` não tem role de escrita própria nesta fase; execução real contra
  `postgres-main` é sempre manual, nunca automatizada em pipeline.
- **Limites de decisão do implementer:** sintaxe exata de idempotência (`DO $$ ... $$` vs. função
  auxiliar), nome e formato exato de `verify-grants.sql`.
- **Dependências disponíveis:** nenhuma — pode começar imediatamente.
- **Artefatos exigidos pelo gate:** os 5 scripts SQL são criados nesta própria task; nenhum artefato
  preexistente é necessário. O container Postgres descartável usado pelo gate não é versionado (é
  infraestrutura efêmera do próprio comando de verificação).
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma — as duas pendências em "Questões em Aberto" da TechSpec
  (CustomMessaging no OpenMetadata, PAT de escopo mínimo) foram aprovadas para resolução durante a
  task 9.0 (V-06), não afetam esta task.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: rodar os 4 scripts (`001` a `004`) em sequência, 2 vezes seguidas,
      contra um Postgres 16-alpine efêmero, sem nenhum erro na segunda rodada
- [ ] `verify-grants.sql` confirma: `catalog_role` tem `USAGE+CREATE` em `catalog`, `USAGE+SELECT` em
      `integration`, e nenhum privilégio em `booking`/`payment` (idem para as outras duas roles)
- [ ] Build compila sem erros: N/A (SQL puro, sem projeto .NET) — usar `psql --set ON_ERROR_STOP=1`
      como equivalente de "build sem warning" para SQL
- [ ] Tentativa de `booking_role` escrever em `catalog` (ou vice-versa) falha com `permission denied`
- [ ] `db/bootstrap/README.md` documenta a ordem de execução manual e a forma de passar a senha sem
      hardcode
- [ ] Checkpoint de feedback executado: 4 scripts 2x + `verify-grants.sql` → grants exatos confirmados
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
      (todos criados nesta própria task)
- [ ] Nenhum arquivo produzido por task futura é necessário para validar esta task
- [ ] A evidência acima prova somente o provisionamento de banco e não depende de nenhum serviço HTTP
      já existir
