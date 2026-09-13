---
status: done
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: [4.0]
---

<task_context>
<domain>services/booking, services/notification-worker</domain>
<type>integration</type>
<scope>core_feature</scope>
<!-- high: acoplamento irredutível -- publish e consume só provam valor juntos; separar quebraria
     a fatia (ver references/vertical-slicing.md). Revisão do plano já feita nesta própria geração
     de tasks. -->
<complexity>high</complexity>
<dependencies>external_apis</dependencies>
<unblocks>"8.0, 9.0"</unblocks>
<feedback_checkpoint>`dotnet test --filter "FullyQualifiedName~LocalizeStay.Messaging.IntegrationTests.DiagnosticPingFlowTests"` verde: publish por Booking, consumo + ACK pelo Worker, correlationId/causationId presentes no log. Manualmente, a UI de management do RabbitMQ mostra o vhost `/localize-stay` isolado dos recursos do `ecad-sba`</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="LocalizeStay.Messaging.IntegrationTests.DiagnosticPingFlowTests"</gate_command>
<gate_test_selector>Classe `DiagnosticPingFlowTests` do projeto `services/booking/tests/LocalizeStay.Messaging.IntegrationTests` (ou local equivalente combinando publisher de Booking e consumer do Worker)</gate_test_selector>
<gate_expected_result>Teste `DiagnosticPingFlowTests` passa: mensagem publicada por Booking é consumida e ACKada pelo Worker; o log estruturado do Worker contém o mesmo `correlationId`/`causationId` publicado por Booking</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Booking publica `DiagnosticPing` na exchange `diagnostics.topic` do vhost `/localize-stay`; Notification Worker consome a fila `notification.diagnostics`, faz ACK e loga com o mesmo correlationId/causationId</vertical_slice>
</task_context>

# Tarefa 6.0: Mensageria de diagnóstico — Booking publica, Notification Worker consome (V-03)

## Relacionada as User Stories

- N/A — TechSpec Standalone. Cobre a fatia V-03 (`techspec.md`, Mapa de Fatias Verticais).

## Visão Geral

Prova a capacidade de mensageria assíncrona entre Booking e um novo serviço, o Notification Worker,
usando o RabbitMQ compartilhado (`ecad-dev-rabbitmq`, reaproveitado do projeto `ecad-sba`) isolado por
um vhost dedicado (`/localize-stay`). Não implementa nenhum evento de negócio real da saga
(`PaymentRequested` etc., já nomeados no ADR-002) — usa uma mensagem de diagnóstico
(`DiagnosticPing`) só para provar que a topologia, a biblioteca e a correlação de IDs funcionam. Esta
é uma única fatia vertical (não duas) porque publish sem consumo, ou consumo sem publish, não prova
nada sozinho — o comportamento observável é o fluxo completo.

## Entrega Observável

- **Entrada ou gatilho:** `POST /internal/diagnostics/ping` em Booking (endpoint técnico interno, não
  um endpoint de negócio).
- **Resultado esperado:** Booking publica `DiagnosticPing` na exchange `diagnostics.topic` do vhost
  `/localize-stay`; o Notification Worker consome da fila `notification.diagnostics`, faz ACK e loga
  um evento estruturado com o mesmo `correlationId`/`causationId` publicado por Booking.
- **Checkpoint de feedback:** `dotnet test --filter "FullyQualifiedName~DiagnosticPingFlowTests"`
  (Testcontainers RabbitMQ) — verde. Manualmente (fora do gate automatizado): a UI de management do
  `ecad-dev-rabbitmq` real mostra o vhost `/localize-stay` isolado dos recursos do `ecad-sba`.
- **Seletor focalizado:** `LocalizeStay.Messaging.IntegrationTests.DiagnosticPingFlowTests`
- **Fora deste checkpoint:** nenhum evento de negócio real (`PaymentRequested`, `PaymentAuthorized`
  etc.) — a topologia e a biblioteca já estarão prontas, só o payload/routing key muda quando o
  primeiro PRD de negócio chegar; nenhuma exportação de AsyncAPI (isso é a task 8.0).

## Requisitos

- `MessagingExtensions.cs` em Booking configura o publisher via `Rmq.CloudEvents`, com ACK em sucesso
  e NACK sem requeue após falha final; retry com backoff e DLQ conforme
  `dotnet-dependency-config/examples/messaging-rabbitmq.md`.
