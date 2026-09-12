# Revisão — Task 4.0: Booking sobe com o mesmo padrão de Catalog (V-02) — focused, 1ª revisão

## Gate (estágio 1, antes de material semântico)

- Contrato lido da task (`4_task.md:4-21`): `slice_type: vertical`, `verification_type: behavioral`,
  `gate_command: scripts/ai-flow/gate.sh --filter="LocalizeStay.Booking.IntegrationTests.HealthCheckTests"`,
  esperado: `HealthCheckTests` verde, 0 falhas, migration inicial aplicada no schema `booking` e
  `/health/ready` `Healthy` contra o Postgres do Testcontainers.
- Comando executado pelo validator (worktree `/home/tsgomes/github-tassosgomes/localize-stay-booking-lab-prd-fundacao-fase0`,
  branch `feature/prd-fundacao-fase0`): `scripts/ai-flow/gate.sh --filter="LocalizeStay.Booking.IntegrationTests.HealthCheckTests"`
- Resultado: `GATE: APROVADO` — `arquivos alterados: 29 (.NET: 22, node: 0)`,
  `format: dotnet format ok (services/booking/LocalizeStay.Booking.sln: 16 arquivos)`,
  `build: dotnet build ok (services/booking/LocalizeStay.Booking.sln 0 Warning(s) 0 Error(s)); dotnet build ok (services/catalog/LocalizeStay.Catalog.sln 0 Warning(s) 0 Error(s))`,
  `testes: ok (LocalizeStay.Booking.IntegrationTests.HealthCheckTests=4)` — exit 0.
- Gate executado ANTES da leitura semântica além do contrato; nenhuma aprovação do implementer reutilizada.

## Evidência comportamental reexecutada pelo validator (Testcontainers/Docker disponível)

- `dotnet test services/booking/LocalizeStay.Booking.sln --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.HealthCheckTests"`
  → `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 3 s` — exit 0.
  Os 4 testes são exatamente a classe `HealthCheckTests` (`HealthCheckTests.cs:14-84`):
  migration aplica `__bootstrap_check` em `booking`, `ready` Healthy com banco no ar,
  `live` Healthy, e `ready` ≠ 200 + `live` = 200 com Postgres derrubado (`StopDatabaseAsync`).
- O gate já havia compilado ambas as solutions com `0 Warning(s) 0 Error(s)`, confirmando que Booking
  não quebrou Catalog (paralelizável sem arquivos compartilhados).
- Testcontainers com imagem `postgres:16-alpine` (`CustomWebApplicationFactory.cs:14`), container efêmero,
  sem tocar em `postgres-main` real.
- Evidência manual contra `postgres-main` real (`curl :5102/health/ready` → 200, subtask 4.6): NÃO executada
  neste ambiente. `localhost:5432` sem listener, nenhuma env `postgres*` presente; binário `curl` existe mas
  não há `postgres-main` alcançável. Ausência registrada como limitação, NÃO como reprovação — conforme
  instrução da convocação ("Ausência do curl no postgres-main real NÃO reprova").

## Escopo revisado

- Base: checkpoint `ae08fa3` (`checkpoint(task 3.0)`); HEAD durante a revisão = `ae08fa3ea64233e913489404bb96005e51274478`
  (nenhum commit novo; HEAD == checkpoint). Diff revisado = working tree + untracked.
- Modificados rastreados (3): `README.md` (+38 seção Booking), `tasks/prd-fundacao-fase0/4_task.md`
  (`pending` → `validating`), `tasks/prd-fundacao-fase0/flow-state.json` (`active_task` → `4.0`,
  `last_checkpoint` → `ae08fa3`). Mudanças de status/state são do flow, não lógica de negócio.
