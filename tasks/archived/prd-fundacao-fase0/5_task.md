---
status: done
slice_type: vertical
verification_type: behavioral
parallelizable: true
blocked_by: [1.0, 2.0]
---

<task_context>
<domain>services/payment</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database,http_server</dependencies>
<unblocks>"7.0, 8.0, 9.0"</unblocks>
<feedback_checkpoint>`dotnet test --filter "FullyQualifiedName~LocalizeStay.Payment.IntegrationTests.HealthCheckTests"` verde; manualmente, `curl :5103/health/ready` contra `postgres-main` real retorna 200</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="LocalizeStay.Payment.IntegrationTests.HealthCheckTests"</gate_command>
<gate_test_selector>Classe `HealthCheckTests` do projeto `services/payment/tests/LocalizeStay.Payment.IntegrationTests`</gate_test_selector>
<gate_expected_result>Teste(s) da classe `HealthCheckTests` passam (verde); 0 falhas; a migration inicial é aplicada no schema `payment` e `/health/ready` retorna `Healthy` contra o Postgres do Testcontainers</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Payment sobe (`dotnet run`), conecta no Postgres compartilhado com `payment_role`, aplica a migration inicial no schema `payment` e responde `/health/ready` consultando o Postgres real</vertical_slice>
</task_context>

# Tarefa 5.0: Payment sobe com o mesmo padrão de Catalog (V-02)

## Relacionada as User Stories

- N/A — TechSpec Standalone. Cobre, para Payment, a fatia V-02 (`techspec.md`, Mapa de Fatias
  Verticais — "Booking e Payment sobem com o mesmo padrão").

## Visão Geral

Réplica estrutural do padrão provado em 3.0 (Catalog), agora para Payment. É uma fatia própria porque
Payment tem seu próprio schema, role, solution e gate — pode ser verificada isoladamente sem que
Catalog ou Booking existam. Payment não recebe mensageria nesta fundação (só Booking publica a
mensagem de diagnóstico na task 6.0); Payment fica pronto para o primeiro PRD de negócio de pagamento
decidir sua própria estratégia de eventos mais adiante.

## Entrega Observável

- **Entrada ou gatilho:** `dotnet run --project services/payment/src/1-Services/LocalizeStay.Payment.Api`
- **Resultado esperado:** o serviço sobe, `GET /health/live` retorna `200 Healthy` sem dependência
  externa, e `GET /health/ready` retorna `200 Healthy` após consultar o Postgres real via
  `payment_role`.
- **Checkpoint de feedback:** `dotnet test --filter "FullyQualifiedName~HealthCheckTests"` no projeto
  `LocalizeStay.Payment.IntegrationTests` (Testcontainers Postgres) — verde. Manualmente (fora do
  gate automatizado): `curl :5103/health/ready` contra `postgres-main` real retorna `200`.
- **Seletor focalizado:** `LocalizeStay.Payment.IntegrationTests.HealthCheckTests`
- **Fora deste checkpoint:** nenhuma regra de negócio de pagamento; nenhuma mensageria (Payment não
  publica/consome nesta fundação); nenhuma verificação de CORS (task 7.0).

## Requisitos

- Mesmos requisitos estruturais da task 3.0 (Clean Architecture, `Program.cs` enxuto, health checks
  separados por intenção, `dotnet user-secrets`), aplicados ao domínio Payment.
- `PaymentDbContext` conectando ao schema `payment` de `localize_stay`, autenticado como
  `payment_role`.
- Migration inicial sentinela `__bootstrap_check` no schema `payment`.

## Arquivos Envolvidos

- **Criar:**
  - `services/payment/LocalizeStay.Payment.sln`
  - `services/payment/.config/dotnet-tools.json`
  - `services/payment/src/1-Services/LocalizeStay.Payment.Api/Program.cs`
  - `services/payment/src/1-Services/LocalizeStay.Payment.Api/Extensions/CorsExtensions.cs`
  - `services/payment/src/1-Services/LocalizeStay.Payment.Api/Extensions/SwaggerExtensions.cs`
  - `services/payment/src/1-Services/LocalizeStay.Payment.Api/Extensions/PersistenceExtensions.cs`
  - `services/payment/src/1-Services/LocalizeStay.Payment.Api/Extensions/HealthCheckExtensions.cs`
  - `services/payment/src/1-Services/LocalizeStay.Payment.Api/appsettings.json` (sem segredo)
  - `services/payment/src/4-Infra/LocalizeStay.Payment.Infra/Persistence/PaymentDbContext.cs` +
    migration inicial (`__bootstrap_check`)
  - `services/payment/tests/LocalizeStay.Payment.IntegrationTests/HealthCheckTests.cs` (+ `.csproj`)
