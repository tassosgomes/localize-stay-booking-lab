# Revisão — Task 8.0: Contratos exportados e validados (V-05)

- Modo: focused (primeira revisão)
- Task: `tasks/prd-fundacao-fase0/8_task.md` (enabling/static, V-05)
- Branch: `feature/prd-fundacao-fase0`
- Checkpoint base: `2a12150` (= HEAD; sem commits novos — a entrega está toda no working tree)
- Data (UTC): 2026-09-12

## Gate (executado ANTES da semântica, pelo validator)

- Comando do contrato: `scripts/ai-flow/gate.sh --static`
- Resultado: `GATE: APROVADO` (exit 0) — 10 arquivos alterados (.NET: 0, node: 0);
  format pulado; build ok nos 4 `.sln` + `tsc` ok; testes n/a (static).
- Gate passou → revisão semântica prosseguiu. Nenhum exit 1/2.

## Escopo revisado (diff desde 2a12150 + untracked)

- Tracked modificados (só transição de fase, sem conteúdo funcional):
  - `tasks/prd-fundacao-fase0/8_task.md` — `status: pending` → `validating`
  - `tasks/prd-fundacao-fase0/flow-state.json` — `active_task` 7.0 → 8.0, checkpoint registrado
- Untracked no escopo da task (todos revisados):
  - `contracts/openapi/catalog.json`, `contracts/openapi/booking.json`,
    `contracts/openapi/payment.json`
  - `contracts/asyncapi/diagnostics-v1.yaml`
  - `contracts/data-contracts/TEMPLATE.md`
  - `scripts/contracts/export-openapi.sh`
- Fora do escopo (não revisados como entrega; `scripts/ai-flow/*` segue infra):
  `scripts/ai-flow/gate.sh`, `scripts/ai-flow/gate.contract.md` (untracked, infra do gate).

## Evidência estática reexecutada pelo validator (0 erros)

- `npx --yes @apidevtools/swagger-cli validate contracts/openapi/catalog.json`
  → `contracts/openapi/catalog.json is valid` (exit 0)
- `... validate contracts/openapi/booking.json` → `is valid` (exit 0)
- `... validate contracts/openapi/payment.json` → `is valid` (exit 0)
- `npx --yes @asyncapi/cli@2 validate contracts/asyncapi/diagnostics-v1.yaml`
  → `File ... is valid! ... don't have governance issues` (exit 0)

## Verificações do enunciado

- (a) JSON exportados REAIS do código atual: **OK, com prova viva.**
  Rodei o Booking atual com o mesmo procedimento do script (envs dummy, sem tocar
  em banco/broker) e o `/swagger/v1/swagger.json` servido saiu **byte-idêntico**
  (`diff` limpo) a `contracts/openapi/booking.json` — contém exatamente o único
  endpoint real, `POST /internal/diagnostics/ping`
  (`services/booking/.../DiagnosticsEndpoints.cs:20`), com titles/versão
  (`LocalizeStay Booking API`/`v1`) iguais ao `SwaggerExtensions.cs` de cada API.
  `catalog.json`/`payment.json` com `paths: {}` são honestos: as duas APIs têm
  Swashbuckle configurado mas **zero** `MapGet`/`MapPost` (grep confirma).
  Prova escrita só em `/tmp`; nenhum arquivo do repo foi modificado.
- (b) AsyncAPI reflete a topologia real de V-03: **OK.**
  `diagnostics.topic` (topic, durable), routing key `diagnostics.ping`, fila
  `notification.diagnostics` — idênticos a `DiagnosticsTopology.cs` do Booking
  (Exchange/RoutingKey/Queue) e do Notification Worker (idem + DLX
  `notification.diagnostics.dlx` + DLQ `notification.diagnostics.dlq`, ambos no
  YAML); vhost `/localize-stay` = `MessagingExtensions.ExpectedVirtualHost`
  (`Extensions/MessagingExtensions.cs:14`); CloudEvent type
  `com.localizestay.diagnostics.ping.v1` igual nos dois `DiagnosticsTopology.cs`;
  `correlationId`/`causationId` obrigatórios no payload **e** nos headers
  `x-correlation-id`/`x-causation-id` (critério de sucesso da task atendido);
  retry "máx. 5 tentativas" = default `MaxAttempts: 5` em `RabbitMqSettings.cs`;
  semântica ACK/NACK/DLQ do YAML confere com `DiagnosticPingConsumer.cs`
  (ACK em sucesso, NACK sem requeue na falha final, NACK com requeue no
  desligamento). Nomes de eventos futuros (`PaymentRequested`, …) conferem com a
  ADR-002. Ressalva não bloqueante: ver rec. 3 (host do server).
