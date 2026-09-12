# Revisão — Task 2.0: Bootstrap de banco (EN-02) — focused, 1ª revisão

## Gate (estágio 1, antes de material semântico)

- Comando: `scripts/ai-flow/gate.sh --static`
- Resultado: `GATE: APROVADO` — `arquivos alterados: 10 (.NET: 0, node: 0)`, `format: pulado`,
  `build: ok (nada a compilar)`, `testes: nao aplicavel (static)` — exit 0.

## Evidência específica reexecutada pelo validator (container descartável, nunca `postgres-main`)

- Container: `postgres:16-alpine` efêmero (`--rm`), `localhost:54333`, senhas fictícias locais.
- 4 scripts rodados **2x seguidas** com `ON_ERROR_STOP=1`: 2ª rodada sem erro (idempotência OK).
  - R2-001 saiu sem output (zero linhas — `WHERE NOT EXISTS` não emitiu `CREATE`), demais
    convergiram (`DO`/`GRANT`/`REVOKE` repetidos sem erro).
- `verify-grants.sql` após a 2ª rodada: exit 0; matriz role×schema exata —
  cada role `t/t` só no próprio schema, `t/f` em `integration`, `f/f` nos demais domínios.
- Negação funcional: `booking_role` → `CREATE TABLE catalog.probe_neg1` falha
  (`permission denied for schema catalog`); `catalog_role` → `SELECT booking.seed1` falha
  (`permission denied for schema booking`).
- Positivo: `booking_role` escreve/lê no próprio schema OK; `catalog_role` lê
  `integration.events` OK (após `GRANT SELECT` como operador).
- Guarda 002 sem `-v`: imprime `ERROR: psql variable "catalog_password" is not set...` e
  interrompe o script antes de qualquer `ALTER ROLE` (nenhuma alteração executada).
- `postgres-main` real nunca tocado: todos os comandos contra `localhost:54333`
  (container parado/removido ao final). Task e README apontam o gate só ao descartável
  e reservam o real à execução manual — conforme exigido.

## Escopo revisado

- Diff desde checkpoint `5e3b39f`: só `2_task.md` (`pending`→`validating`) e `flow-state.json`
  (`active_task` 1.0→2.0 + histórico) — sem lógica.
- Untracked do escopo: `db/bootstrap/001-database.sql`, `002-roles.sql`, `003-schemas.sql`,
  `004-grants.sql`, `verify-grants.sql`, `db/bootstrap/README.md`.
- Skill `dotnet-dependency-config` (seção Configuração): nenhuma credencial hardcoded ou
  versionada — senhas só via `-v`/`$ENV` (`002-roles.sql:67-71`, `README.md:26-33`);
  `pw-*` no README são senhas fictícias locais do exemplo descartável (declarado em `README:90-91`).

## Conformidade (checklist da task)

- Idempotência real, sem `IF NOT EXISTS` inventado: `001` usa `SELECT ... WHERE NOT EXISTS ...
  \gexec` (`CREATE DATABASE` não tem `IF NOT EXISTS`); `002` usa `DO ... IF NOT EXISTS` sobre
  `pg_roles` (`ROLE` não tem `IF NOT EXISTS`); `003` usa `CREATE SCHEMA IF NOT EXISTS` (válido)
  + `ALTER OWNER` convergente; `004` `GRANT`/`REVOKE` convergentes. OK.
- Owners/grants mínimos + revogação cruzada: owner por domínio, `integration` sem owner de
  serviço; `USAGE,CREATE` só no próprio; `USAGE`+`SELECT` em `integration` sem `CREATE`
  (`004:29,60`); `REVOKE ALL` cruzado schema+tabelas+sequências (`004:46-56`). OK.
- `verify-grants.sql` com `has_schema_privilege` (matriz + asserções rígidas) e
  `has_table_privilege` (SELECT em `integration`). OK.

## Bloqueantes

Nenhum.

## Recomendações (2, não bloqueantes)

1. `db/bootstrap/002-roles.sql:28,34,40` — `\quit 1` é ignorado pelo psql 16
   (`\quit: extra argument "5" ignored`, exit 0; verificado em `psql 16.14`). A guarda
   cumpre o essencial (interrompe antes do `ALTER ROLE`), mas o exit 0 não sinaliza falha
   a wrappers com `&&`/`set -e`. Sugestão: trocar por falha garantida com `ON_ERROR_STOP=1`
   (ex.: `SELECT` em tabela inexistente ou `DO ... RAISE EXCEPTION`).
2. `db/bootstrap/README.md:79` — o bloco de validação descartável invoca `001-database.sql`
   sem `PGPASSWORD=postgres` (as demais linhas têm o prefixo); contra `postgres:16-alpine`
   com senha, a 1ª linha falha por falta de autenticação. Sugestão: prefixar a linha do
   `001` ou exportar `PGPASSWORD=postgres` uma vez antes do loop.

## Imutabilidade

- HEAD antes: `5e3b39f42ed0c99f63ba6174662cc1fc3888f240`; HEAD depois: idêntico.
- Working tree inalterado pelo validator (2 modificados + `db/`, `scripts/` untracked, como no
  início); nenhum código, status, task ou commit editado — só este relatório criado.

## Veredito

**VALIDAÇÃO APROVADA (2 recomendações)**
