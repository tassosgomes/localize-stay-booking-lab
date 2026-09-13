# Revisão — Task 5.0: Payment sobe com o mesmo padrão de Catalog (V-02) — focused, 1ª revisão

## Gate (estágio 1, antes de material semântico)

- Contrato lido da task (`5_task.md:4-21`): `slice_type: vertical`, `verification_type: behavioral`,
  `gate_command: scripts/ai-flow/gate.sh --filter="LocalizeStay.Payment.IntegrationTests.HealthCheckTests"`,
  esperado: `HealthCheckTests` verde, 0 falhas, migration inicial aplicada no schema `payment` e
  `/health/ready` `Healthy` contra o Postgres do Testcontainers.
- Comando executado pelo validator (worktree `/home/tsgomes/github-tassosgomes/localize-stay-booking-lab-prd-fundacao-fase0`,
  branch `feature/prd-fundacao-fase0`): `scripts/ai-flow/gate.sh --filter="LocalizeStay.Payment.IntegrationTests.HealthCheckTests"`
- Resultado: `GATE: APROVADO` — `arquivos alterados: 29 (.NET: 22, node: 0)`,
  `format: dotnet format ok (services/payment/LocalizeStay.Payment.sln: 16 arquivos)`,
  `build: dotnet build ok (services/booking 0W/0E); dotnet build ok (services/catalog 0W/0E); dotnet build ok (services/payment 0W/0E)`,
  `testes: ok (LocalizeStay.Payment.IntegrationTests.HealthCheckTests=4)` — exit 0.
- Gate executado ANTES da leitura semântica além do contrato; nenhuma aprovação do implementer reutilizada.

## Evidência comportamental reexecutada pelo validator (Testcontainers/Docker disponível)

- `dotnet test services/payment/LocalizeStay.Payment.sln --filter "FullyQualifiedName~LocalizeStay.Payment.IntegrationTests.HealthCheckTests"`
  → `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 4 s` — exit 0.
  Os 4 testes são exatamente a classe `HealthCheckTests` (`HealthCheckTests.cs:14-84`):
  migration aplica `__bootstrap_check` em `payment`, `ready` Healthy com banco no ar,
  `live` Healthy, e `ready` ≠ 200 + `live` = 200 com Postgres derrubado (`StopDatabaseAsync`).
- O gate já havia compilado as três solutions com `0 Warning(s) 0 Error(s)`, confirmando que Payment
  não quebrou Catalog/Booking (paralelizável sem arquivos compartilhados).
- Testcontainers com imagem `postgres:16-alpine` (`CustomWebApplicationFactory.cs:14`), container efêmero,
  sem tocar em `postgres-main` real.
- Evidência manual contra `postgres-main` real (`curl :5103/health/ready` → 200, subtask 5.6): NÃO executada
  neste ambiente. `getent hosts postgres-main` → `NO-DNS-postgres-main`; sem `postgres-main` alcançável.
  Ausência registrada como limitação, NÃO como reprovação — conforme instrução da convocação
  ("Ausência do curl postgres-main real NÃO reprova").

## Escopo revisado

- Base: checkpoint `a73987a` (`checkpoint(task 4.0)`); HEAD durante a revisão = `a73987a7f1e4ca2d9940e2f31d36682b486585c6`
  (nenhum commit novo; HEAD == checkpoint). Diff revisado = working tree + untracked.
- Modificados rastreados (3): `README.md` (+38 seção Payment), `tasks/prd-fundacao-fase0/5_task.md`
  (`pending` → `validating`), `tasks/prd-fundacao-fase0/flow-state.json` (`active_task` → `5.0`,
  `last_checkpoint` → `a73987a`). Mudanças de status/state são do flow, não lógica de negócio.