- Untracked do escopo (`services/booking/**`, 26 arquivos): `LocalizeStay.Booking.sln`,
  `.config/dotnet-tools.json`, `Api/Program.cs`, `Api/Extensions/{Cors,Swagger,Persistence,HealthCheck,MiddlewarePipelineExtensions}.cs`,
  `Api/appsettings.json`, `Api/*.csproj`, `Application/*.csproj` (só csproj), `Domain/*.csproj` (só csproj),
  `Infra/*.csproj`, `Infra/Persistence/{BookingDbContext,BootstrapCheck,BootstrapCheckConfiguration}.cs`,
  `Infra/Persistence/HealthChecks/BookingReadinessCheck.cs`,
  `Infra/Migrations/{20260912131607_InitialCreate,Designer,Snapshot}.cs`,
  `IntegrationTests/{HealthCheckTests,CustomWebApplicationFactory,BookingIntegrationTestCollection}.cs` + csproj.
- Fora do escopo declarado mas presente como untracked: `scripts/ai-flow/gate.sh` + `gate.contract.md`
  (ferramenta do flow, não artefato da task; ver recomendação 1). `bin/`/`obj/` existem em disco mas estão
  cobertos por `.gitignore` — não versionados.
- Skills pertinentes conferidas por inspeção de conformidade: `dotnet-architecture` (Clean Architecture
  `1-Services→2-Application→3-Domain`, `4-Infra→Domain`), `dotnet-program-setup` (`Program.cs` 17 linhas,
  só orquestração), `dotnet-dependency-config` (CPM + user-secrets, sem segredo versionado),
  `dotnet-observability` (liveness/readiness separados por tags), `dotnet-testing`
  (`WebApplicationFactory` + Testcontainers, AAA).

## Conformidade (checklist da task + atenções especiais)

- (a) Réplica fiel do padrão Catalog, sem divergência estrutural injustificada: CONFERE. `Program.cs` 17 linhas
  idêntico ao Catalog salvo namespace (`Catalog.Api.Extensions` → `Booking.Api.Extensions`, verificado via
  `difflib`); mesmo conjunto de 5 Extensions com os mesmos nomes; `difflib` Catalog×Booking mostra só renames:
  `PersistenceExtensions` (`Catalog`→`Booking`, `catalog`→`booking` no `MigrationsHistoryTable`),
  `HealthCheckExtensions` (`CatalogReadinessCheck`/`catalog-db` → `BookingReadinessCheck`/`booking-db`),
  `SwaggerExtensions` (título `Catalog API` → `Booking API`), `CorsExtensions` (só namespace).
  `Api.csproj` idêntico salvo `UserSecretsId` (`localizestay-catalog-dev` → `localizestay-booking-dev`).
  `MiddlewarePipelineExtensions.cs` existe nos dois serviços (Swagger + CORS + MapHealthChecks, sem
  `UseHttpsRedirection` com justificativa de lab local) — não é desvio.
- (b) Schema `booking` / `booking_role` ownership: RESPEITADO no código versionado.
  `HasDefaultSchema("booking")` (`BookingDbContext.cs:11`), `EnsureSchema("booking")` + tabela em `booking`
  (migration `20260912131607_InitialCreate.cs:14-27`), `MigrationsHistoryTable("__EFMigrationsHistory","booking")`
  (`PersistenceExtensions.cs:22`, `CustomWebApplicationFactory.cs:37-38,66-67`), readiness consulta
  `BootstrapChecks` no schema próprio (`BookingReadinessCheck.cs:21`). Teste usa superuser efêmero do container
  (padrão Testcontainers), enquanto o caminho produtivo documentado no README usa `Username=booking_role`
  contra `postgres-main` — separação correta, igual à 3.0.
- (c) Sentinela sem negócio: SÓ `__bootstrap_check`, sem regra de reserva. `Up` cria
  `booking.__bootstrap_check(Id uuid PK)`; `BootstrapCheck` 1 coluna `Id` (`BootstrapCheck.cs:8`);
  comentário declara descartável e sem semântica (`BootstrapCheck.cs:3-5`); `BookingDbContext.BootstrapChecks`
  `internal`, consistente com sentinela de fundação.
