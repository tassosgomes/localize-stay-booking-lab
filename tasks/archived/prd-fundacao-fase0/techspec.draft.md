# TechSpec: Fundação Técnica da Fase 0

> **Modo de operação:** Standalone (sem PRD de origem — atividade puramente técnica)
> **PRD de origem:** Nenhum. Esta TechSpec traduz diretamente `vision.md`, `docs/brief.md`,
> `context/domain-map.md`, `context/architecture-baseline.md` e `docs/adr/*` em infraestrutura de
> desenvolvimento executável. Objetivo explícito do autor: preparar o terreno técnico (repositório,
> serviços, banco, broker, contratos, catalogação) para que os futuros PRDs de negócio (reserva,
> disponibilidade, pagamento) tenham onde pousar sem precisar decidir de novo a arquitetura.
> **API Contract:** Não aplicável nesta etapa — nenhum endpoint de negócio é criado; os serviços
> nascem apenas com Swagger/health check. Endpoints de negócio serão contratados via
> `tsg-flow-contract-creator` quando o primeiro PRD de domínio existir.
> **Data:** 2026-09-11
> **Status:** Em Revisão
> **Handoff:** draft — não gerar Tasks ainda

---

## Resumo Executivo

Esta TechSpec estabelece o esqueleto técnico dos quatro serviços (Catalog, Booking, Payment,
Notification Worker) e do frontend de teste definidos no baseline arquitetural, conectando-os às
dependências stateful do servidor `infra` (Coolify). As versões/recursos abaixo foram **confirmados
ao vivo no Coolify** (não apenas por documentação), e a forma de reaproveitamento foi decidida com
o autor nesta revisão:

- **PostgreSQL:** `postgres-main` (`postgres:16-alpine`), recurso verdadeiramente compartilhado do
  projeto `Shared Infrastructure`/`production` — usado conforme a convenção já registrada em
  `infra/roteiro.md` ("Regra 4 — PostgreSQL compartilhado por padrão": um database + role por
  projeto dentro da mesma instância).
- **RabbitMQ:** reaproveita o `ecad-dev-rabbitmq` (RabbitMQ 4.3.5-management) já provisionado para
  o projeto `ecad-sba`, em vez de subir um broker dedicado — decisão explícita do autor, aceitando
  o acoplamento operacional de dois laboratórios no mesmo broker físico. Isolamento lógico via
  **vhost dedicado** (`/localize-stay`), não apenas convenção de nome.
- **OpenMetadata:** reaproveita o `ecad-dev-openmetadata` (OpenMetadata 2.0.1, Postgres +
  Elasticsearch 9.3.0 como metastore) já provisionado — mesma decisão de reaproveitamento; os
  ativos do Localize Stay convivem no mesmo catálogo com os do `ecad-sba`, diferenciados por tags de
  ownership/domínio.

Não implementa nenhuma regra de negócio (catálogo, reserva, pagamento): cada serviço nasce com sua
solution Clean Architecture, seu schema/role própria no PostgreSQL, health checks, Swagger e CORS;
a capacidade de mensageria é provada com uma mensagem de diagnóstico, não com os eventos reais da
saga. Contratos (OpenAPI, AsyncAPI, Data Contract) ganham convenção de local e um primeiro artefato
real cada, prontos para os PRDs de negócio estenderem. OpenMetadata passa a catalogar os ativos
recém-criados (schemas, serviços, tópicos).

**Trade-off primário:** entregar "infraestrutura vazia" (sem valor de negócio observável para um
usuário final) em troca de eliminar, dos PRDs de negócio futuros, toda decisão estrutural repetida
(como organizar a solution, como configurar Program.cs, como o serviço fala com Postgres/RabbitMQ
compartilhados, como CORS é habilitado) — cada PRD de negócio poderá focar exclusivamente em regra
de domínio. O custo é que esta entrega, sozinha, não move nenhum critério de conclusão de negócio da
Fase 0; ela é pré-requisito para que os PRDs que os movem possam começar.

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|-------|---------|------------------------|
| `dotnet-architecture` | `.claude/skills/dotnet-architecture` | Clean Architecture por serviço, formato "microsserviços em monorepo" (`examples/microservices.md`) adaptado ao ownership lógico por schema do baseline |
| `dotnet-dependency-config` | `.claude/skills/dotnet-dependency-config` | EF Core + Npgsql, `dotnet user-secrets` para connection strings/credenciais, versionamento central de pacotes, RabbitMQ |
| `dotnet-program-setup` | `.claude/skills/dotnet-program-setup` | `Program.cs` enxuto, `Extensions/` por concern (CORS, Swagger, Health Checks, Persistence, Messaging) |
| `dotnet-observability` | `.claude/skills/dotnet-observability` | Health checks liveness/readiness/startup, logging estruturado com correlationId/causationId |
| `dotnet-testing` | `.claude/skills/dotnet-testing` | Testcontainers (Postgres e RabbitMQ) para testes de integração da própria infraestrutura |
| `react-architecture` | `.claude/skills/react-architecture` | Estrutura "Base" para o frontend de teste (poucas telas, sem `features/*` ainda) |
| `react-runtime-config` | `.claude/skills/react-runtime-config` | Avaliada e **não aplicada integralmente** nesta fase — ver Desvios Identificados |

---

## Arquitetura do Sistema

### Visão Geral dos Componentes

- **`services/catalog`, `services/booking`, `services/payment`** — quatro... três serviços ASP.NET
  Core (Minimal API), cada um com sua solution Clean Architecture própria (`API → Application →
  Domain`, `Infrastructure → Domain`), schema PostgreSQL próprio (`catalog`, `booking`, `payment`)
  e role própria (`catalog_role`, `booking_role`, `payment_role`) com escrita restrita ao próprio
  schema e leitura restrita ao schema `integration`.
- **`services/notification-worker`** — Worker Service .NET (BackgroundService), sem schema próprio
  nesta etapa (permanece stateless, conforme baseline: só ganha schema se precisar reter estado no
  futuro). Consome RabbitMQ.
