---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: true
blocked_by: [1.0, 2.0]
---

<task_context>
<domain>services/booking</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database,http_server</dependencies>
<unblocks>"6.0, 7.0, 8.0, 9.0"</unblocks>
<feedback_checkpoint>`dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.HealthCheckTests"` verde; manualmente, `curl :5102/health/ready` contra `postgres-main` real retorna 200</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="LocalizeStay.Booking.IntegrationTests.HealthCheckTests"</gate_command>
<gate_test_selector>Classe `HealthCheckTests` do projeto `services/booking/tests/LocalizeStay.Booking.IntegrationTests`</gate_test_selector>
<gate_expected_result>Teste(s) da classe `HealthCheckTests` passam (verde); 0 falhas; a migration inicial é aplicada no schema `booking` e `/health/ready` retorna `Healthy` contra o Postgres do Testcontainers</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Booking sobe (`dotnet run`), conecta no Postgres compartilhado com `booking_role`, aplica a migration inicial no schema `booking` e responde `/health/ready` consultando o Postgres real</vertical_slice>
</task_context>

# Tarefa 4.0: Booking sobe com o mesmo padrão de Catalog (V-02)

## Relacionada as User Stories

- N/A — TechSpec Standalone. Cobre, para Booking, a fatia V-02 (`techspec.md`, Mapa de Fatias
  Verticais — "Booking e Payment sobem com o mesmo padrão").

## Visão Geral

Réplica estrutural do padrão provado em 3.0 (Catalog), agora para Booking. É uma fatia própria (não
uma sub-parte de 3.0) porque Booking tem seu próprio schema, role, solution e gate — pode ser
verificada isoladamente sem que Catalog exista. Booking é o serviço que a task 6.0 estende com
mensageria; por isso, embora esta task não implemente `MessagingExtensions.cs` ainda, o esqueleto
criado aqui já é a base sobre a qual 6.0 adiciona `AddMessagingConfiguration(...)`.

## Entrega Observável

- **Entrada ou gatilho:** `dotnet run --project services/booking/src/1-Services/LocalizeStay.Booking.Api`
- **Resultado esperado:** o serviço sobe, `GET /health/live` retorna `200 Healthy` sem dependência
  externa, e `GET /health/ready` retorna `200 Healthy` após consultar o Postgres real via
  `booking_role`.
- **Checkpoint de feedback:** `dotnet test --filter "FullyQualifiedName~HealthCheckTests"` no projeto
  `LocalizeStay.Booking.IntegrationTests` (Testcontainers Postgres) — verde. Manualmente (fora do
  gate automatizado): `curl :5102/health/ready` contra `postgres-main` real retorna `200`.
- **Seletor focalizado:** `LocalizeStay.Booking.IntegrationTests.HealthCheckTests`
- **Fora deste checkpoint:** nenhuma regra de negócio de reserva; nenhuma mensageria ainda (isso é a
  task 6.0, que adiciona `MessagingExtensions.cs` e `POST /internal/diagnostics/ping` sobre este
  esqueleto); nenhuma verificação de CORS (task 7.0).

## Requisitos

- Mesmos requisitos estruturais da task 3.0 (Clean Architecture, `Program.cs` enxuto, health checks
  separados por intenção, `dotnet user-secrets`), aplicados ao domínio Booking.
- `BookingDbContext` conectando ao schema `booking` de `localize_stay`, autenticado como
  `booking_role`.
- Migration inicial sentinela `__bootstrap_check` no schema `booking`.

## Arquivos Envolvidos