- **Modificar:**
  - `README.md` (raiz) — adicionar seção "Como rodar Payment localmente"
- **Referência:**
  - `services/catalog/**` (task 3.0) — padrão estrutural a replicar (referência de forma, não
    dependência de compilação)
  - `db/bootstrap/*.sql` (task 2.0) — schema `payment` e `payment_role` já provisionados
  - `Directory.Build.props`, `Directory.Packages.props` (task 1.0)
- **Skills para consultar durante implementação:**
  - `dotnet-architecture`, `dotnet-program-setup`, `dotnet-dependency-config`, `dotnet-observability`,
    `dotnet-testing` — mesmas referências da task 3.0

## Subtarefas

- [ ] 5.1 Criar o esqueleto da solution Clean Architecture (`1-Services`..`4-Infra`) para Payment
- [ ] 5.2 Implementar `Program.cs` e as 4 Extensions (Cors, Swagger, Persistence, HealthCheck)
- [ ] 5.3 Criar `PaymentDbContext` + migration inicial `__bootstrap_check` no schema `payment` via
      `payment_role`
- [ ] 5.4 Configurar `dotnet user-secrets` para a connection string e documentar no README
- [ ] 5.5 Criar `HealthCheckTests` (Testcontainers Postgres) cobrindo migration + `/health/ready`
- [ ] 5.6 Validar manualmente contra `postgres-main` real (`curl :5103/health/ready` → 200) — fora do
      gate automatizado, registrar evidência

## Sequenciamento

- Bloqueado por: 1.0 (convenções de build), 2.0 (database/role/schema `payment` já provisionados)
- Desbloqueia: 7.0 (frontend precisa do `/health` de Payment), 8.0 (contratos exportam o Swagger de
  Payment), 9.0 (OpenMetadata cataloga o schema `payment`)
- Paralelizável: Sim, com 3.0 e 4.0 — nenhum arquivo compartilhado entre os três serviços

## Rastreabilidade

- Esta tarefa cobre: parte da fatia V-02 da TechSpec (o outro serviço, Booking, é a task 4.0).
- Evidência esperada: `HealthCheckTests` verde; migration `__bootstrap_check` aplicada no schema
  `payment`; `curl :5103/health/ready` → 200 contra `postgres-main` real (evidência manual).

## Detalhes de Implementação

Mesmo padrão de `Program.cs` da task 3.0 (ver `techspec.md`, "Program.cs — padrão a replicar nos 3
serviços HTTP"). Payment não adiciona `.AddMessagingConfiguration(...)` nesta fundação.

Endpoints desta task: apenas `GET /health/live`, `GET /health/ready`, `GET /swagger`.

**Convenções da stack:** idênticas às da task 3.0.

## Prontidão para Implementação

- **Decisões fechadas:** nome do schema (`payment`) e da role (`payment_role`) já fixados por 2.0;
  porta do serviço `5103`; tabela sentinela `__bootstrap_check` é descartável.
- **Limites de decisão do implementer:** organização interna de `Extensions/*` além do mínimo
  exigido; nome exato da migration inicial.
- **Dependências disponíveis:** `Directory.Build.props`/`Directory.Packages.props` (1.0), schema
  `payment` + `payment_role` provisionados em `localize_stay` (2.0).
- **Artefatos exigidos pelo gate:** `HealthCheckTests.cs` e o projeto de testes são criados nesta
  própria task; o Testcontainers Postgres é efêmero.
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Payment.IntegrationTests.HealthCheckTests"`
- [ ] O seletor encontra pelo menos um teste e não executa casos sem relação com esta task
- [ ] Build compila sem erros: `dotnet build services/payment/LocalizeStay.Payment.sln`
- [ ] `GET /health/live` responde `200 Healthy` sem depender de Postgres
- [ ] `GET /health/ready` responde `200 Healthy` com Postgres do Testcontainers saudável e um código
      de erro quando indisponível
- [ ] Nenhum warning de compilação
- [ ] Checkpoint de feedback executado: `curl :5103/health/ready` contra `postgres-main` real → `200`
      (evidência manual)
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente esta fatia (Payment) e não depende de Catalog/Booking/mensageria
