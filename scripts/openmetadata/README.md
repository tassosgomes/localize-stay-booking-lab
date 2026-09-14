# Publicação de contratos no OpenMetadata

Todo push em `main` roda o job `catalog-metadata` do `.github/workflows/ci.yml`
no runner self-hosted `homelab` (o único que alcança
`openmetadata.lab.tasso.dev.br`, só disponível na LAN do `infra`). O job
publica, nesta ordem, tudo que está versionado em `contracts/`:

1. **Ingestão de schema Postgres** (`ingestion-postgres.yaml`, conector nativo
   `postgres`) — schemas `catalog`/`booking`/`payment`/`integration`.
2. **OpenAPI** (`ingestion-openapi-catalog.yaml`, `ingestion-openapi-booking.yaml`,
   conector nativo `rest`) — `apiServices`/`apiCollections`/`apiEndpoints` a
   partir de `contracts/openapi/{catalog,booking}.json`. `payment.json` entra
   quando tiver `paths` reais (hoje é `{}`).
3. **AsyncAPI** (`register-rabbitmq`, publicador dedicado em .NET) — um
   `messagingServices`/`topics` por canal declarado em
   `contracts/asyncapi/*.yaml`. O OpenMetadata 2.0.x não tem conector nativo
   para RabbitMQ, por isso este passo continua sendo um upsert HTTP manual em
   vez de `metadata ingest`.
4. **Data Contracts** (`register-data-contracts`, publicador dedicado em
   .NET) — `contracts/data-contracts/*.md` (formato próprio do projeto, não é
   ODCS) vira `description` + tag `localize-stay` + 4 Custom Properties
   (`dataContractRef`, `dataContractVersion`, `dataContractStatus`,
   `dataContractOwnerDomain`) na `table`/`view` correspondente. Depende da
   ingestão do passo 1 já ter criado a tabela/view alvo.

Todo passo é idempotente (upsert/PATCH por nome ou FQN) — repetir em todo
push é seguro.

## Arquivos

| Arquivo | Papel |
|---|---|
| `ingestion-postgres.yaml` | Config do conector Postgres (schemas do projeto) |
| `ingestion-openapi-catalog.yaml`, `ingestion-openapi-booking.yaml` | Config do conector `rest` (OpenAPI) |
| `register-rabbitmq/` | Console .NET: lê `contracts/asyncapi/*.yaml` e faz upsert de messagingService + topics |
| `register-data-contracts/` | Console .NET: lê `contracts/data-contracts/*.md` e faz PATCH de description + tags + Custom Properties |
| `bootstrap-custom-properties.sh` | Setup único (não roda no CI) das 4 Custom Properties usadas pelo passo 4 |
| `RegisterRabbitMq.Tests/`, `RegisterDataContracts.Tests/` | Testes unitários dos parsers/payloads (sem rede) — gate das respectivas tasks |

## Pré-requisitos (manuais, feitos uma vez pelo dono do homelab)

Nada disso é automatizável pela pipeline — precisa existir **antes** da
primeira execução real do job `catalog-metadata`:

1. Criar um bot de ingestão no OpenMetadata (Settings → Bots) com escopo de
   escrita sobre `databaseServices`/`tables`, `apiServices`/`apiCollections`/
   `apiEndpoints` e `messagingServices`/`topics`; salvar o JWT como o secret
   `OPENMETADATA_INGESTION_JWT` no GitHub.
2. Criar a repo variable `OPENMETADATA_BASE_URL` =
   `https://openmetadata.lab.tasso.dev.br/api`.
3. Criar o role `openmetadata_reader` no `postgres-main` rodando
   `db/bootstrap/005-openmetadata-reader.sql` (só leitura, sem `CREATE`) e
   salvar a senha gerada como o secret `OM_POSTGRES_READER_PASSWORD` no
   GitHub — ver `db/bootstrap/README.md`.
4. Rodar `scripts/openmetadata/bootstrap-custom-properties.sh` uma vez contra
   o ambiente real (ou criar as mesmas 4 propriedades manualmente em
   Settings → Custom Properties → Table).
5. Confirmar as repo variables `POSTGRES_MAIN_HOST`/`POSTGRES_MAIN_PORT`
   (já usadas pelo job `migrate`) — o job `catalog-metadata` reaproveita as
   duas para alcançar o `postgres-main` a partir do runner `homelab`, em vez
   do nome de serviço interno do Docker (`postgres-main`, que só resolve
   dentro da rede do Coolify).

## Uso local (dry-run, sem rede)

```bash
# AsyncAPI: imprime os payloads de messagingService/topics sem chamar a rede.
dotnet run --project scripts/openmetadata/register-rabbitmq -- --dry-run

# Data Contracts: imprime o JSON Patch simulado (entidade atual assumida
# vazia — a execução real busca o estado atual antes de montar o patch).
dotnet run --project scripts/openmetadata/register-data-contracts -- --dry-run
```

## Execução manual (fora do CI, ex.: depuração local contra o ambiente real)

```bash
export OPENMETADATA_BASE_URL="https://openmetadata.lab.tasso.dev.br/api"
export OPENMETADATA_INGESTION_JWT="<jwt do bot de ingestão>"

dotnet run --project scripts/openmetadata/register-rabbitmq
dotnet run --project scripts/openmetadata/register-data-contracts

# Ingestão Postgres/OpenAPI usam o CLI `metadata` (pacote openmetadata-ingestion):
export OM_SERVER_HOST_PORT="$OPENMETADATA_BASE_URL"
export OM_INGESTION_JWT="$OPENMETADATA_INGESTION_JWT"
export OM_POSTGRES_PASSWORD="<senha do role openmetadata_reader>"
export OM_POSTGRES_HOST_PORT="<host LAN do postgres-main>:5432"  # NÃO "postgres-main:5432"
metadata ingest -c scripts/openmetadata/ingestion-postgres.yaml
```

## Confirmação na UI

Depois de um push em `main` (ou de uma execução manual), confirmar em
`https://openmetadata.lab.tasso.dev.br`:

- schemas `catalog`/`booking`/`payment`/`integration` atualizados;
- `apiServices` `localize-stay-catalog-api` e `localize-stay-booking-api` com
  endpoints;
- `localize-stay-rabbitmq` com um tópico por canal AsyncAPI;
- cada tabela/view com Data Contract (ex.:
  `integration.reservation_calendar_v1`) com description, tag `localize-stay`
  e as 4 Custom Properties preenchidas.