- **Criar:**
  - `services/booking/LocalizeStay.Booking.sln`
  - `services/booking/.config/dotnet-tools.json`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Program.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/CorsExtensions.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/SwaggerExtensions.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/PersistenceExtensions.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/HealthCheckExtensions.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/appsettings.json` (sem segredo)
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/BookingDbContext.cs` +
    migration inicial (`__bootstrap_check`)
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/HealthCheckTests.cs` (+ `.csproj`)
- **Modificar:**
  - `README.md` (raiz) — adicionar seção "Como rodar Booking localmente"
- **Referência:**
  - `services/catalog/**` (task 3.0) — padrão estrutural a replicar (não é uma dependência de
    compilação; é só referência de forma)
  - `db/bootstrap/*.sql` (task 2.0) — schema `booking` e `booking_role` já provisionados
  - `Directory.Build.props`, `Directory.Packages.props` (task 1.0)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture`, `dotnet-program-setup`, `dotnet-dependency-config`, `dotnet-observability`,
    `dotnet-testing` — mesmas referências da task 3.0

## Subtarefas

- [ ] 4.1 Criar o esqueleto da solution Clean Architecture (`1-Services`..`4-Infra`) para Booking
- [ ] 4.2 Implementar `Program.cs` e as 4 Extensions (Cors, Swagger, Persistence, HealthCheck)
- [ ] 4.3 Criar `BookingDbContext` + migration inicial `__bootstrap_check` no schema `booking` via
      `booking_role`
- [ ] 4.4 Configurar `dotnet user-secrets` para a connection string e documentar no README
- [ ] 4.5 Criar `HealthCheckTests` (Testcontainers Postgres) cobrindo migration + `/health/ready`
- [ ] 4.6 Validar manualmente contra `postgres-main` real (`curl :5102/health/ready` → 200) — fora do
      gate automatizado, registrar evidência

## Sequenciamento

- Bloqueado por: 1.0 (convenções de build), 2.0 (database/role/schema `booking` já provisionados)
- Desbloqueia: 6.0 (mensageria estende este esqueleto de Booking), 7.0 (frontend precisa do
  `/health` de Booking), 8.0 (contratos exportam o Swagger de Booking), 9.0 (OpenMetadata cataloga o
  schema `booking`)
- Paralelizável: Sim, com 3.0 e 5.0 — nenhum arquivo compartilhado entre os três serviços

## Rastreabilidade

- Esta tarefa cobre: parte da fatia V-02 da TechSpec (o outro serviço, Payment, é a task 5.0).
- Evidência esperada: `HealthCheckTests` verde; migration `__bootstrap_check` aplicada no schema
  `booking`; `curl :5102/health/ready` → 200 contra `postgres-main` real (evidência manual).

## Detalhes de Implementação

Mesmo padrão de `Program.cs` da task 3.0 (ver `techspec.md`, "Program.cs — padrão a replicar nos 3
serviços HTTP"). Booking não adiciona `.AddMessagingConfiguration(...)` ainda — isso é feito na task
6.0, que modifica este `Program.cs`.

Endpoints desta task: apenas `GET /health/live`, `GET /health/ready`, `GET /swagger` — o endpoint
`POST /internal/diagnostics/ping` citado na TechSpec pertence à task 6.0, não a esta.

**Convenções da stack:** idênticas às da task 3.0 (ver "Detalhes de Implementação" de 3.0 para o
snippet completo de `Program.cs` e a convenção de testes com `CustomWebApplicationFactory` +
Testcontainers).

## Prontidão para Implementação

- **Decisões fechadas:** nome do schema (`booking`) e da role (`booking_role`) já fixados por 2.0;
  porta do serviço `5102`; tabela sentinela `__bootstrap_check` é descartável.
- **Limites de decisão do implementer:** organização interna de `Extensions/*` além do mínimo
  exigido; nome exato da migration inicial.
- **Dependências disponíveis:** `Directory.Build.props`/`Directory.Packages.props` (1.0), schema
  `booking` + `booking_role` provisionados em `localize_stay` (2.0).
- **Artefatos exigidos pelo gate:** `HealthCheckTests.cs` e o projeto de testes são criados nesta
  própria task; o Testcontainers Postgres é efêmero.
- **Dependências futuras:** Nenhuma — a task 6.0 modifica `Program.cs`/`Extensions` depois, mas esta
  task já entrega um Booking completo e válido por si só.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.HealthCheckTests"`
- [ ] O seletor encontra pelo menos um teste e não executa casos sem relação com esta task
- [ ] Build compila sem erros: `dotnet build services/booking/LocalizeStay.Booking.sln`
- [ ] `GET /health/live` responde `200 Healthy` sem depender de Postgres
- [ ] `GET /health/ready` responde `200 Healthy` com Postgres do Testcontainers saudável e um código
      de erro quando indisponível
- [ ] Nenhum warning de compilação
- [ ] Checkpoint de feedback executado: `curl :5102/health/ready` contra `postgres-main` real → `200`
      (evidência manual)
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente esta fatia (Booking) e não depende de Catalog/Payment/mensageria
