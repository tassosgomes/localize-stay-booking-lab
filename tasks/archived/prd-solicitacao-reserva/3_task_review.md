# Task Review 3.0 — Endpoint completo `POST /v1/reservations` (V-03)

- **Mode:** focused (primeira revisão)
- **Validator:** worker fresco (sessão independente do implementer)
- **HEAD revisado:** `7cb832c` (checkpoint 2.0; diff da task em unstaged + untracked)
- **Base do diff:** checkpoint `7cb832c`; escopo confirmado: 15 arquivos criados + 9 modificados, conforme relatório do implementer

## Resultado

**VALIDATION APPROVED** — 0 bloqueantes, 3 recomendações não bloqueantes.

## Gate (contrato de verificação da task)

Comando (executei pessoalmente, não reutilizei evidência do implementer):

```
scripts/ai-flow/gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationEndpointTests"
→ GATE: APROVADO (exit 0)
→ testes: ok (ReservationEndpointTests = 7)
→ build: 4 soluções 0 Warning(s) 0 Error(s); dotnet format ok (19 arquivos)
```

O `gate_expected_result` foi satisfeito: 7 testes (1 sucesso + 6 rejeições), cada um com status/code exatos do `api-contract.yaml`, e o cenário de sucesso confirma a publicação do evento com `correlationId`/`causationId` = `reservationId`.

## Escopo revisado

- Diff unstaged (9 modificados: `Directory.Packages.props`, 2 csprojs de teste, `Program.cs`, `MessagingExtensions`, `MiddlewarePipelineExtensions`, `DiagnosticsEndpoints`, `FakeCatalogServerFactory`, `3_task.md`/`flow-state.json` = metadata procedural pending→in_progress).
- Untracked (15): `Cqrs/Dispatcher.cs`, `RequestReservationCommand/Validator/Handler`, `IReservationRequestedPublisher`, `ReservationRequestedRmqPublisher`, `Contracts/` (3), `ErrorHandling/GlobalExceptionHandler`, `Endpoints/ReservationEndpoints`, `ApplicationExtensions`, `RequestReservationCommandHandlerTests` (11 casos), `ReservationEndpointTests` (7 fatos).
- Frontend e demais serviços (catalog/payment/notification-worker): **zero diff** — nenhum escopo das tasks 4.0–6.0.

## Verificação dos pontos sensíveis

| # | Ponto | Evidência | Status |
|---|---|---|---|
| a | 7 cenários com status/code/title/problem+json exatos | `AssertRejectionAsync` (ReservationEndpointTests.cs:290-307) valida status HTTP, `Content-Type: application/problem+json`, `status`/`code`/`title` literais idênticos aos examples do `api-contract.yaml` (linhas 216-319), `instance: /v1/reservations`, `type`/`detail` não vazios; `GlobalExceptionHandler` produz os mesmos `type` URIs (`https://localize-stay.lab/problems/*`) | ✅ |
| b | RN-02/RN-03 → 422 sem chamar Catalog | Ordem normativa em `RequestReservationCommandHandler.cs:33-34` (Ensure* antes de `CheckAvailabilityAsync`); unit tests `Times.Never` no client (casos checkOut==checkIn, guests 0/-2); integração `RequestsReceived == 0` | ✅ |
| c | 404 e active=false → 422 ACOMODACAO_INDISPONIVEL | Handler traduz `null`→`AcomodacaoIndisponivelException` (Handler:41-47); `active=false` rejeitado no domínio via `Reservation.Create`; teste único cobre os dois desfechos (AC única do PRD) | ✅ |
| d | Falha Catalog → 503 CATALOG_INDISPONIVEL, sem Reservation/evento, com traceId | Integração ServerError → 503 + `traceId` não vazio + 0 persistidas + 0 eventos; `CatalogUnavailableException` mapeada só para 503 no handler de exceção | ✅ |
| e | Sucesso publica `booking.reservation_requested.v1` com correlationId/causationId=reservationId (ADR-002) | Teste consume de fila real (RabbitMQ Testcontainers, exchange `booking.reservation-events`, routing key `reservation.requested`): `type` do envelope, headers `x-correlation-id`/`x-causation-id` = id, `data.ReservationId`/`Status=solicitada`/`TotalAmount=1050` | ✅ |
| f | Preço/moeda/total congelados como string decimal | `MoneyStringJsonConverter` (F2, InvariantCulture) só no `ReservationResponseDto`; integração exige `ValueKind.String`, `"350.00"`/`"1050.00"` (3 noites × 350), `currency=BRL`; Domain segue `decimal` puro; evento publica número (regra de string é exclusiva do HTTP, conforme task) | ✅ |
| g | Nenhuma rejeição persiste/publica | `CountPersistedReservationsAsync == 0` e `BasicGetOneAsync == null` em toda rejeição da integração; `AddAsync`/`PublishAsync` `Times.Never` nos unit tests | ✅ |
| h | Lacunas resolvidas | `GlobalExceptionHandler` criado (RFC 9457 + `Extensions["code"]` + `traceId` em 5xx, registrado via `AddExceptionHandler`+`UseExceptionHandler` em Program.cs); `IDispatcher` criado em `Application/Cqrs/Dispatcher.cs` (command-side only, queries adiadas para F02) — a task assumia que a fundação o entregaria; a criação foi necessária, está no espírito do esqueleto CQRS citado e não extrapola o escopo | ✅ |
| i | Nenhum escopo 4.0–6.0 | Zero diff em `frontend/` e nos outros serviços; sem GET `/reservations/{id}` (F02), sem avanço de saga | ✅ |

