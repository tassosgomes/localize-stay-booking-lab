---
status: done
slice_type: vertical
verification_type: behavioral
parallelizable: true
blocked_by: [1.0, 2.0]
---

<task_context>
<domain>services/catalog</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database,http_server</dependencies>
<unblocks>"7.0, 8.0, 9.0"</unblocks>
<feedback_checkpoint>`dotnet test --filter "FullyQualifiedName~LocalizeStay.Catalog.IntegrationTests.HealthCheckTests"` verde, com o Postgres do Testcontainers respondendo Healthy; manualmente, `curl :5101/health/ready` contra `postgres-main` real retorna 200</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="LocalizeStay.Catalog.IntegrationTests.HealthCheckTests"</gate_command>
<gate_test_selector>Classe `HealthCheckTests` do projeto `services/catalog/tests/LocalizeStay.Catalog.IntegrationTests`</gate_test_selector>
<gate_expected_result>Teste(s) da classe `HealthCheckTests` passam (verde); 0 falhas; a migration inicial é aplicada no schema `catalog` e `/health/ready` retorna `Healthy` contra o Postgres do Testcontainers</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Catalog sobe (`dotnet run`), conecta no Postgres compartilhado com `catalog_role`, aplica a migration inicial no schema `catalog` e responde `/health/ready` consultando o Postgres real</vertical_slice>
</task_context>

# Tarefa 3.0: Catalog sobe e prova conexão real com Postgres (V-01)

## Relacionada as User Stories

- N/A — TechSpec Standalone. Cobre a fatia V-01 (`techspec.md`, Mapa de Fatias Verticais).

## Visão Geral

Primeira fatia vertical de infraestrutura: prova que o padrão "Clean Architecture por serviço +
Extensions por concern + EF Core no schema próprio via role dedicada" funciona ponta a ponta com uma
dependência real do `infra` (Postgres compartilhado), sem nenhuma regra de negócio. Este é o molde
que as tasks 4.0 (Booking) e 5.0 (Payment) replicam.

## Entrega Observável

- **Entrada ou gatilho:** `dotnet run --project services/catalog/src/1-Services/LocalizeStay.Catalog.Api`
- **Resultado esperado:** o serviço sobe, `GET /health/live` retorna `200 Healthy` sem depender de
  nada externo, e `GET /health/ready` retorna `200 Healthy` após consultar o Postgres real via
  `catalog_role`.
- **Checkpoint de feedback:** `dotnet test --filter "FullyQualifiedName~HealthCheckTests"` no projeto
  `LocalizeStay.Catalog.IntegrationTests` (Testcontainers Postgres) — verde. Manualmente (fora do
  gate automatizado): `curl :5101/health/ready` contra a instância real de `postgres-main` retorna
  `200`.
- **Seletor focalizado:** `LocalizeStay.Catalog.IntegrationTests.HealthCheckTests`
- **Fora deste checkpoint:** nenhuma regra de negócio de catálogo (isso é escopo de um PRD futuro);
  nenhuma verificação de CORS (isso é escopo da task 7.0); nenhuma mensageria (task 6.0 é só Booking).

## Requisitos

- Solution Clean Architecture (`API → Application → Domain`, `Infrastructure → Domain`) para Catalog,
  usando as convenções de `Directory.Build.props`/`Directory.Packages.props` da task 1.0.
- `Program.cs` enxuto (~20-40 linhas), só orquestrando chamadas a `Extensions/*`.
- `CatalogDbContext` conectando ao schema `catalog` de `localize_stay` autenticado como
  `catalog_role` (credenciais via `dotnet user-secrets`, nunca em `appsettings.json`).
- Migration inicial "sentinela": tabela `__bootstrap_check` (1 coluna, sem semântica de negócio) —
  prova que a role consegue criar/ler no próprio schema. Descartável: o primeiro PRD de negócio de
  Catalog a substitui pela primeira migration real.
- `GET /health/live` (liveness, sem dependência externa) e `GET /health/ready` (readiness, consulta o
  Postgres real).
- `GET /swagger` expõe um documento OpenAPI vazio/skeleton (infraestrutura de geração pronta para
  endpoints futuros).

## Arquivos Envolvidos

