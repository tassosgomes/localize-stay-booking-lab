# Revisão — Task 6.0: Mensageria de diagnóstico (V-03)

- Modo: focused (primeira revisão; complexity HIGH no perfil standard passa por focused)
- Task: `tasks/prd-fundacao-fase0/6_task.md` (lida integralmente, 187 linhas; vertical/behavioral)
- Checkpoint base: `b0ee1a7` (ancestral de HEAD confirmado)
- HEAD revisado: `b0ee1a7d4cc7a192ad2622abcd5159d20079542c` (sem commits novos; diff = worktree + untracked)
- Escopo revisado: diff worktree (11 arquivos) + untracked
  (`services/booking/**/DiagnosticsEndpoints.cs`, `Extensions/MessagingExtensions.cs`,
  `Extensions/RabbitMqSettings.cs`, `Extensions/RabbitMqHealthConnection.cs`, `Messaging/*`,
  `services/booking/tests/LocalizeStay.Messaging.IntegrationTests/**`,
  `services/notification-worker/**`). `scripts/` untracked é infra do gate, fora do escopo da task.

## Gate (antes da semântica)

- Contrato: `scripts/ai-flow/gate.sh --filter="LocalizeStay.Messaging.IntegrationTests.DiagnosticPingFlowTests"`
- Resultado do gate (executado pelo validator): `GATE: APROVADO` — build das 4 solutions
  ok (0 warnings/errors), `DiagnosticPingFlowTests=2`, `EXIT_CODE=0`.
- Reexecução independente de evidência (validator):
  `dotnet test services/booking/tests/LocalizeStay.Messaging.IntegrationTests/... --filter
  "FullyQualifiedName~LocalizeStay.Messaging.IntegrationTests.DiagnosticPingFlowTests"` →
  `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2` (`EXIT_CODE=0`, Testcontainers
  RabbitMQ `rabbitmq:4.3.5-management-alpine` + Postgres efêmeros funcionais).

## Verificações do escopo (a–h)

- (a) vhost `/localize-stay` imposto por fail-fast, nunca default: APROVADO.
  `Booking/Extensions/MessagingExtensions.cs:14` (`ExpectedVirtualHost = "/localize-stay"`);
  `Booking/Extensions/RabbitMqSettings.cs:28` e
  `Worker/Extensions/RabbitMqSettings.cs:34` rejeitam qualquer `VirtualHost` diferente
  (comparação `Ordinal` exata); `Read()` lança `InvalidOperationException` com orientação de
  user-secrets. Todas as conexões (`MessagingExtensions`, `RabbitMqHealthConnection`,
  `DiagnosticPingConsumer`, testes) usam a constante — nenhum `"/"` default nem enumeração
  de outro vhost (grep `VirtualHost` só retorna o esperado + menção `ecad-sba` em comentário).
- (b) ACK sucesso / NACK sem requeue final / DLQ; retry+backoff: APROVADO.
  Publisher Booking via `Rmq.CloudEvents` com `DefaultRetry` (MaxAttempts 5, exponencial +
  jitter) e exchange `diagnostics.topic` declarada. Consumer
  (`Worker/Messaging/DiagnosticPingConsumer.cs`): `BasicAckAsync` no sucesso (`:175`),
  `BasicNackAsync(requeue:false)` em envelope inválido (`:154`) e após `MaxAttempts`
  (`:195`), `requeue:true` só em cancelamento/desligamento (`:180`); loop com
  `ComputeBackoff` exponencial + jitter com teto 30s; DLX/DLQ
  (`notification.diagnostics.dlx`/`.dlq`, quorum, `x-delivery-limit`, DLX binding) declarados
  de forma idempotente espelhando `QueueManager` da biblioteca. Teste cobre fila principal
  vazia + consumers ≥ 1 e DLQ vazia no caminho feliz.
- (c) correlationId/causationId em toda mensagem e log: APROVADO.
  `Booking/DiagnosticsEndpoints.cs:33-44` gera ambos (causationId == correlationId na origem),
  publica em payload + headers `x-correlation-id`/`x-causation-id` e loga ambos (`:54-59`);
  Worker loga ambos no consumo (`DiagnosticPingConsumer.cs:166-173`) e no erro final;
  `TryUnwrap` exige ambos (fallback header→payload, rejeita vazios). Testes asserem igualdade
  e presença nos dois lados (`DiagnosticPingFlowTests.cs:57-72`, `:101-112`).
