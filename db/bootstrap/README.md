# Bootstrap de banco — `db/bootstrap` (EN-02)

Provisionamento manual, executado **UMA única vez** por um operador com
privilégio suficiente (superuser ou `CREATEDB` + `CREATEROLE`), **fora do ciclo
de deploy** de qualquer aplicação. Não é migration de serviço: criar
database/role/schema exige privilégio maior que o de aplicação (princípio de
menor privilégio).

> A execução real contra o `postgres-main` (Postgres compartilhado do `infra`)
> é **manual, com revisão humana, nunca automatizada em pipeline** sem
> confirmação (decisão da TechSpec, seção "Análise de Impacto"). O gate
> automatizado só prova os scripts contra um Postgres genérico descartável.

## Ordem de execução (sempre nesta sequência)

Contra o `postgres-main`, como operador privilegiado:

```bash
PSQL="psql -h <postgres-main-host> -U <admin> -v ON_ERROR_STOP=1"

# 1. Database (conecta no db de manutenção `postgres`)
$PSQL -d postgres -f db/bootstrap/001-database.sql

# 2. Roles — senhas via variáveis de ambiente, NUNCA hardcoded.
#    Gere uma senha forte por role e exporte-as apenas no shell do operador:
export CATALOG_PASSWORD="$(openssl rand -base64 24)"
export BOOKING_PASSWORD="$(openssl rand -base64 24)"
export PAYMENT_PASSWORD="$(openssl rand -base64 24)"
$PSQL -d postgres \
  -v catalog_password="$CATALOG_PASSWORD" \
  -v booking_password="$BOOKING_PASSWORD" \
  -v payment_password="$PAYMENT_PASSWORD" \
  -f db/bootstrap/002-roles.sql

# 3. Schemas (o script troca para `localize_stay` com \c)
$PSQL -d localize_stay -f db/bootstrap/003-schemas.sql

# 4. Grants mínimos + revogação cruzada
$PSQL -d localize_stay -f db/bootstrap/004-grants.sql

# 5. Verificação (após a execução; no gate, após a SEGUNDA rodada)
$PSQL -d localize_stay -f db/bootstrap/verify-grants.sql
```

Quem tem privilégio: o dono do homelab (autor), como dono do `infra` — único
perfil com acesso administrativo ao `postgres-main` (ver TechSpec,
"Dependências Técnicas Bloqueantes").

Como passar a senha sem hardcode: as senhas vivem só no ambiente do shell do
operador (`export` acima) e trafegam para o script como variáveis de
substituição do `psql` (`-v nome="$VAR"` → `PASSWORD :'nome'`). Nada é escrito
em arquivo versionado; o histórico do shell deve ter `HISTCONTROL=ignorespace`
ou as senhas devem ser lidas de um gerenciador de segredos. Cada serviço usa
depois a sua senha via `dotnet user-secrets` local (nunca em
`appsettings*.json` versionado).

## Opcional — role de leitura para o OpenMetadata

`005-openmetadata-reader.sql` é independente da sequência 001-004 (pode
rodar antes ou depois, a qualquer momento) e cria o role `openmetadata_reader`
usado pela ingestão de schema do OpenMetadata
(`scripts/openmetadata/ingestion-postgres.yaml`, job `catalog-metadata` do
CI). Só `CONNECT` + `USAGE` + `SELECT` nos 4 schemas do projeto, sem `CREATE`:

```bash
export OPENMETADATA_READER_PASSWORD="$(openssl rand -base64 24)"
$PSQL -d postgres \
  -v reader_password="$OPENMETADATA_READER_PASSWORD" \
  -f db/bootstrap/005-openmetadata-reader.sql
```

Depois, salve `$OPENMETADATA_READER_PASSWORD` como o secret
`OM_POSTGRES_READER_PASSWORD` no GitHub — ver `scripts/openmetadata/README.md`.

## Resultado esperado

Database `localize_stay` com schemas `catalog` (owner `catalog_role`),
`booking` (owner `booking_role`), `payment` (owner `payment_role`) e
`integration` (owner = operador de bootstrap, sem role de escrita própria);
cada role com `USAGE+CREATE` só no próprio schema e `USAGE+SELECT` em
`integration`, sem acesso cruzado entre domínios.

## Validação descartável (idempotência — como o gate prova)

Nunca valide contra o `postgres-main` real. Suba um Postgres 16-alpine
efêmero e rode os 4 scripts **2x seguidas** com `ON_ERROR_STOP=1` (a segunda
rodada deve sair sem erro), mais o `verify-grants.sql` após a 2ª rodada:

```bash
docker run -d --rm --name pg-bootstrap-check \
  -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres \
  -p 54333:5432 postgres:16-alpine
# aguardar pg_isready ...
export PGV="psql -h localhost -p 54333 -U postgres -v ON_ERROR_STOP=1"
export PW_ARGS="-v catalog_password=pw-catalog -v booking_password=pw-booking -v payment_password=pw-payment"
for i in 1 2; do
  $PGV -d postgres -f db/bootstrap/001-database.sql
  PGPASSWORD=postgres $PGV -d postgres $PW_ARGS -f db/bootstrap/002-roles.sql
  PGPASSWORD=postgres $PGV -d localize_stay -f db/bootstrap/003-schemas.sql
  PGPASSWORD=postgres $PGV -d localize_stay -f db/bootstrap/004-grants.sql
done
PGPASSWORD=postgres $PGV -d localize_stay -f db/bootstrap/verify-grants.sql
# negação funcional esperada (permission denied), ex.:
# PGPASSWORD=pw-booking psql ... -d localize_stay -c "SET ROLE booking_role; CREATE TABLE catalog.probe(id int);"
docker stop pg-bootstrap-check
```

Todas as evidências acima usam senhas fictícias locais; o container é
descartado (`--rm`) ao final.