- **Criar:**
  - `services/catalog/LocalizeStay.Catalog.sln`
  - `services/catalog/.config/dotnet-tools.json`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Program.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Extensions/CorsExtensions.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Extensions/SwaggerExtensions.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Extensions/PersistenceExtensions.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Extensions/HealthCheckExtensions.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/appsettings.json` (sem segredo)
  - `services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/Persistence/CatalogDbContext.cs` +
    migration inicial (`__bootstrap_check`)
  - `services/catalog/tests/LocalizeStay.Catalog.IntegrationTests/HealthCheckTests.cs` (+ `.csproj`)
- **Modificar:**
  - `README.md` (raiz) — adicionar seção "Como rodar Catalog localmente" e "Como configurar
    `dotnet user-secrets`"
- **Referência:**
  - `db/bootstrap/*.sql` (task 2.0) — schema `catalog` e `catalog_role` já provisionados
  - `Directory.Build.props`, `Directory.Packages.props` (task 1.0)
  - `context/architecture-baseline.md` — Ownership de Dados, Comunicação, Observabilidade
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — Clean Architecture por serviço, monorepo (`examples/microservices.md`)
  - `dotnet-program-setup` — `Program.cs` enxuto, `Extensions/` por concern
  - `dotnet-dependency-config` — EF Core + Npgsql, `dotnet user-secrets`
  - `dotnet-observability` — health checks liveness/readiness separados
  - `dotnet-testing` — `WebApplicationFactory` + Testcontainers Postgres (`examples/integration-tests.md`)

## Subtarefas

- [ ] 3.1 Criar o esqueleto da solution Clean Architecture (`1-Services`..`4-Infra`) para Catalog
- [ ] 3.2 Implementar `Program.cs` e as 4 Extensions (Cors, Swagger, Persistence, HealthCheck)
- [ ] 3.3 Criar `CatalogDbContext` + migration inicial `__bootstrap_check` no schema `catalog` via
      `catalog_role`
- [ ] 3.4 Configurar `dotnet user-secrets` para a connection string e documentar no README
- [ ] 3.5 Criar `HealthCheckTests` (Testcontainers Postgres) cobrindo migration + `/health/ready`
- [ ] 3.6 Validar manualmente contra `postgres-main` real (`curl :5101/health/ready` → 200) — fora do
      gate automatizado, registrar evidência

## Sequenciamento

- Bloqueado por: 1.0 (convenções de build), 2.0 (database/role/schema `catalog` já provisionados)
- Desbloqueia: 7.0 (frontend precisa do `/health` de Catalog), 8.0 (contratos exportam o Swagger de
  Catalog), 9.0 (OpenMetadata cataloga o schema `catalog`)
- Paralelizável: Sim, com 4.0 e 5.0 — nenhum arquivo compartilhado entre os três serviços; a TechSpec
  descreve V-02 como "réplica do padrão de V-01" por precedente de código, não por dependência de
  artefato (ver nota de desvio em `tasks.md`)

## Rastreabilidade

- Esta tarefa cobre: Fatia V-01 da TechSpec.
- Evidência esperada: `HealthCheckTests` verde; migration `__bootstrap_check` aplicada no schema
  `catalog`; `curl :5101/health/ready` → 200 contra `postgres-main` real (evidência manual).

## Detalhes de Implementação

Da TechSpec (`techspec.md`, "Program.cs — padrão a replicar nos 3 serviços HTTP"):

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCorsConfiguration(builder.Configuration)
    .AddSwaggerConfiguration()
    .AddPersistenceConfiguration(builder.Configuration)
    .AddHealthCheckConfiguration(builder.Configuration);

var app = builder.Build();
app.UseApplicationPipeline(builder.Environment);
app.Run();
```

(Booking e Payment adicionam `.AddMessagingConfiguration(...)` a partir da task 6.0 — Catalog não
precisa desta chamada.)

Tabela de modelo de dados (TechSpec, "Modelos de Dados"): tabela sentinela `__bootstrap_check`, 1
coluna, sem semântica de negócio — só prova que a migration + role funcionam. Descartável pelo
primeiro PRD de negócio de Catalog.

Endpoints (TechSpec, "Endpoints de API"): `GET /health/live` (liveness, sem dependência externa),
`GET /health/ready` (readiness, consulta o Postgres real via `HealthCheckExtensions`), `GET /swagger`
(documento OpenAPI vazio/skeleton).

**Convenções da stack (das skills consultadas):**
- Repository/DbContext isolado em `4-Infra`, sem vazar `Npgsql`/EF Core para `Program.cs`.
- Testes seguem Arrange-Act-Assert, usando `CustomWebApplicationFactory` com
  `PostgreSqlBuilder().WithImage("postgres:16-alpine")` (mesma imagem usada em produção), conforme
  `dotnet-testing/examples/integration-tests.md`.
- Logs estruturados básicos (sem OpenTelemetry nesta fase, conforme baseline).

## Prontidão para Implementação

- **Decisões fechadas:** nome do schema (`catalog`) e da role (`catalog_role`) já fixados por 2.0;
  porta do serviço `5101` (usada no checkpoint manual); tabela sentinela `__bootstrap_check` é
  descartável e não deve virar modelo de domínio.
- **Limites de decisão do implementer:** organização interna de `Extensions/*` além do mínimo exigido
  (Cors, Swagger, Persistence, HealthCheck); nome exato da migration inicial.
- **Dependências disponíveis:** `Directory.Build.props`/`Directory.Packages.props` (1.0), schema
  `catalog` + `catalog_role` provisionados em `localize_stay` (2.0).
- **Artefatos exigidos pelo gate:** `HealthCheckTests.cs` e o projeto de testes são criados nesta
  própria task; o Testcontainers Postgres é efêmero (não versionado); o schema/role reais usados na
  verificação manual vêm de 2.0 (já executados).
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Catalog.IntegrationTests.HealthCheckTests"`
- [ ] O seletor encontra pelo menos um teste e não executa casos sem relação com esta task
- [ ] Build compila sem erros: `dotnet build services/catalog/LocalizeStay.Catalog.sln`
- [ ] `GET /health/live` responde `200 Healthy` sem depender de Postgres (testável derrubando a
      dependência)
- [ ] `GET /health/ready` responde `200 Healthy` quando o Postgres do Testcontainers está saudável e
      um código de erro (não 200) quando indisponível
- [ ] Nenhum warning de compilação (`TreatWarningsAsErrors=true` herdado de `Directory.Build.props`)
- [ ] Checkpoint de feedback executado: `curl :5101/health/ready` contra `postgres-main` real → `200`
      (evidência manual, registrada fora do gate automatizado)
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente esta fatia (Catalog) e não depende de Booking/Payment/frontend