- **`db/bootstrap`** — scripts SQL idempotentes, executados uma única vez por um operador com
  privilégio suficiente em `postgres-main` (`infra`), que criam o database `localize_stay` (novo,
  dedicado ao projeto, seguindo a convenção "database + role por projeto" já usada nesse Postgres
  compartilhado), os quatro schemas dentro dele (`catalog`, `booking`, `payment`, `integration`) e
  as três roles de serviço com os grants mínimos descritos no baseline. Não são migrations de
  aplicação — são provisionamento de infraestrutura, análogo em espírito ao papel do Coolify para os
  próprios containers do `infra`.
- **`contracts/`** — convenção de local e primeiro artefato real de cada um dos três estilos de
  contrato (OpenAPI exportado, AsyncAPI da topologia de diagnóstico, template de Data Contract),
  prontos para os PRDs de negócio publicarem os contratos reais no mesmo lugar.
- **`frontend/`** — SPA React + Vite + TypeScript ("Base", conforme ADR-003), cliente fino que
  consome diretamente as APIs de Catalog/Booking/Payment via CORS, sem gateway/BFF.
- **OpenMetadata** (`ecad-dev-openmetadata`, reaproveitado) — passa a catalogar os schemas do
  database `localize_stay`, os três serviços HTTP e a topologia RabbitMQ do vhost `/localize-stay`
  criados por esta TechSpec, com tags de ownership que os distinguem dos ativos do `ecad-sba` no
  mesmo catálogo.

### Diagrama de Componentes

```text
                         ┌──────────────────────────┐
                         │   frontend (React/Vite)  │
                         └─────────────┬────────────┘
                     CORS, HTTP direto (sem gateway)
              ┌──────────────┬─────────┴────────┬──────────────┐
              ▼              ▼                  ▼              │
      ┌──────────────┐┌──────────────┐  ┌──────────────┐       │
      │ Catalog.Api  ││ Booking.Api  │  │ Payment.Api  │       │
      └──────┬───────┘└──────┬───────┘  └──────┬───────┘       │
             │ catalog_role  │ booking_role     │ payment_role  │
             ▼               ▼                  ▼               
      ┌─────────────────────────────────────────────────┐
      │   postgres-main (infra) — database localize_stay │
      │  schemas: catalog | booking | payment | integration
      └─────────────────────────────────────────────────┘
                               ▲
                 publica/consome mensagem de diagnóstico
                               │
      ┌──────────────┐  ecad-dev-rabbitmq (infra)  ┌───────────────────────┐
      │ Booking.Api  │  vhost /localize-stay  ────▶ │ Notification.Worker   │
      └──────────────┘  (broker compartilhado       └───────────────────────┘
                          com o projeto ecad-sba,
                          isolado por vhost)

      ecad-dev-openmetadata (infra, catálogo compartilhado com ecad-sba,
      diferenciado por tags de ownership) ingere: schemas do database
      localize_stay, specs OpenAPI dos 3 serviços, topologia RabbitMQ do
      vhost /localize-stay (registrada manualmente via API como serviço
      CustomMessaging — ver Design de Implementação).
```

---

## Estratégia de Entrega Incremental

Cada fatia abaixo entrega uma capacidade técnica observável e verificável (compila, roda, prova a
conectividade real com uma dependência do `infra`), mesmo sem comportamento de negócio — o
"comportamento observável" aqui é infraestrutura funcionando ponta a ponta, não uma jornada de
usuário. Nenhuma fatia implementa regra de Catalog/Booking/Payment/Notification; isso é escopo dos
PRDs de negócio que virão depois, referenciando esta fundação.

### Mapa de Fatias Verticais

| Slice | Comportamento observável | Requisitos cobertos | Entrada → processamento → saída | Artefatos principais | Evidência / checkpoint | Bloqueado por |
|-------|--------------------------|----------------------|----------------------------------|-----------------------|-------------------------|----------------|
| V-01 | Serviço Catalog sobe, conecta no Postgres compartilhado com role própria e responde health check real | Baseline §Ownership de Dados, §Comunicação, §Observabilidade | `dotnet run` → `Program.cs` chama `Extensions/*` → EF Core aplica migration inicial no schema `catalog` com `catalog_role` → `GET /health` consulta o Postgres real | Solution `LocalizeStay.Catalog.*`, `Extensions/{Cors,Swagger,Persistence,HealthCheck}Extensions.cs`, migration inicial, `appsettings.json` (sem segredo) | `dotnet build` sem warning; `curl :5101/health` → `200 Healthy` contra a instância real do `infra`; teste de integração com Testcontainers Postgres cobrindo a mesma migration | EN-01 |
| V-02 | Booking e Payment sobem com o mesmo padrão, cada um com seu schema/role dentro do database `localize_stay`; schema `integration` existe com leitura concedida às três roles | Baseline §Ownership de Dados (schema por domínio + schema de integração) | Réplica do fluxo de V-01 para Booking/Payment; `db/bootstrap` cria o database `localize_stay` em `postgres-main`, os schemas `booking`, `payment`, `integration` e os grants cruzados de leitura | Solutions `LocalizeStay.Booking.*`, `LocalizeStay.Payment.*`; `db/bootstrap/{001-database,002-schemas,003-grants}.sql` | `curl :5102/health` e `:5103/health` → `200 Healthy`; teste manual `SELECT` do schema `integration` com `catalog_role` falha (sem escrita) e com leitura funciona | V-01 |
| V-03 | Booking publica uma mensagem de diagnóstico no vhost `/localize-stay` do RabbitMQ compartilhado (`ecad-dev-rabbitmq`); Notification Worker consome, faz ACK e loga com correlationId | Baseline §Comunicação Assíncrona, ADR-002 | Booking expõe endpoint interno de diagnóstico (`POST /internal/diagnostics/ping`) que publica `DiagnosticPing` numa exchange `diagnostics.topic` do vhost dedicado; Worker consome fila `notification.diagnostics` no mesmo vhost, loga `correlationId`/`causationId` e faz ACK | `MessagingExtensions.cs` em Booking e em Notification Worker, `LocalizeStay.Notification.Worker` (host), RabbitMQ health check | Log estruturado do Worker mostra o evento consumido com os mesmos IDs publicados por Booking; UI de management do RabbitMQ mostra o vhost `/localize-stay` isolado dos recursos do `ecad-sba`; teste de integração com Testcontainers RabbitMQ cobre publish→consume→ack | V-02 |
| V-04 | Frontend local exibe o status de Catalog/Booking/Payment lendo `/health` de cada um via CORS, sem gateway | ADR-003 | Vite dev server → `fetch` direto às três URLs configuradas → renderiza status | `frontend/` (Vite+React+TS, estrutura Base), `CorsExtensions.cs` habilitado nos 3 serviços para a origem do Vite | `npm run dev` + abrir no navegador mostra os 3 status "Healthy" sem erro de CORS no console | V-02 |
| V-05 | Cada serviço expõe seu OpenAPI real; a topologia de diagnóstico do RabbitMQ está documentada em AsyncAPI; o Data Contract dos datasets futuros tem template pronto | Baseline §Padrões de Comunicação, Vision Fase 0 DoD | `contracts/openapi/{catalog,booking,payment}.json` exportados do Swagger de cada serviço; `contracts/asyncapi/diagnostics-v1.yaml` descreve `DiagnosticPing`; `contracts/data-contracts/TEMPLATE.md` pronto para `available_accommodations_v1`/`reservation_calendar_v1` | Scripts/comandos de export, arquivos versionados | `swagger-cli validate` (ou equivalente) nos 3 JSON; `asyncapi validate` no YAML | V-01, V-02, V-03 |
| V-06 | `ecad-dev-openmetadata` mostra os schemas do database `localize_stay`, os três serviços (via OpenAPI) e a topologia do vhost `/localize-stay` como ativos catalogados, com ownership que os distingue dos ativos do `ecad-sba` | Vision Fase 0 DoD, Baseline §Observabilidade (catalogação) | Ingestion connector Postgres aponta para `postgres-main`, filtrado ao database `localize_stay`; specs OpenAPI de V-05 registradas como serviços de API; vhost `/localize-stay` registrado manualmente via API do OpenMetadata como serviço `CustomMessaging` (RabbitMQ não tem conector nativo — ver Design de Implementação) | Config de ingestion (YAML/UI do OpenMetadata), script de registro via API para o `CustomMessaging`, tags de ownership por domínio (`localize-stay`) | UI do OpenMetadata lista os 4 schemas, os 3 serviços de API e a fila/exchange de diagnóstico, cada um com owner e tag `localize-stay`, visualmente separados dos ativos do `ecad-sba` | V-02, V-03, V-05 |