- Toda connection string de aplicação já inclui o vhost `/localize-stay` — nenhum código do
  Localize Stay declara ou enumera exchanges/filas de outro vhost.
- Convenção de nomes: exchange `diagnostics.topic`, fila `notification.diagnostics`, dentro do vhost
  — os PRDs de negócio devem seguir o padrão `<domínio>.<propósito>` para as exchanges reais.
- Toda mensagem publicada carrega `correlationId`/`causationId`, mesmo sendo diagnóstico.
- Health check de RabbitMQ (`AddRabbitMQ`) exposto em `/health/ready` de Booking e do Worker.
- Notification Worker é um `BackgroundService` minimalista: sem camadas Domain/Application nesta
  etapa (não há regra de negócio a isolar ainda) — evita design antecipado.
- Endpoint `POST /internal/diagnostics/ping` deve ser removido ou isolado sob um flag quando o
  primeiro PRD de Booking chegar, para não vazar para o contrato público (documentar essa nota no
  próprio código, não implementar o flag agora).

## Arquivos Envolvidos

- **Criar:**
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/MessagingExtensions.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/DiagnosticsEndpoints.cs` (Minimal API
    `POST /internal/diagnostics/ping`)
  - `services/notification-worker/LocalizeStay.Notification.sln`
  - `services/notification-worker/src/1-Services/LocalizeStay.Notification.Worker/Program.cs`
  - `services/notification-worker/src/1-Services/LocalizeStay.Notification.Worker/Extensions/MessagingExtensions.cs`
  - `services/notification-worker/src/1-Services/LocalizeStay.Notification.Worker/DiagnosticPingConsumer.cs`
    (`BackgroundService`)
  - `services/booking/tests/LocalizeStay.Messaging.IntegrationTests/DiagnosticPingFlowTests.cs` (+
    `.csproj`) — Testcontainers RabbitMQ cobrindo publish→consume→ack
- **Modificar:**
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Program.cs` (adicionar
    `.AddMessagingConfiguration(builder.Configuration)`)
- **Referência:**
  - `docs/adr/adr-002-broker-fase0-rabbitmq.md` — decisão de broker e convenção de nomes de evento
  - `services/booking/**` (task 4.0) — esqueleto já existente que esta task estende
  - `dotnet-dependency-config/examples/messaging-rabbitmq.md` — padrão de retry/backoff/DLQ
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — `Rmq.CloudEvents`, retry/backoff/DLQ
  - `dotnet-observability` — logging correlacionado com `correlationId`/`causationId`
  - `dotnet-testing` — Testcontainers RabbitMQ

## Subtarefas

- [ ] 6.1 Implementar `MessagingExtensions.cs` em Booking (publisher `Rmq.CloudEvents`, ACK/NACK,
      retry+DLQ) e o endpoint `POST /internal/diagnostics/ping`
- [ ] 6.2 Criar a solution do Notification Worker (`BackgroundService`, sem Domain/Application) com
      `MessagingExtensions.cs` (consumer) e `DiagnosticPingConsumer.cs`
- [ ] 6.3 Garantir que toda mensagem carrega `correlationId`/`causationId` e que o Worker os loga
      estruturadamente ao consumir
- [ ] 6.4 Expor health check de RabbitMQ em `/health/ready` de Booking e do Worker
- [ ] 6.5 Criar `DiagnosticPingFlowTests` (Testcontainers RabbitMQ) cobrindo publish→consume→ack e a
      presença dos IDs de correlação no log
- [ ] 6.6 Criar manualmente o vhost `/localize-stay` no `ecad-dev-rabbitmq` real (UI/API de
      management) e validar visualmente o isolamento dos recursos do `ecad-sba` — fora do gate
      automatizado, registrar evidência

## Sequenciamento

- Bloqueado por: 4.0 (Booking precisa existir para ganhar o endpoint de diagnóstico e o publisher)
- Desbloqueia: 8.0 (AsyncAPI documenta esta topologia), 9.0 (OpenMetadata registra o vhost como
  `CustomMessaging`)
- Paralelizável: Não (única fatia que cruza Booking e Worker; não há outra task paralela com a mesma
  superfície de arquivo nesta etapa)