- (d) Sem segredos versionados: OK. `appsettings.json:3-5` tem `"Booking": ""`; `UserSecretsId:
  localizestay-booking-dev` (`Api.csproj:5`); README documenta `user-secrets set` + `ConnectionStrings__Booking`
  para deploy com placeholders `<postgres-main-host>` / `<senha-do-operador>` e `<mesma-string>`, sem valor real;
  grep por `Password|secret` em `services/booking` só acha placeholder efêmero `postgres/postgres` no
  `CustomWebApplicationFactory.cs:17,29` (container de teste) + mensagem de erro orientando `user-secrets`
  (`PersistenceExtensions.cs:17`).
- (e) NADA de mensageria/diagnóstico (escopo da 6.0): CONFERE — AUSENTE como exigido. Grep por
  `Messaging|AddMessaging|diagnostics|ping|RabbitMq|MassTransit|Kafka|ServiceBus` em `services/booking` →
  0 matches. `Program.cs:5-10` sem `.AddMessagingConfiguration(...)`; sem `MessagingExtensions.cs`; sem
  `POST /internal/diagnostics/ping`; endpoints só `GET /health/live`, `GET /health/ready`, `GET /swagger`
  (`HealthCheckExtensions.cs:23-30`, `MiddlewarePipelineExtensions.cs:14-16`, `SwaggerExtensions.cs:20-29`).
  Não há motivo para REPROVAR por escopo.
- (f) Porta 5102 sem colisão: OK. `"urls": "http://localhost:5102"` (`appsettings.json:2`) vs Catalog `5101`;
  README documenta `curl localhost:5102/health/live|ready` + Swagger `http://localhost:5102/swagger`.
- Critérios de sucesso: seletor encontra 4 testes e nenhum caso alheio; `dotnet build` 0W/0E (gate cobre ambas
  as slns); `/health/live` sem dependência externa (tag `live`); `/health/ready` Healthy com banco e erro sem
  banco (4º teste); Swagger skeleton em `/swagger`; CORS infra pronta com `AllowedOrigins` vazio (sem verificação,
  correto pois CORS é task 7.0); todos os artefatos do gate criados nesta task; nada de Payment/mensageria exigido.

## Bloqueantes

Nenhum. Arquivo/linha: —.

## Recomendações (2, não bloqueantes)

1. `scripts/ai-flow/gate.sh`, `scripts/ai-flow/gate.contract.md` untracked fora do escopo `services/booking/**` —
   pertencem ao ferramental do flow, não à fatia V-02. Mesmo ponto da revisão 3.0 (rec. 3): considere commit
   separado ou declaração de ownership para não poluir o diff das tasks. Não bloqueia.
2. `tests/.../CustomWebApplicationFactory.cs:22-30,53-68` — placeholder via `Environment.SetEnvironmentVariable`
   no ctor + substituição em `ConfigureTestServices` replica o padrão 3.0 e funciona, mas o duplo caminho de
   connection string continua sutil (placeholder `localhost` nunca usado de fato). Manter o comentário existente;
   opcionalmente alinhar com Catalog numa futura nota de padrão. Não bloqueia.

## Imutabilidade

- HEAD antes: `ae08fa3ea64233e913489404bb96005e51274478`; HEAD depois: idêntico (confirmado por `git rev-parse HEAD`
  após gate + `dotnet test`).
- Nenhum código, status, task ou commit editado pelo validator; working tree inalterado
  (3 modificados + `scripts/`, `services/booking/` untracked, como no início) — só este relatório criado em
  `tasks/prd-fundacao-fase0/4_task_review.md`.
- Comandos e resultados acima: gate exit 0 (`GATE: APROVADO`, 4 testes), `dotnet test` reexecutado exit 0 (4 passed).

## Veredito

**VALIDAÇÃO APROVADA (2 recomendações)**
