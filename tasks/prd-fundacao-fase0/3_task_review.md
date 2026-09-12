# Revisão — Task 3.0: Catalog sobe e prova conexão real com Postgres (V-01) — focused, 1ª revisão

## Gate (estágio 1, antes de material semântico)

- Contrato lido da task (`3_task.md:4-21`): `slice_type: vertical`, `verification_type: behavioral`,
  `gate_command: scripts/ai-flow/gate.sh --filter="LocalizeStay.Catalog.IntegrationTests.HealthCheckTests"`,
  esperado: `HealthCheckTests` verde, 0 falhas, migration no schema `catalog`, `/health/ready` Healthy
  contra Postgres do Testcontainers.
- Comando executado pelo validator (worktree `/home/tsgomes/github-tassosgomes/localize-stay-booking-lab-prd-fundacao-fase0`,
  branch `feature/prd-fundacao-fase0`): `scripts/ai-flow/gate.sh --filter="LocalizeStay.Catalog.IntegrationTests.HealthCheckTests"`
- Resultado: `GATE: APROVADO` — `arquivos alterados: 32 (.NET: 23, node: 0)`,
  `format: dotnet format ok (services/catalog/LocalizeStay.Catalog.sln: 16 arquivos)`,
  `build: dotnet build ok (services/catalog/LocalizeStay.Catalog.sln 0 Warning(s) 0 Error(s))`,
  `testes: ok (LocalizeStay.Catalog.IntegrationTests.HealthCheckTests=4)` — exit 0.
- Gate executado ANTES da leitura semântica além do contrato; nenhuma aprovação do implementer reutilizada.

## Evidência comportamental reexecutada pelo validator (Testcontainers/Docker disponível)

- `dotnet test services/catalog/LocalizeStay.Catalog.sln --filter "FullyQualifiedName~LocalizeStay.Catalog.IntegrationTests.HealthCheckTests" --nologo`
  → `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 5 s` — exit 0.
  Os 4 testes são exatamente a classe `HealthCheckTests` (`HealthCheckTests.cs:14-84`):
  migration aplica `__bootstrap_check` em `catalog`, `ready` Healthy com banco no ar,
  `live` Healthy, e `ready` ≠ 200 + `live` = 200 com Postgres derrubado (`StopDatabaseAsync`).
- `dotnet build services/catalog/LocalizeStay.Catalog.sln --nologo` → `6 projects, 0 errors, 0 warnings` — exit 0
  (`TreatWarningsAsErrors=true` herdado respeitado; nenhum warning).
- Testcontainers com imagem `postgres:16-alpine` (`CustomWebApplicationFactory.cs:14`), mesma imagem de produção
  exigida pela task; container efêmero, sem tocar em `postgres-main` real.
- Evidência manual contra `postgres-main` real (`curl :5101/health/ready` → 200, subtask 3.6): NÃO executada
  neste ambiente. Ausência registrada como limitação, NÃO como reprovação — o gate/Testcontainers prova o
  comportamento automatizável; o manual é opcional do ambiente conforme instrução da convocação.

## Escopo revisado

- Base: checkpoint `8b100c3` (`checkpoint(task 2.0)`); HEAD durante a revisão = `8b100c3` (nenhum commit novo;
  `8b100c3 is ancestor of HEAD` confirmado). Diff revisado = working tree + untracked.
- Modificados rastreados (5): `.gitignore`, `Directory.Packages.props`, `README.md`,
  `tasks/prd-fundacao-fase0/3_task.md` (status `validating`), `tasks/prd-fundacao-fase0/flow-state.json`.
- Untracked do escopo: `NuGet.Config`, `services/catalog/.config/dotnet-tools.json`,
  `services/catalog/LocalizeStay.Catalog.sln`, `Api/Program.cs`, `Api/Extensions/{Cors,Swagger,Persistence,HealthCheck}.cs`,
  `Api/appsettings.json`, `Api/*.csproj`, `Application/*.csproj` (só csproj),
  `Domain/*.csproj` (só csproj), `Infra/*.csproj`, `Infra/Persistence/{CatalogDbContext,BootstrapCheck,BootstrapCheckConfiguration}.cs`,
  `Infra/Persistence/HealthChecks/CatalogReadinessCheck.cs`, `Infra/Migrations/{20260912130434_InitialCreate,Designer,Snapshot}.cs`,
  `IntegrationTests/{HealthCheckTests,CustomWebApplicationFactory,CatalogIntegrationTestCollection}.cs` + csproj.
