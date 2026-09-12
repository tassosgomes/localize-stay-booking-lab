# localize-stay-booking-lab

## Como rodar Catalog localmente

Pré-requisitos: .NET 10 SDK, Docker (para os Testcontainers dos testes) e
acesso de rede ao `postgres-main` com o bootstrap `db/bootstrap` já executado
(database `localize_stay`, schemas e `catalog_role` provisionados).

```bash
# 1. Apontar para o Postgres real (ver seção user-secrets abaixo)
dotnet user-secrets set "ConnectionStrings:Catalog" \
  "Host=<postgres-main-host>;Port=5432;Database=localize_stay;Username=catalog_role;Password=<senha-do-operador>" \
  --project services/catalog/src/1-Services/LocalizeStay.Catalog.Api

# 2. Aplicar a migration inicial no schema `catalog` (step de deploy, nunca no boot)
ConnectionStrings__Catalog="<mesma-string>" \
  dotnet ef database update \
  --project services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra \
  --startup-project services/catalog/src/1-Services/LocalizeStay.Catalog.Api

# 3. Subir o serviço (porta 5101)
dotnet run --project services/catalog/src/1-Services/LocalizeStay.Catalog.Api

# 4. Checar
curl localhost:5101/health/live   # 200 Healthy, sem depender do banco
curl localhost:5101/health/ready  # 200 Healthy, consultando o Postgres real
```

Swagger (skeleton, sem endpoints de negócio ainda): `http://localhost:5101/swagger`.

Testes de integração (Postgres efêmero via Testcontainers, sem tocar no
`postgres-main` real):

```bash
dotnet test services/catalog/LocalizeStay.Catalog.sln \
  --filter "LocalizeStay.Catalog.IntegrationTests.HealthCheckTests"
```

## Como rodar Booking localmente

Mesmo padrão do Catalog (réplica estrutural, solution própria), com schema
`booking`, role `booking_role` e porta 5102. Pré-requisitos: .NET 10 SDK,
Docker (para os Testcontainers dos testes) e acesso de rede ao `postgres-main`
com o bootstrap `db/bootstrap` já executado (database `localize_stay`, schemas
e `booking_role` provisionados).

```bash
# 1. Apontar para o Postgres real (user-secrets `localizestay-booking-dev`)
dotnet user-secrets set "ConnectionStrings:Booking" \
  "Host=<postgres-main-host>;Port=5432;Database=localize_stay;Username=booking_role;Password=<senha-do-operador>" \
  --project services/booking/src/1-Services/LocalizeStay.Booking.Api

# 2. Aplicar a migration inicial no schema `booking` (step de deploy, nunca no boot)
ConnectionStrings__Booking="<mesma-string>" \
  dotnet ef database update \
  --project services/booking/src/4-Infra/LocalizeStay.Booking.Infra \
  --startup-project services/booking/src/1-Services/LocalizeStay.Booking.Api

# 3. Subir o serviço (porta 5102)
dotnet run --project services/booking/src/1-Services/LocalizeStay.Booking.Api

# 4. Checar
curl localhost:5102/health/live   # 200 Healthy, sem depender do banco
curl localhost:5102/health/ready  # 200 Healthy, consultando o Postgres real
```

Swagger (skeleton, sem endpoints de negócio ainda): `http://localhost:5102/swagger`.

Testes de integração (Postgres efêmero via Testcontainers, sem tocar no
`postgres-main` real):

```bash
dotnet test services/booking/LocalizeStay.Booking.sln \
  --filter "LocalizeStay.Booking.IntegrationTests.HealthCheckTests"
```

## Como configurar `dotnet user-secrets`

Nenhuma credencial vive em `appsettings*.json` versionado. Cada desenvolvedor
guarda a sua connection string no user-secrets do projeto Api
(`UserSecretsId: localizestay-catalog-dev`):

```bash
dotnet user-secrets set "ConnectionStrings:Catalog" \
  "Host=<postgres-main-host>;Port=5432;Database=localize_stay;Username=catalog_role;Password=<sua-senha>" \
  --project services/catalog/src/1-Services/LocalizeStay.Catalog.Api

dotnet user-secrets list \
  --project services/catalog/src/1-Services/LocalizeStay.Catalog.Api
```

A senha é a definida pelo operador ao executar `db/bootstrap/002-roles.sql`
(ver `db/bootstrap/README.md`). Em CI/deploy futuro, a mesma chave chega via
variável de ambiente `ConnectionStrings__Catalog`.