- Untracked do escopo (`services/payment/**`, .NET: 22 arquivos): `LocalizeStay.Payment.sln`,
  `.config/dotnet-tools.json`, `Api/Program.cs`, `Api/Extensions/{Cors,Swagger,Persistence,HealthCheck,MiddlewarePipelineExtensions}.cs`,
  `Api/appsettings.json`, `Api/*.csproj`, `Application/*.csproj` (só csproj), `Domain/*.csproj` (só csproj),
  `Infra/*.csproj`, `Infra/Persistence/{PaymentDbContext,BootstrapCheck,BootstrapCheckConfiguration}.cs`,
  `Infra/Persistence/HealthChecks/PaymentReadinessCheck.cs`,
  `Infra/Migrations/{20260912132452_InitialCreate,Designer,Snapshot}.cs`,
  `IntegrationTests/{HealthCheckTests,CustomWebApplicationFactory,PaymentIntegrationTestCollection}.cs` + csproj.
- Fora do escopo declarado mas presente como untracked: `scripts/` (ferramenta do flow, não artefato da task;
  ver recomendação 1). `bin/`/`obj/` existem em disco mas estão cobertos por `.gitignore` — não versionados.
- Skills pertinentes conferidas por inspeção de conformidade: `dotnet-architecture` (Clean Architecture
  `1-Services→2-Application→3-Domain`, `4-Infra→Domain`), `dotnet-program-setup` (`Program.cs` 17 linhas,
  só orquestração), `dotnet-dependency-config` (CPM + user-secrets, sem segredo versionado),
  `dotnet-observability` (liveness/readiness separados por tags), `dotnet-testing`
  (`WebApplicationFactory` + Testcontainers, AAA).

## Conformidade (checklist da task + atenções especiais)

- (a) Réplica fiel do padrão Catalog/Booking, sem divergência estrutural injustificada: CONFERE. `Program.cs` 17 linhas
  idêntico ao Booking salvo namespace (`Booking.Api.Extensions` → `Payment.Api.Extensions`, verificado via `diff`);
  diffs normalizados Booking×Payment (renames `Booking/booking` ↔ `Payment/payment`) mostram IDENTICAL-MODULO-NAMING em:
  `PersistenceExtensions`, `HealthCheckExtensions`, `CorsExtensions`, `SwaggerExtensions`,
  `MiddlewarePipelineExtensions`, `BookingDbContext`→`PaymentDbContext`, `BootstrapCheck(.cs)` e `HealthCheckTests.cs`.
  Divergências restantes são as exigidas (schema `payment`, título `Payment API`, porta 5103,
  `UserSecretsId` `localizestay-payment-dev`). `MiddlewarePipelineExtensions.cs` existe nos três serviços
  (Swagger + CORS + MapHealthChecks, sem `UseHttpsRedirection` com justificativa de lab local) — não é desvio,
  embora a lista de arquivos da task cite só 4 Extensions.
- (b) Schema `payment` / `payment_role` ownership: RESPEITADO no código versionado.
  `HasDefaultSchema("payment")` (`PaymentDbContext.cs:11`), `EnsureSchema("payment")` + tabela em `payment`
  (migration `20260912132452_InitialCreate.cs:14-27`), `MigrationsHistoryTable("__EFMigrationsHistory","payment")`
  (`PersistenceExtensions.cs:22`, `CustomWebApplicationFactory.cs:37-38,66-67`), readiness consulta
  `BootstrapChecks` no schema próprio (`PaymentReadinessCheck.cs:21`). Role não é hardcoded no código — mesmo
  padrão de Booking/Catalog; o caminho produtivo documentado no README usa `Username=payment_role` contra
  `postgres-main`, enquanto o teste usa superuser efêmero do container. Bootstrap 2.0 provisiona
  `payment_role` (`002-roles.sql`), schema `payment AUTHORIZATION payment_role` (`003-schemas.sql`) e grants
  (`004-grants.sql:34`).
- (c) Sentinela sem negócio: SÓ `__bootstrap_check`, sem regra de pagamento. `Up` cria
  `payment.__bootstrap_check(Id uuid PK)`; `BootstrapCheck` 1 coluna `Id` (`BootstrapCheck.cs:8`);
  comentário declara descartável e sem semântica (`BootstrapCheck.cs:3-5`); `PaymentDbContext.BootstrapChecks`
  `internal`, consistente com sentinela de fundação. Nenhuma entidade de negócio em `services/payment/src`.