- Fora do escopo declarado mas presente como untracked: `scripts/ai-flow/gate.sh` + `gate.contract.md`
  (ferramenta do flow, não artefato da task; ver recomendação 3). `bin/`/`obj/` existem em disco mas estão
  cobertos pelo `.gitignore` novo e excluídos do `git ls-files --others --exclude-standard` — não versionados.
- Skills pertinentes conferidas por inspeção de conformidade (sem necessidade de abrir guias além do exigido):
  `dotnet-architecture` (Clean Architecture `1-Services→2-Application→3-Domain`, `4-Infra→Domain`),
  `dotnet-program-setup` (`Program.cs` 17 linhas, só orquestração),
  `dotnet-dependency-config` (CPM + user-secrets, sem segredo versionado),
  `dotnet-observability` (liveness/readiness separados por tags),
  `dotnet-testing` (`WebApplicationFactory` + Testcontainers, AAA).

## Conformidade (checklist da task + atenções especiais)

- `Program.cs` enxuto (17 linhas, `Program.cs:1-17`), orquestrando `AddCors/Swagger/Persistence/HealthCheckConfiguration`
  + `UseApplicationPipeline` — conforme TechSpec/tresl. Assinatura difere em detalhe não-bloqueante
  (`AddHealthCheckConfiguration()` sem `IConfiguration`, contra `.AddHealthCheckConfiguration(builder.Configuration)`
  no snippet da task `3_task.md:131`); sem config necessária, ver recomendação 1.
- (a) `MiddlewarePipelineExtensions.cs` extra: JUSTIFICADO, não desvio. O snippet exigido pela task
  (`3_task.md:133`: `app.UseApplicationPipeline(builder.Environment)`) exige esse tipo; sem ele `Program.cs`
  não compilaria. Limite de decisão do implementer (`3_task.md:161-162`) permite organização interna de
  `Extensions/*` além das 4 exigidas. Conteúdo mínimo e correto (`MiddlewarePipelineExtensions.cs:8-19`:
  Swagger + CORS + MapHealthChecks, sem `UseHttpsRedirection` com justificativa de lab local).
- (b) Pins adicionais: NECESSÁRIOS, não escopo estourado. `Mvc.Testing 10.0.11` (exigido por
  `CustomWebApplicationFactory : WebApplicationFactory<Program>`), `HealthChecks 10.0.11` (referência direta
  em `Infra.csproj:9` para `CatalogReadinessCheck`), `EFCore 10.0.12` + `EFCore.Relational 10.0.12`
  (unificam transitivas do `Npgsql 10.0.3` que fixa EF Core >= 10.0.4; sem isto MSB3277 + `TreatWarningsAsErrors`
  quebra o build — comentário em `Directory.Packages.props:4-6`), `CentralPackageTransitivePinningEnabled=true`
  (mesma razão). Nenhum serviço fixa versão individualmente.
- (c) `Application`/`Domain` vazios: CONFORME. Só csprojs, sem `.cs`; `Application` referencia `Domain`
  (`Application.csproj:8`), `Infra` referencia `Domain` (`Infra.csproj:14`), `Api` referencia
  `Application`+`Infra` — `API → Application → Domain`, `Infrastructure → Domain`, sem regra de negócio.
- (d) Migration sentinela: SÓ `__bootstrap_check`, sem negócio. `Up` cria `catalog.__bootstrap_check(Id uuid PK)`
  (`20260912130434_InitialCreate.cs:17-27`); `BootstrapCheck` 1 coluna `Id` (`BootstrapCheck.cs:8`);
  comentário declara descartável (`BootstrapCheck.cs:3-5`); snapshot confirma `ToTable("__bootstrap_check","catalog")`
  + `HasDefaultSchema("catalog")`.