### Habilitadores inevitáveis

| Habilitador | Por que não pode fazer parte de uma fatia | Menor escopo | Fatias desbloqueadas |
|-------------|--------------------------------------------|--------------|----------------------|
| EN-01 — Convenções de solução compartilhadas | `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` e `.config/dotnet-tools.json` precisam existir antes do primeiro `dotnet build`; se cada serviço definir os seus, V-02/V-03 divergem em versão de pacote e regras de compilador, e o "mesmo padrão" deixa de ser verdade | 4 arquivos na raiz do repo, sem nenhuma lógica | V-01 |

---

## Design de Implementação

### Estrutura de repositório (monorepo, uma pasta por serviço)

Decisão adotada por ser o padrão já usado no repositório atual (docs/context na raiz de um único
repo Git, sem múltiplos remotos) — variação de "microsserviços" descrita em
`dotnet-architecture/examples/microservices.md`, que já prevê explicitamente "uma pasta por serviço
em monorepo" como alternativa a um repositório por serviço.

```text
localize-stay-booking-lab/
├── Directory.Build.props            # Nullable, ImplicitUsings, TreatWarningsAsErrors, LangVersion
├── Directory.Packages.props         # versões centrais (EF Core, Npgsql, Swashbuckle, xUnit, ...)
├── .editorconfig
├── services/
│   ├── catalog/
│   │   ├── LocalizeStay.Catalog.sln
│   │   ├── .config/dotnet-tools.json         # dotnet-ef fixado na major do EFCore.Design
│   │   ├── src/
│   │   │   ├── 1-Services/LocalizeStay.Catalog.Api/
│   │   │   │   ├── Program.cs                # ~20-40 linhas, só chamadas de Extensions
│   │   │   │   └── Extensions/
│   │   │   │       ├── CorsExtensions.cs
│   │   │   │       ├── SwaggerExtensions.cs
│   │   │   │       ├── PersistenceExtensions.cs
│   │   │   │       └── HealthCheckExtensions.cs
│   │   │   ├── 2-Application/LocalizeStay.Catalog.Application/   # vazio nesta etapa (sem caso de uso ainda)
│   │   │   ├── 3-Domain/LocalizeStay.Catalog.Domain/             # vazio nesta etapa
│   │   │   └── 4-Infra/LocalizeStay.Catalog.Infra/
│   │   │       └── Persistence/CatalogDbContext.cs               # schema "catalog", migration inicial
│   │   └── tests/
│   │       └── LocalizeStay.Catalog.IntegrationTests/            # Testcontainers Postgres
│   ├── booking/    # mesma forma, LocalizeStay.Booking.*, + Extensions/MessagingExtensions.cs (V-03)
│   ├── payment/    # mesma forma, LocalizeStay.Payment.*
│   └── notification-worker/
│       └── LocalizeStay.Notification.sln
│           └── src/1-Services/LocalizeStay.Notification.Worker/  # BackgroundService, sem Domain/Application ainda
├── frontend/
│   └── localize-stay-frontend/      # Vite + React + TS, estrutura "Base"
├── db/
│   └── bootstrap/
│       ├── 001-database.sql          # cria o database "localize_stay" em postgres-main
│       ├── 002-roles.sql
│       ├── 003-schemas.sql
│       └── 004-grants.sql
├── scripts/
│   └── openmetadata/
│       └── register-rabbitmq.*       # registra o vhost /localize-stay como CustomMessaging (V-06)
└── contracts/
    ├── openapi/
    ├── asyncapi/
    └── data-contracts/
```

**Desvio deliberado do exemplo `microservices.md`:** o exemplo padrão da skill assume "banco por
serviço" e contrato compartilhado como pacote NuGet (`ProjectName.Contracts`). Nenhum dos dois se
aplica aqui — ver Conformidade com Skills / Desvios Identificados.

**Minimalismo do Notification Worker:** sem camadas Domain/Application nesta etapa, porque não há
nenhuma regra de negócio a isolar ainda (o worker só teria interfaces vazias) — evita design
antecipado para um requisito hipotético. Quando o primeiro PRD de notificação definir uma regra real
(ex.: formatar mensagem por canal), as camadas nascem junto com essa regra, não antes.