## Rastreabilidade

- Esta tarefa cobre: Fatia V-03 da TechSpec.
- Evidência esperada: `DiagnosticPingFlowTests` verde; log do Worker mostra o mesmo `correlationId`
  publicado por Booking; vhost `/localize-stay` visível e isolado no RabbitMQ real (evidência manual).

## Detalhes de Implementação

Da TechSpec (`techspec.md`, "RabbitMQ — capacidade de mensageria (V-03)"):

> O broker (`ecad-dev-rabbitmq`) é compartilhado com o projeto `ecad-sba` [...]. Para não acoplar as
> duas topologias no mesmo namespace lógico, o Localize Stay usa um **vhost dedicado**
> (`/localize-stay`), criado uma vez via UI/API de management do RabbitMQ [...]. Biblioteca:
> `Rmq.CloudEvents`, com ACK em sucesso e NACK sem requeue após falha final; retry com backoff e DLQ
> [...]. Convenção de nomes provada aqui com `diagnostics.topic` (exchange) e
> `notification.diagnostics` (fila) [...]. Toda mensagem publicada carrega
> `correlationId`/`causationId`, mesmo sendo uma mensagem de diagnóstico sem valor de negócio — a
> infraestrutura de correlação precisa existir antes do primeiro evento real, não pode ser adicionada
> depois como retrofit.

O teste automatizado usa Testcontainers RabbitMQ (uma instância efêmera), não o `ecad-dev-rabbitmq`
real — a criação do vhost real e a inspeção visual na UI de management são verificação manual (task
2.0 tem o mesmo padrão para o Postgres).

**Convenções da stack (das skills consultadas):**
- Retry com backoff + DLQ desde o primeiro publisher (convenção herdada pelos PRDs de negócio
  futuros), conforme `dotnet-dependency-config/examples/messaging-rabbitmq.md`.
- Logging estruturado incluindo `correlationId`/`causationId` em toda mensagem, conforme
  `dotnet-observability`.

## Prontidão para Implementação

- **Decisões fechadas:** nomes exatos `diagnostics.topic` (exchange) e `notification.diagnostics`
  (fila); vhost `/localize-stay`; biblioteca `Rmq.CloudEvents`; payload é diagnóstico, não um evento
  de negócio real — não inventar campos de domínio.
- **Limites de decisão do implementer:** estrutura exata do payload `DiagnosticPing` (além de
  `correlationId`/`causationId`); detalhes de configuração de retry/backoff (número de tentativas,
  intervalo).
- **Dependências disponíveis:** esqueleto de Booking (task 4.0) já compilando e respondendo
  `/health/ready`.
- **Artefatos exigidos pelo gate:** `DiagnosticPingFlowTests.cs` e o projeto de testes são criados
  nesta própria task; o Testcontainers RabbitMQ é efêmero (não versionado); a criação do vhost real
  é manual, documentada, não bloqueia o gate automatizado.
- **Dependências futuras:** Nenhuma — o endpoint de diagnóstico e o payload serão substituídos por um
  PRD de negócio futuro, mas isso não é uma dependência desta task, é trabalho posterior que a
  reutiliza.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Messaging.IntegrationTests.DiagnosticPingFlowTests"`
- [ ] O seletor encontra pelo menos um teste e não executa casos sem relação com esta task
- [ ] Build compila sem erros: `dotnet build services/booking/LocalizeStay.Booking.sln && dotnet build services/notification-worker/LocalizeStay.Notification.sln`
- [ ] `POST /internal/diagnostics/ping` em Booking retorna sucesso e resulta em uma mensagem
      publicada na exchange `diagnostics.topic`
- [ ] O Worker consome a mensagem, faz ACK (mensagem não reaparece na fila) e loga
      `correlationId`/`causationId` idênticos aos publicados
- [ ] `/health/ready` de Booking e do Worker reporta o status da conexão RabbitMQ
- [ ] Checkpoint de feedback executado: criação manual do vhost `/localize-stay` no
      `ecad-dev-rabbitmq` real e inspeção visual na UI de management confirmando isolamento do
      `ecad-sba` (evidência manual)
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente esta fatia (publish→consume→ack de diagnóstico) e não depende
      de contratos (8.0) ou catalogação (9.0)