- (c) Validações reexecutadas: **OK** — ver seção de evidência acima (4/4, 0 erros).
- (d) TEMPLATE sem dataset real: **OK** — só placeholders `<!-- PREENCHER -->`;
  `available_accommodations_v1`/`reservation_calendar_v1` não aparecem como
  datasets (só alusão genérica no cabeçalho como exemplos futuros); estado
  `rascunho`.
- (e) Script de export versionado e reprodutível: **OK.**
  `scripts/contracts/export-openapi.sh` (modo 755) referencia os 3 `.csproj`
  corretos (todos existem), portas `5101/5102/5103` = `urls` dos
  `appsettings.json` de cada API, usa connection strings/credenciais dummy e
  nunca toca em Postgres/RabbitMQ reais; a prova viva acima confirma a
  reprodutibilidade. Ressalva não bloqueante: ver rec. 2 (`--no-build`).
- (f) Escopo de scripts: **OK** — só `scripts/contracts/*` tratado como entrega;
  nada em `scripts/ai-flow/*` foi criado/modificado como parte da task.

## Bloqueantes

Nenhum. Todos os critérios de sucesso verificáveis da task foram atendidos:
3× `swagger-cli validate` + 1× `asyncapi validate` com 0 erros (reexecutados);
`correlationId`/`causationId` documentados; TEMPLATE genérico; artefatos
produzidos somente nesta task; nenhuma dependência de catalogação OpenMetadata (9.0).

## Recomendações (não bloqueantes — 3)

1. `contracts/openapi/booking.json` declara resposta `200 OK`, mas
   `DiagnosticsEndpoints.cs:61` retorna `Results.Accepted` (202) com corpo
   `PingAcceptedResponse` — limitação de inferência do Swashbuckle em minimal
   APIs. Sugerir `ProducesResponseType`/`Produces` quando o primeiro PRD de
   Booking (contract-creator) enriquecer o contrato; não invalida V-05.
2. `scripts/contracts/export-openapi.sh` usa `dotnet run --no-build`, que exige
   build prévio (o gate o fez, mas num clone fresco o script falha). Documentar
   o pré-requisito `dotnet build` no cabeçalho ou remover `--no-build`.
3. `contracts/asyncapi/diagnostics-v1.yaml` (`servers.rabbitmq.host:
   ecad-dev-rabbitmq:5672`) — hostname não rastreável em nenhum config do repo
   (`appsettings.json` usam `localhost`; nenhum `compose`/env referencia
   `ecad-dev-rabbitmq`). Aceitável (host é environment-specific e vhost/nomes —
   a parte contratual — conferem), mas ancorar a origem (task 6.0/compose) num
   comentário para os PRDs de negócio não copiarem um host sem proveniência.

## Imutabilidade

- HEAD antes e depois da revisão: `2a12150e557aa7bc9d6460d9063cbe76a317ac00`
  (sem commits durante a revisão; serviço temporário do Booking usado na prova
  viva foi encerrado — porta 5102 fechada — e só escreveu em `/tmp`).
- `git status --porcelain` inalterado: `M 8_task.md`, `M flow-state.json`,
  `?? contracts/`, `?? scripts/` (sendo `scripts/ai-flow/*` infra fora do escopo).
- Nenhum código, status, task ou commit foi editado pelo validator; apenas este
  relatório foi criado.

## Resultado

**VALIDAÇÃO APROVADA (3 recs)**