### Program.cs — padrão a replicar nos 3 serviços HTTP

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCorsConfiguration(builder.Configuration)
    .AddSwaggerConfiguration()
    .AddPersistenceConfiguration(builder.Configuration)
    .AddHealthCheckConfiguration(builder.Configuration);
// Booking e Payment adicionam .AddMessagingConfiguration(builder.Configuration) a partir de V-03

var app = builder.Build();
app.UseApplicationPipeline(builder.Environment);
app.Run();
```

### Modelos de Dados

Nenhuma entidade de domínio é criada nesta etapa (não há regra de negócio). Cada serviço ganha
apenas uma migration inicial "sentinela" para provar que a role consegue criar/ler no próprio schema:

| Serviço | Schema | Tabela sentinela | Propósito |
|---|---|---|---|
| Catalog | `catalog` | `__bootstrap_check` (1 coluna, sem semântica de negócio) | Provar migration + role funcionando |
| Booking | `booking` | `__bootstrap_check` | idem |
| Payment | `payment` | `__bootstrap_check` | idem |

A tabela sentinela é descartável: o primeiro PRD de negócio de cada domínio a substitui pela
primeira migration real, sem precisar manter compatibilidade com ela.

### Endpoints de API

Nenhum endpoint de negócio. Cada um dos três serviços HTTP expõe apenas:

- `GET /health/live` — liveness, não depende de dependência externa.
- `GET /health/ready` — readiness, consulta o Postgres real via `HealthCheckExtensions`.
- `GET /swagger` — documento OpenAPI vazio/skeleton (nenhum endpoint de negócio ainda, mas a
  infraestrutura de geração já existe para os PRDs seguintes apenas adicionarem endpoints).
- Booking adiciona `POST /internal/diagnostics/ping` (V-03) — endpoint técnico de diagnóstico, não
  um endpoint de negócio; deve ser removido ou isolado sob um flag quando o primeiro PRD de Booking
  chegar, para não vazar para o contrato público.

### Bootstrap de banco (`db/bootstrap`)

Executado manualmente uma única vez por um operador com privilégio suficiente em `postgres-main`
(`infra`) — não faz parte do boot de nenhum serviço (`dotnet-dependency-config` já proíbe aplicar
migration automaticamente em produção; aqui vai um passo além: nem a criação de database/role/schema
é responsabilidade de uma migration de serviço, é provisionamento fora do ciclo de deploy de
aplicação):

1. `001-database.sql` — cria o database `localize_stay` em `postgres-main`, seguindo a convenção já
   em uso nesse Postgres compartilhado ("database + role por projeto", `infra/roteiro.md`).
2. `002-roles.sql` — cria `catalog_role`, `booking_role`, `payment_role` (login, senha via variável
   de ambiente do `psql`, nunca hardcoded no script versionado).
3. `003-schemas.sql` — dentro de `localize_stay`, cria `catalog`, `booking`, `payment`,
   `integration`, com owner = role do próprio domínio (schema `integration` não tem role de escrita
   própria: nenhum consumidor escreve nele nesta fase).
4. `004-grants.sql` — concede `USAGE, CREATE` no schema próprio para cada role; concede `USAGE,
   SELECT` no schema `integration` para as três roles de serviço; revoga qualquer acesso cruzado
   entre schemas de domínio.

As migrations EF Core de cada serviço rodam depois, conectando diretamente no database
`localize_stay` e autenticadas com a role do próprio domínio, e só têm permissão de atuar dentro do
seu schema — reforço automático da regra de ownership no nível de banco, não apenas de convenção de
código. Por ser um database próprio (não apenas um schema num database compartilhado por múltiplos
projetos), nenhuma tabela/schema de outro projeto que eventualmente use `postgres-main` fica visível
para as roles do Localize Stay.

### RabbitMQ — capacidade de mensageria (V-03)

O broker (`ecad-dev-rabbitmq`) é compartilhado com o projeto `ecad-sba` — decisão explícita do autor
de reaproveitar em vez de provisionar um broker dedicado. Para não acoplar as duas topologias no
mesmo namespace lógico, o Localize Stay usa um **vhost dedicado** (`/localize-stay`), criado uma vez
via UI/API de management do RabbitMQ, análogo em espírito ao `db/bootstrap` do Postgres — outro
passo de provisionamento manual, fora do boot de qualquer serviço:

- Biblioteca: `Rmq.CloudEvents` (baseline de `dotnet-dependency-config`), com ACK em sucesso e NACK
  sem requeue após falha final; retry com backoff e DLQ conforme
  `dotnet-dependency-config/examples/messaging-rabbitmq.md`.
- Toda connection string de aplicação já inclui o vhost `/localize-stay` — nenhum código do
  Localize Stay declara ou enumera exchanges/filas de outro vhost.
- Convenção de nomes provada aqui com `diagnostics.topic` (exchange) e `notification.diagnostics`
  (fila), dentro do vhost — os PRDs de negócio devem seguir o mesmo padrão `<domínio>.<propósito>`
  para as exchanges reais da saga (`PaymentRequested`, `PaymentAuthorized`, etc., já nomeadas no
  ADR-002), sempre dentro de `/localize-stay`.
- Toda mensagem publicada carrega `correlationId`/`causationId`, mesmo sendo uma mensagem de
  diagnóstico sem valor de negócio — a infraestrutura de correlação precisa existir antes do
  primeiro evento real, não pode ser adicionada depois como retrofit.
- Health check de RabbitMQ (`AddRabbitMQ` do pacote de health checks) exposto em `/health/ready` de
  Booking e do Worker, apontando para o vhost `/localize-stay`.

### Registro do RabbitMQ no OpenMetadata (V-06)

OpenMetadata 2.0.x não tem conector de ingestão nativo para RabbitMQ (a lista de conectores de
messaging suportados é Kafka, Kinesis, Pub/Sub e Redpanda). O schema de serviços de mensageria do
OpenMetadata inclui, porém, um tipo genérico `CustomMessaging`, e a API REST
(`/v1/services/messagingServices` + `/v1/topics`) permite registrar manualmente um serviço e seus
tópicos sem um ingestion connector automático. V-06 registra o vhost `/localize-stay` como um
serviço `CustomMessaging` chamado `localize-stay-rabbitmq`, com um tópico por exchange/fila real
(`diagnostics.topic`, `notification.diagnostics`), via um script simples contra essa API — não uma
pipeline de ingestion agendada, já que não há um connector que a automatize.

### Frontend (V-04)

- Estrutura "Base" (`react-architecture`): `src/components`, `src/hooks`, `src/services`,
  `src/utils` no nível de `src/` — ainda não há `features/*` porque não há tela de negócio.
- `src/services/healthService.ts` concentra as três chamadas `fetch` (Catalog/Booking/Payment).
- URLs de backend via `.env.development` (`VITE_CATALOG_URL`, `VITE_BOOKING_URL`,
  `VITE_PAYMENT_URL`), lidas por um módulo `src/config/env.ts` tipado — ver Desvios Identificados
  sobre por que não é o padrão completo de `react-runtime-config` nesta etapa.

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Fatia | Tipo | Skills Aplicáveis | Descrição |
|---------|-------|------|-------------------|-----------|
| `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` | EN-01 | Config | `dotnet-dependency-config` | Convenções e versões centrais compartilhadas pelos 4 serviços |
| `services/catalog/LocalizeStay.Catalog.sln` + projetos `1-Services`..`4-Infra` | V-01 | Solution/Config | `dotnet-architecture`, `dotnet-program-setup` | Esqueleto Clean Architecture do Catalog |
| `services/catalog/.../Extensions/{Cors,Swagger,Persistence,HealthCheck}Extensions.cs` | V-01 | Config | `dotnet-program-setup` | Bootstrap por concern, `Program.cs` só orquestra |
| `services/catalog/.../CatalogDbContext.cs` + migration inicial | V-01 | Migration | `dotnet-dependency-config` | Conexão ao schema `catalog` via `catalog_role` |
| `services/catalog/tests/LocalizeStay.Catalog.IntegrationTests` | V-01 | Test | `dotnet-testing` | Testcontainers Postgres cobrindo migration + health check |
| `services/booking/**`, `services/payment/**` (mesma forma do Catalog) | V-02 | Solution/Config/Test | `dotnet-architecture`, `dotnet-program-setup`, `dotnet-testing` | Réplica do padrão de V-01 |
| `db/bootstrap/{001-database,002-roles,003-schemas,004-grants}.sql` | V-02 | Config | `dotnet-dependency-config` | Cria o database `localize_stay` em `postgres-main` e provisiona roles/schemas dentro dele |
| `services/booking/.../Extensions/MessagingExtensions.cs` | V-03 | Config | `dotnet-dependency-config` | Conexão ao vhost `/localize-stay` do RabbitMQ compartilhado, publisher de diagnóstico |
| `services/booking/.../DiagnosticsController.cs` (ou Minimal API equivalente) | V-03 | Controller | `dotnet-architecture` | `POST /internal/diagnostics/ping` |
| `services/notification-worker/**` | V-03 | Solution/Config | `dotnet-architecture`, `dotnet-program-setup` | Worker Service consumindo `notification.diagnostics` |
| `services/*/tests/*.IntegrationTests` (mensageria) | V-03 | Test | `dotnet-testing` | Testcontainers RabbitMQ, publish→consume→ack |
| Criação do vhost `/localize-stay` (via UI/API de management do `ecad-dev-rabbitmq`) | V-03 | Config | — | Provisionamento manual, isola a topologia do Localize Stay dentro do broker compartilhado com `ecad-sba` |
| `frontend/localize-stay-frontend/**` | V-04 | App | `react-architecture` | SPA Vite+React+TS, estrutura Base |
| `frontend/localize-stay-frontend/src/services/healthService.ts`, `src/config/env.ts` | V-04 | Service/Config | `react-architecture` | Chamadas de health check via CORS |
| `services/*/Extensions/CorsExtensions.cs` (atualização para incluir origem do frontend) | V-04 | Config | `dotnet-program-setup` | Habilita CORS explícito para o Vite dev server |
| `contracts/openapi/{catalog,booking,payment}.json` | V-05 | Contrato | `dotnet-program-setup` | Export do Swagger de cada serviço |
| `contracts/asyncapi/diagnostics-v1.yaml` | V-05 | Contrato | — | Primeiro AsyncAPI real, documenta a topologia de V-03 |
| `contracts/data-contracts/TEMPLATE.md` | V-05 | Contrato | — | Template para `available_accommodations_v1`/`reservation_calendar_v1` futuros |
| Config de ingestion do OpenMetadata (Postgres + API services) | V-06 | Config | — | Executado no próprio `ecad-dev-openmetadata` (UI ou YAML de ingestion), não no repo de aplicação |
| `scripts/openmetadata/register-rabbitmq.*` (script contra a API do OpenMetadata) | V-06 | Script | — | Registra o vhost `/localize-stay` como serviço `CustomMessaging` e seus tópicos, já que não há conector nativo de RabbitMQ |
| `README.md` (raiz, expandido) | V-01..V-06 | Docs | — | Como rodar cada serviço localmente e apontar para as dependências do `infra` |

### Arquivos a Modificar

| Caminho | Fatia | Skills Aplicáveis | Alteração |
|---------|-------|-------------------|-----------|
| `README.md` | V-01, V-04 | — | Adicionar seção "Como rodar localmente" e "Como configurar `user-secrets`" |
| `docs/adr/index.md` | — | — | Nenhuma ADR nova nesta TechSpec (ver seção ADRs) |

### Arquivos de Referência (não alterar)

| Caminho | Motivo da Consulta |
|---------|-------------------|
| `context/architecture-baseline.md` | Regras de ownership de dados, comunicação e observabilidade que todo artefato acima precisa respeitar |
| `docs/adr/adr-001-backend-stack-dotnet.md`, `adr-002-broker-fase0-rabbitmq.md`, `adr-003-frontend-teste-react.md` | Decisões de stack já aceitas, não reabertas aqui |
| `/home/tsgomes/github-tassosgomes/infra/AGENTS.md` | Convenções e limites de segurança para operar contra o Postgres/RabbitMQ/OpenMetadata compartilhados do `infra` (fora deste repositório) |

---

## Pontos de Integração

- **`postgres-main` (`infra`, via Coolify, `postgres:16-alpine`, confirmado ao vivo):** conexão de
  rede (não localhost) ao database `localize_stay`; credenciais via `dotnet user-secrets` por
  desenvolvedor, nunca versionadas; sem retry/circuit breaker elaborado nesta fase (não é o foco —
  apenas timeout de conexão razoável no Npgsql).
- **`ecad-dev-rabbitmq` (`infra`, via Coolify, RabbitMQ 4.3.5-management, reaproveitado do
  `ecad-sba`):** conexão de rede ao vhost `/localize-stay`; retry com backoff e DLQ desde o primeiro
  publisher (V-03), pois é convenção que os PRDs de negócio herdam, não um retrofit. Por ser um
  broker compartilhado com outro projeto, nenhuma operação de administração do broker (usuários,
  policies globais, outros vhosts) é feita por este repositório — apenas o vhost próprio.
- **`ecad-dev-openmetadata` (`infra`, via Coolify, OpenMetadata 2.0.1, reaproveitado do `ecad-sba`):**
  ingestion connector consultando `postgres-main` (filtrado ao database `localize_stay`) e as specs
  OpenAPI/AsyncAPI publicadas em `contracts/`; RabbitMQ registrado manualmente como `CustomMessaging`
  (sem conector nativo). Sem autenticação/autorização aplicável nesta fase (baseline: Fase 0 não
  implementa auth); tags de ownership por domínio evitam confundir os ativos do Localize Stay com os
  do `ecad-sba` no mesmo catálogo.

---

## Análise de Impacto

| Componente Afetado | Tipo de Impacto | Descrição & Risco | Ação Requerida |
|--------------------|-----------------|-------------------|-----------------|
| `postgres-main` (`infra`) | Modificado (novo database `localize_stay` + roles) | Risco baixo: database próprio isola completamente das tabelas de outros projetos nesse Postgres; um script mal escrito em `db/bootstrap` afeta no máximo o próprio database | Executar os scripts manualmente, com revisão humana, nunca automatizado num pipeline sem confirmação; validar que não há database/role com nome colidente antes de criar |
| `ecad-dev-rabbitmq` (`infra`, compartilhado com `ecad-sba`) | Modificado (novo vhost `/localize-stay`) | Risco baixo-médio: broker compartilhado com outro projeto ativo; o vhost isola exchanges/filas, mas ambos os projetos competem pelos mesmos limites de recursos do container (CPU/memória/conexões) | Criar apenas o vhost próprio, nunca alterar policies globais/usuários administrativos do broker; monitorar se o uso do Localize Stay não degrada o `ecad-sba` |
| `ecad-dev-openmetadata` (`infra`, compartilhado com `ecad-sba`) | Modificado (novos ativos catalogados) | Risco baixo: apenas ingestion/registro via API, não escreve nos sistemas de origem; risco de poluir visualmente o catálogo do `ecad-sba` se as tags de ownership não forem aplicadas | Confirmar credenciais de ingestion com escopo de leitura; aplicar tag/owner `localize-stay` em todo ativo criado por esta TechSpec |
| Outros domínios do roadmap (Vision) | Nenhum | Fase 2 (Busca) e além ainda não têm serviço; nada a impactar agora | — |

---

## Abordagem de Testes

### Testes Unitários

Não aplicável nesta etapa — não há regra de domínio a testar (Domain/Application ainda vazios em
todos os serviços).

### Testes de Integração

- Um teste de integração por serviço HTTP (V-01/V-02), usando `WebApplicationFactory` +
  Testcontainers PostgreSQL, cobrindo: aplicação da migration inicial no schema correto, health
  check retornando `Healthy` contra o Postgres do container.
- Um teste de integração de mensageria (V-03), usando Testcontainers RabbitMQ, cobrindo: publish do
  `DiagnosticPing` por Booking, consumo e ACK pelo Worker, presença de `correlationId`/`causationId`
  no log estruturado.
- Testcontainers, não a instância real do `infra`, para não acoplar a suíte automatizada a um
  recurso compartilhado e stateful fora do controle do CI/dev local.

### Testes de Contrato

- `contracts/openapi/*.json` validados com um linter de OpenAPI (ex.: `swagger-cli validate` ou
  equivalente já usado pelo autor) para garantir que o export de cada serviço é um documento válido.
- `contracts/asyncapi/diagnostics-v1.yaml` validado com `asyncapi validate` (ou ferramenta
  equivalente), estabelecendo o padrão que os PRDs de negócio devem seguir para os eventos reais.

### Verificação manual (contra o `infra` real)

Cada checkpoint da tabela de fatias que menciona "contra a instância real" é uma verificação manual
do desenvolvedor (não um teste automatizado), rodada uma vez por fatia para confirmar que a
infraestrutura compartilhada de fato funciona — os testes automatizados usam Testcontainers.

---

## Sequenciamento de Desenvolvimento

### Build Order

1. EN-01 (convenções de solução) — sem dependências.
2. V-01 (Catalog) — depende de EN-01 e de `db/bootstrap/001-database.sql` + `002-roles.sql` + parte
   de `003-schemas.sql` referente a `catalog` já executados manualmente contra `postgres-main`.
3. V-02 (Booking, Payment, schema `integration` + grants) — depende de V-01 (reaplica o mesmo
   padrão) e do restante de `db/bootstrap`.
4. V-03 (mensageria + Notification Worker) — depende de V-02 (Booking precisa existir para publicar)
   e da criação prévia do vhost `/localize-stay` em `ecad-dev-rabbitmq`.
5. V-04 (frontend) — depende de V-02 (os três `/health` precisam existir e ter CORS habilitado).
6. V-05 (contratos exportados) — depende de V-01, V-02 e V-03 (precisa que Swagger e a topologia
   RabbitMQ já existam para documentá-los).
7. V-06 (OpenMetadata) — depende de V-02, V-03 e V-05 (ingere schemas, serviços e specs já
   publicadas, e registra o `CustomMessaging` do vhost `/localize-stay`).

### Dependências Técnicas Bloqueantes

- Todo o desenvolvimento (os 3 serviços HTTP, o Worker, o frontend e a execução dos Testcontainers)
  roda no **ThinkPad** — confirmado com o autor, não há necessidade de usar o `desenv` para esta
  fundação. O ThinkPad só precisa de acesso de rede a `postgres-main`, `ecad-dev-rabbitmq` e
  `ecad-dev-openmetadata` no `infra` (ver `infra/AGENTS.md`) e de Docker local para os
  Testcontainers.
- Um operador com privilégio suficiente em `postgres-main` para rodar `db/bootstrap` antes de
  V-01/V-02, e permissão de management no `ecad-dev-rabbitmq` para criar o vhost `/localize-stay`
  antes de V-03 (o autor, como dono do homelab, já tem ambos os acessos — confirmado via Coolify
  nesta revisão).
- Credenciais de leitura para o ingestion connector do `ecad-dev-openmetadata` e um Personal Access
  Token com escopo de escrita para o script de registro do `CustomMessaging` (V-06).

---

## Monitoramento e Observabilidade

- Logging estruturado básico em cada serviço (baseline: nada além disso nesta fase — sem
  OpenTelemetry/tracing/métricas formais, reservados à Fase 1).
- Todo log relacionado a mensageria inclui `correlationId`/`causationId`, mesmo para a mensagem de
  diagnóstico — estabelece o hábito antes do primeiro evento de negócio existir.
- Health checks separados por intenção (`/health/live`, `/health/ready`) nos 3 serviços HTTP e no
  Worker (o Worker expõe `/health/ready` mínimo via um host HTTP leve, só para o probe — não é uma
  API pública).

---

## Considerações Técnicas

### Decisões Principais

- **Decisão:** Monorepo, uma pasta por serviço em `services/`, em vez de um repositório Git por
  serviço.
  **Racional:** é a forma como o repositório já existe hoje (docs/context na raiz de um único
  repo); `dotnet-architecture/examples/microservices.md` já prevê essa variação explicitamente.
  **Trade-offs:** builds/CI de um serviço podem precisar ignorar mudanças nos outros; aceitável na
  escala de laboratório da Fase 0.
  **Alternativas rejeitadas:** um repositório por serviço — overhead de gestão desproporcional ao
  estágio do projeto (autor único, sem múltiplas equipes).

- **Decisão:** Bootstrap de roles/schemas do Postgres como scripts SQL manuais em `db/bootstrap`,
  fora do ciclo de migration de qualquer serviço.
  **Racional:** criar role/schema normalmente exige privilégio maior que o de aplicação; misturar
  isso com a migration de um serviço específico romperia a regra "nenhum serviço aplica migration
  em schema alheio" logo na primeira migration.
  **Trade-offs:** um passo manual a mais antes do primeiro `dotnet run`; documentado no README.
  **Alternativas rejeitadas:** cada serviço criar seu próprio schema/role via migration — rejeitada
  por exigir credencial de superusuário dentro do código de aplicação, contrariando o princípio de
  menor privilégio já registrado no baseline.

- **Decisão:** Reaproveitar o RabbitMQ (`ecad-dev-rabbitmq`) e o OpenMetadata (`ecad-dev-openmetadata`)
  já provisionados para o projeto `ecad-sba`, em vez de subir instâncias dedicadas ao Localize Stay
  — confirmado ao vivo no Coolify que só `postgres-main` é genuinamente compartilhado por convenção;
  RabbitMQ e OpenMetadata estavam rotulados/escopados para `ecad-sba`. Decisão explícita do autor.
  **Racional:** evita o custo operacional de mais um RabbitMQ e, principalmente, mais um
  OpenMetadata (Postgres + Elasticsearch + Airflow) no homelab só para o Localize Stay; um catálogo
  de metadados, por natureza, é feito para cobrir múltiplas fontes/projetos — reaproveitar é o uso
  pretendido da ferramenta, não um atalho.
  **Trade-offs:** os dois projetos-laboratório passam a competir pelos mesmos recursos de container
  (RabbitMQ) e pelo mesmo catálogo visual (OpenMetadata); uma falha operacional no `ecad-sba` que
  afete o broker/catálogo compartilhado também afeta o Localize Stay, e vice-versa.
  **Alternativas rejeitadas:** provisionar RabbitMQ e/ou OpenMetadata dedicados — rejeitada pelo
  autor nesta revisão por elevar o custo operacional do homelab sem um problema real de contenção
  ainda observado entre os dois projetos.

- **Decisão:** Isolar a topologia do Localize Stay dentro do RabbitMQ compartilhado com um **vhost
  dedicado** (`/localize-stay`), em vez de apenas um prefixo de nome nas exchanges/filas.
  **Racional:** vhost é o mecanismo nativo de multi-tenancy do RabbitMQ (permissões, listagem e
  isolamento lógico por vhost) — mais robusto que confiar em convenção de nome para não colidir com
  a topologia do `ecad-sba` no mesmo broker.
  **Trade-offs:** mais um recurso a provisionar manualmente (como o `db/bootstrap` do Postgres) antes
  de V-03; sem custo real além disso.
  **Alternativas rejeitadas:** prefixar nomes de exchange/fila (`localize-stay.diagnostics.topic`) no
  vhost `/` compartilhado — rejeitada por depender de disciplina de nomenclatura para evitar colisão,
  em vez de uma garantia estrutural do broker.

- **Decisão:** Capacidade de mensageria provada com uma mensagem de diagnóstico
  (`DiagnosticPing`/`diagnostics.topic`), não com os eventos reais da saga (`PaymentRequested` etc.,
  já nomeados no ADR-002).
  **Racional:** implementar os eventos reais exigiria decidir regra de negócio (quando publicar,
  com qual payload) — isso é escopo do PRD de Booking/Payment, não desta fundação técnica.
  **Trade-offs:** o primeiro PRD de negócio ainda precisa trocar o payload/routing key de
  diagnóstico pelo real; a topologia e a biblioteca já estarão prontas, só o conteúdo muda.
  **Alternativas rejeitadas:** adiar toda a prova de mensageria para o primeiro PRD de negócio —
  rejeitada porque misturaria "aprender a publicar no RabbitMQ compartilhado" com "decidir a regra
  de confirmação da reserva" no mesmo PRD, dificultando isolar falhas.

### Riscos Conhecidos

- **Migration/role divergindo entre ambientes:** se o script de `db/bootstrap` for rodado com
  parâmetros diferentes em algum momento (ex.: nome de role digitado errado), os serviços falham
  silenciosamente ao tentar conectar. Mitigação: scripts idempotentes (`CREATE ROLE IF NOT EXISTS`
  equivalente) e checklist de verificação manual antes de V-01.
- **Testcontainers indisponível no ThinkPad (Docker/WSL):** os testes de integração desta fundação
  dependem de Docker local no ThinkPad (confirmado como único ambiente de desenvolvimento desta
  fundação, sem uso do `desenv`). Mitigação: já é uma dependência assumida pelo `dotnet-testing` do
  próprio catálogo de skills do autor; se o Docker do ThinkPad apresentar limitação de recursos,
  reavaliar o uso do `desenv` como exceção pontual, não como padrão desta fundação.
- **Contenção de recursos no broker/catálogo compartilhado com `ecad-sba`:** por reaproveitar
  `ecad-dev-rabbitmq` e `ecad-dev-openmetadata`, um pico de uso de um projeto pode afetar a
  disponibilidade percebida pelo outro (ambos são laboratórios pessoais de baixo tráfego, então o
  risco é mais operacional que de capacidade). Mitigação: vhost dedicado no RabbitMQ; tags de
  ownership no OpenMetadata; nenhuma alteração em configuração global de nenhum dos dois recursos
  compartilhados a partir deste repositório.

### Requisitos Especiais

Não aplicável — sem requisito de performance, segurança além do padrão (Fase 0 não tem
autenticação) ou conformidade regulatória nesta etapa (dados fictícios, sem PII).

### Conformidade com Skills

- Segue `dotnet-architecture` (Clean Architecture por serviço, formato monorepo com uma pasta por
  serviço) e `dotnet-program-setup` (Program.cs enxuto, Extensions por concern) integralmente.
- Segue `dotnet-dependency-config` para EF Core, `dotnet user-secrets`, versionamento central de
  pacotes e RabbitMQ, **exceto** o item "Containers locais" (ver Desvios).
- Segue `dotnet-observability` para health checks separados por intenção e logging correlacionado,
  **exceto** o padrão de OpenTelemetry (ver Desvios — já coberto pelo baseline, não é desvio novo).
- Segue `dotnet-testing` (Testcontainers Postgres/RabbitMQ) para toda a suíte automatizada desta
  fundação.
- Segue `react-architecture` (estrutura Base) para o frontend.

**Desvios identificados:**

| Desvio | Skill | Justificativa |
|--------|-------|----------------|
| Sem `docker-compose` local para Postgres/RabbitMQ (`examples/local-infrastructure.md`) | `dotnet-dependency-config` | Já decidido no baseline: dependências stateful rodam no `infra` via Coolify por limitação de disco do ThinkPad — não é uma decisão nova desta TechSpec |
| Sem OpenTelemetry/tracing/métricas formais | `dotnet-observability` | Já decidido no baseline: Fase 0 fica só em logging estruturado básico; Fase 1 (Resiliência) introduz tracing — não é uma decisão nova |
| "Banco por serviço" e contrato compartilhado como pacote NuGet não seguidos | `dotnet-architecture/examples/microservices.md` | O baseline já decidiu Service-Based Architecture com uma única instância PostgreSQL e ownership lógico por schema (não um banco por serviço) e contratos formais (OpenAPI/AsyncAPI/Data Contract), não um pacote de DTOs C# — decisão herdada do baseline, não nova aqui |
| `react-runtime-config` (template `runtime-env.js`/Nginx/`envsubst`) não aplicado; URLs de backend via `.env.development`/Vite | `react-runtime-config` | O frontend de teste roda apenas localmente nesta fase (ADR-003 não prevê deploy/containerização dele); introduzir o pipeline completo de runtime-config antecipa uma necessidade (imagem imutável entre ambientes) que ainda não existe — contraria a regra de disciplina de introdução de tecnologia do baseline. Revisitar quando/se o frontend precisar ser containerizado |

---

## Questões em Aberto

- [ ] Confirmar que o tipo de serviço `CustomMessaging` (verificado na documentação/schema atual do
      OpenMetadata) está de fato disponível na build 2.0.1 já instalada em `ecad-dev-openmetadata`
      — a pesquisa usou a documentação v2.0.x e o schema da branch `main`; se não estiver presente
      nessa build específica, o registro do RabbitMQ em V-06 pode precisar de um workaround (ex.:
      registrar como Kafka genérico) ou upgrade do OpenMetadata, o que afetaria também o `ecad-sba`.
- [ ] Definir o nome exato do Personal Access Token/credencial usada pelo script de registro do
      `CustomMessaging` e pela ingestion do Postgres — gerado no próprio OpenMetadata, com escopo
      mínimo, não reaproveitar o token administrativo do Coolify.

---

## Architecture Decision Records

Nenhuma ADR nova é necessária: esta TechSpec aplica decisões já aceitas, sem introduzir escolha
arquitetural nova ou alterar significativamente uma existente (os desvios de skill acima já eram
decorrência do baseline, não decisões novas).

- [ADR-001: Stack de backend — .NET / C# (ASP.NET Core)](../../docs/adr/adr-001-backend-stack-dotnet.md) — define a stack usada em todos os serviços desta TechSpec.
- [ADR-002: Broker de eventos da Fase 0 — RabbitMQ](../../docs/adr/adr-002-broker-fase0-rabbitmq.md) — define o broker e a convenção de nomes de evento usados em V-03.
- [ADR-003: Frontend de teste — React, sem gateway/BFF](../../docs/adr/adr-003-frontend-teste-react.md) — define a stack e o padrão de integração direta usados em V-04.

---

## Próximos Passos

1. **Aprovação:** revisar esta TechSpec (especialmente as decisões de bootstrap de banco e o
   escopo da capacidade de mensageria de diagnóstico) e promover para `techspec.md` com status
   `Aprovado`.
2. **Implementação:** usar `tsg-flow-task-creator` referenciando esta TechSpec para gerar as tasks
   de EN-01 a V-06.
3. **Depois desta fundação:** o primeiro PRD de negócio (provavelmente Catalog — cadastro/consulta
   de hospedagens, por ser o domínio sem dependência de saga) usa `tsg-flow-prd-creator`,
   referenciando `services/catalog` já existente em vez de partir do zero.