Pontos adicionais verificados: 3.4 (diagnóstico isolado — `WithGroupName("internal")` + asserção de que `swagger/v1/swagger.json` não contém `internal/diagnostics`); publish best-effort sem outbox com teste dedicado (publisher falha → Reservation permanece); log estruturado nos pontos de decisão com `correlationId = Reservation.Id`; `Location: /v1/reservations/{id}` no formato do contrato; topologia RMQ declarada idempotente no boot.

## Ausência do dredd — avaliação

**Não bloqueante.** Fundamentos:

1. O contrato de verificação da task (`gate_command`/`gate_expected_result`) é behavioral via `ReservationEndpointTests` — executado e verde por mim. O dredd é declarado na própria task como "evidência manual, fora do gate automatizado"; `tasks.md` (V-03) exige apenas o gate.
2. A justificativa do implementer é factual e verificada por mim: (a) dredd ausente no ambiente (`npx --no-install dredd` falha; binário não instalado); (b) o dredd exigiria a stack real em `:5102`, que depende de infra do homelab via `dev-up`; (c) **o serviço Catalog real não implementa `GET /accommodations/{id}/availability-check`** (varredura em `services/catalog/src` retorna vazio) — logo o dredd falharia na path de dependência independentemente da correção de Booking; (d) a divergência `servers.url` 5000/5010 vs 5101/5102 já está registrada como não bloqueante na TechSpec (linhas 537-541, 571-572).
3. A substância que o dredd agregaria (conformidade dos 7 desfechos com os examples do contrato) está coberta assertivamente nos testes de integração, campo a campo.

## Recomendações (não bloqueantes)

1. **Executar dredd quando a infra permitir** — idealmente antes/durante a V-FE-02 (task 6.0), que já sobe Booking+Catalog reais; registrar que a path `availability-check` do contrato falhará até Catalog implementá-la (limitação estrutural pré-existente, não desta task). Complementa: corrigir `servers.url` do `api-contract.yaml` (5000→5102, 5010→5101) no ajuste de contrato já previsto na TechSpec.
2. **Nuance do 400 de binding**: JSON malformado/tipo errado (ex.: `guestsCount: "dois"`) é rejeitado pelo binding do Minimal API com 400 do framework, sem a extensão `code: VALIDATION_ERROR` — apenas o caminho FluentValidation (campo vazio) produz o ProblemDetails completo do contrato. Não há cenário de 400 entre os 7 do gate, mas vale uniformizar (ex.: `IExceptionHandler` para `BadHttpRequestException` ou `EmptyBody` check) numa task futura de polimento.
3. **`Dispatcher` via reflexão**: resolução dinâmica de handler é adequada ao CQRS nativo agora; quando F02 introduzir queries, considerar cache de `handlerType`/`MethodInfo` se o volume de commands crescer.

## Imutabilidade

HEAD `7cb832c1b64046b804bb010fbd2b4b5706fb362e` e árvore de trabalho conferidos idênticos antes e depois da revisão — nenhuma mudança durante a validação.