- (d) Sem segredos versionados: OK. `appsettings.json:3-5` tem `"Payment": ""`; `UserSecretsId:
  localizestay-payment-dev` (`Api.csproj:5`); README documenta `user-secrets set` + `ConnectionStrings__Payment`
  para deploy com placeholders `<postgres-main-host>` / `<senha-do-operador>` e `<mesma-string>`, sem valor real;
  único `postgres/postgres` é o placeholder efêmero do container de teste (`CustomWebApplicationFactory.cs:17,29`)
  + mensagem de erro orientando `user-secrets` (`PersistenceExtensions.cs:17`).
- (e) NADA de mensageria (escopo da 6.0): CONFERE — AUSENTE como exigido. Grep por
  `MassTransit|RabbitMQ|Kafka|AddMessaging|IBus|IProducer|IConsumer|Broker|Publish\(|Subscribe|Consume` em
  `services/payment` → 0 matches. `Program.cs:5-10` sem `.AddMessagingConfiguration(...)`; sem
  `MessagingExtensions.cs`; endpoints só `GET /health/live`, `GET /health/ready`, `GET /swagger`
  (`HealthCheckExtensions.cs:23-30`, `MiddlewarePipelineExtensions.cs:14-16`, `SwaggerExtensions.cs:20-29`).
  Não há motivo para REPROVAR por escopo.
- (f) Porta 5103 sem colisão: OK. `"urls": "http://localhost:5103"` (`appsettings.json:2`) vs Catalog `5101`,
  Booking `5102`; README documenta `curl localhost:5103/health/live|ready` + Swagger `http://localhost:5103/swagger`.
- Critérios de sucesso: seletor encontra 4 testes e nenhum caso alheio; `dotnet build` 0W/0E (gate cobre as três
  slns); `/health/live` sem dependência externa (tag `live`); `/health/ready` Healthy com banco e erro sem
  banco (4º teste); Swagger skeleton em `/swagger`; CORS infra pronta com `AllowedOrigins` vazio (sem verificação,
  correto pois CORS é task 7.0); todos os artefatos do gate criados nesta task; nada de Catalog/Booking/mensageria exigido.

## Bloqueantes

Nenhum. Arquivo/linha: —.

## Recomendações (2, não bloqueantes)

1. `scripts/` untracked fora do escopo `services/payment/**` — pertence ao ferramental do flow, não à fatia V-02.
   Mesmo ponto das revisões 3.0/4.0: considere commit separado ou declaração de ownership para não poluir o diff
   das tasks. Não bloqueia.
2. `tests/.../CustomWebApplicationFactory.cs:22-30,53-68` — placeholder via `Environment.SetEnvironmentVariable`
   no ctor + substituição em `ConfigureTestServices` replica o padrão 3.0/4.0 e funciona, mas o duplo caminho de
   connection string continua sutil (placeholder `localhost` nunca usado de fato). Manter o comentário existente;
   opcionalmente consolidar o padrão numa nota única futura. Não bloqueia.

## Imutabilidade

- HEAD antes: `a73987a7f1e4ca2d9940e2f31d36682b486585c6`; HEAD depois: idêntico (confirmado por `git rev-parse HEAD`
  após gate + `dotnet test` + escrita do relatório).
- Nenhum código, status, task ou commit editado pelo validator; working tree inalterado
  (3 modificados + `scripts/`, `services/payment/` untracked, como no início) — só este relatório criado em
  `tasks/prd-fundacao-fase0/5_task_review.md`.
- Comandos e resultados acima: gate exit 0 (`GATE: APROVADO`, 4 testes), `dotnet test` reexecutado exit 0 (4 passed).

## Veredito

**VALIDAÇÃO APROVADA (2 recomendações)**