- (d) sem senha versionada: APROVADO. Ambos `appsettings.json` (Booking e Worker) NÃO contêm
  chave `Password` (grep `Password` em `services/**/​*.json` vazio); senha só via env/user-secrets
  (`RabbitMqSettings.Read` orienta `dotnet user-secrets`). `guest:guest` aparece SOMENTE no
  código de teste efêmero (Testcontainers) e nas factories de teste — aceitável.
- (e) health RabbitMQ nos dois: APROVADO. Booking
  (`Extensions/HealthCheckExtensions.cs:19-30`) e Worker
  (`Extensions/HealthCheckExtensions.cs:17-28`) registram `AddRabbitMQ` tag `ready` via holder
  preguiçoso compartilhado; ambos mapeiam `/health/live` (self) e `/health/ready`. Testes
  asserem `/health/ready` OK + `Healthy` nos dois hosts (`DiagnosticPingFlowTests.cs:60-63`, `:125-128`).
- (f) SÓ DiagnosticPing — evento real presente = REPROVE: APROVADO (sem escopo extra).
  Grep por `PaymentRequested|PaymentAuthorized|BookingCreated|Saga` em Booking src, Worker src
  e Messaging tests: zero ocorrências. Ambos `DiagnosticPing.cs` documentam que eventos reais
  (ADR-002) chegam depois; duplicação intencional do contrato documentada (AsyncAPI é task 8.0).
- (g) Worker sem Domain/Application: APROVADO. `services/notification-worker/` contém só
  `LocalizeStay.Notification.sln` + `src/1-Services/LocalizeStay.Notification.Worker/`
  (`Program.cs`, `Extensions/*`, `Messaging/*`) — sem camadas 2-Application/3-Domain/4-Infra.
  Comentário no consumer (`:13-14`) justifica a ausência (sem regra de negócio).
- (h) endpoint `/internal/diagnostics/ping` marcado para remoção/flag: APROVADO.
  Nota no próprio código (`DiagnosticsEndpoints.cs:30-31`): remover ou isolar sob flag no
  primeiro PRD de Booking; rota sob `/internal`, fora do contrato público; `Program.cs`
  registra via `AddMessagingConfiguration` + `MapDiagnosticsEndpoints`.

## Vhost real + UI management (fora do gate)

- Subtarefa 6.6 (criação manual do vhost `/localize-stay` no `ecad-dev-rabbitmq` real + inspeção
  visual do isolamento vs `ecad-sba`) NÃO possui evidência registrada no repo (grep em
  `tasks/prd-fundacao-fase0` vazio; `flow-state.json` cita só o gate). Conforme instrução da
  convocação, ausência NÃO reprova — registrada aqui como limitação/pedência manual (ver rec 2).
  O vhost é criado via API de management nos testes efêmeros (`DiagnosticsFixture`,
  `CustomWebApplicationFactory`), provando o procedimento automatizável.

## Bloqueantes

Nenhum.

## Recomendações (não bloqueantes, 3)

1. Cobertura ponta a ponta em um único fluxo: hoje o teste 1 prova Booking→broker (via
   `BasicGet`) e o teste 2 prova broker→Worker (publish via publisher do Worker + log + ACK).
   A composição prova a fatia (mesma topologia/envelope/CloudEventType nos dois lados), mas um
   futuro teste `POST /internal/diagnostics/ping` → espera do `correlationId` no log capturado
   do Worker fecharia o laço cross-process em uma asserção. Não bloqueia: convenções idênticas
   + envelope CloudEvents validado nos dois lados já dão confiança transitiva.
2. Registrar evidência manual da subtarefa 6.6 quando o homelab estiver acessível (print/linha
   na task ou ADR): vhost `/localize-stay` no `ecad-dev-rabbitmq` isolado do `ecad-sba`.
3. Assimetria intencional dos settings (record mínimo em Booking vs completo no Worker, com
   `DeliveryLimit`/`PrefetchCount` só no Worker): considerar alinhar ou documentar em uma linha
   por que o publisher não precisa desses campos — hoje está implícito; só higiene futura.

## Imutabilidade

- HEAD antes e depois da revisão: `b0ee1a7d4cc7a192ad2622abcd5159d20079542c` (inalterado).
- Nenhum arquivo de código, status, task ou commit foi editado pelo validator; apenas leitura,
  `gate.sh`, `dotnet test` (artefatos `bin/`/`obj/` regenerados, sem mudança de fontes) e a
  escrita deste relatório. Nova validação exigida se o código mudar.

## Resultado

VALIDAÇÃO APROVADA (3 recs)