- (e) Sem segredo versionado: OK. `appsettings.json:4` tem `"Catalog": ""`; `UserSecretsId: localizestay-catalog-dev`
  (`Api.csproj:5`); README documenta `user-secrets set/list` + `ConnectionStrings__Catalog` para CI/deploy sem
  valor real; grep por `Password=|secret` em `services/catalog` só acha placeholder efêmero
  `postgres/postgres` no `CustomWebApplicationFactory.cs:29` (container de teste) + mensagem de erro orientando
  `user-secrets` (`PersistenceExtensions.cs:17`). `NuGet.Config` só tem source `nuget.org` + mapping (NU1504).
- (f) Ownership `catalog_role` / schema `catalog`: RESPEITADO no código versionado.
  `HasDefaultSchema("catalog")` (`CatalogDbContext.cs:11`), `EnsureSchema("catalog")` + tabela em `catalog`
  (migration), `MigrationsHistoryTable("__EFMigrationsHistory","catalog")` (`PersistenceExtensions.cs:22`,
  `CustomWebApplicationFactory.cs:37-38,66-67`), readiness consulta `BootstrapChecks` no schema próprio
  (`CatalogReadinessCheck.cs:21`). Teste usa superuser efêmero do container (padrão Testcontainers), enquanto o
  caminho produtivo documentado no README usa `Username=catalog_role` contra `postgres-main` — separação correta.
- Critérios de sucesso: seletor encontra 4 testes e nenhum caso alheio; `/health/live` sem dependência externa
  (tag `live`, `HealthCheckExtensions.cs:15,23-26`); `/health/ready` Healthy com banco e não-200 sem banco
  (tag `ready` + `CatalogReadinessCheck`, coberto pelo 4º teste); Swagger skeleton em `/swagger`
  (`SwaggerExtensions.cs:20-29`); CORS infra pronta com `AllowedOrigins` vazio (sem verificação, fora do checkpoint
  pois CORS é task 7.0 — correto não verificar aqui); todos os artefatos do gate criados nesta task; nada de
  Booking/Payment/frontend exigido.
- `.gitignore` adiciona `[Bb]in/[Oo]bj//TestResults/` com justificativa (evita `.cs` gerados em `obj/` no
  `dotnet format` do gate) — dentro do escopo permitido.

## Bloqueantes

Nenhum. Arquivo/linha: —.

## Recomendações (3, não bloqueantes)

1. `Api/Program.cs:9` + `Extensions/HealthCheckExtensions.cs:12` — assinatura `AddHealthCheckConfiguration()`
   diverge do snippet da task/techspec (`3_task.md:131`: `.AddHealthCheckConfiguration(builder.Configuration)`).
   Funcionalmente correto (nenhuma config lida), mas considere alinhar a assinatura (aceitar e ignorar
   `IConfiguration`) ou registrar o desvio em `techspec.md`/comentário para Booking/Payment replicarem sem dúvida.
2. `tests/.../CustomWebApplicationFactory.cs:22-30,53-68` — placeholder via `Environment.SetEnvironmentVariable`
   no ctor + substituição em `ConfigureTestServices` funciona, mas o duplo caminho de connection string é sutil
   (placeholder `localhost` nunca usado de fato). Considere comentário já existente suficiente; alternativamente,
   um `TryAdd`-guard ou falha explícita documentada evitaria confusão futura. Não bloqueia.
3. `scripts/ai-flow/gate.sh`, `scripts/ai-flow/gate.contract.md` untracked fora do escopo `services/catalog/**` —
   pertencem ao ferramental do flow, não à fatia V-01. Considere commit separado ou declaração de ownership para
   não poluir o diff da task 3.0 nas próximas revisões. Não bloqueia.

## Imutabilidade

- HEAD antes: `8b100c3703126c2b11cd2f73bf9ad5538c60a07b`; HEAD depois: idêntico (confirmado por `git rev-parse HEAD`
  após `dotnet test` + `dotnet build`).
- Nenhum código, status, task ou commit editado pelo validator; working tree inalterado
  (5 modificados + `NuGet.Config`, `scripts/`, `services/` untracked, como no início) — só este relatório criado em
  `tasks/prd-fundacao-fase0/3_task_review.md`.
- Comandos e resultados acima: gate exit 0, `dotnet test` exit 0 (4 passed), `dotnet build` exit 0 (0W/0E).

## Veredito

**VALIDAÇÃO APROVADA (3 recomendações)**
