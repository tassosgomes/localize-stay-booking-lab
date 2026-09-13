# Resumo de Tarefas de Implementação — Conclusão da Saga (Booking F04)

> **TechSpec de origem:** [`techspec.md`](techspec.md) (rev. 2026-09-12, Status: Aprovado, handoff `approved`)
> **PRD de origem:** [`prd.md`](prd.md)
> **API Contract:** [`api-contract.yaml`](api-contract.yaml) (AsyncAPI 3.1, v1.0.0, Status: Aprovado); [`api-contract.md`](api-contract.md) registra as decisões
> **ADRs pertinentes:** [ADR-001: Stack .NET](../../docs/adr/adr-001-backend-stack-dotnet.md), [ADR-002: RabbitMQ](../../docs/adr/adr-002-broker-fase0-rabbitmq.md)
> **Baseline e domínio:** [`context/architecture-baseline.md`](../../context/architecture-baseline.md), [`domains/booking/domain.md`](../../domains/booking/domain.md)
> **Status do plano:** Confirmado para implementação
> **Regra de entrega:** a task de comportamento é uma fatia vertical validável isoladamente

## Visão Geral

Fecha a Reservation Saga no Booking para os dois resultados publicados por Payment. A mesma fatia
consome `payment.payment_authorized` e `payment.payment_rejected`, localiza a Saga por
`correlationId`, confirma ou cancela a Reservation, registra o resultado e o instante UTC da
transição terminal (`TerminalTransitionAt`), publica o evento final correspondente e ignora/loga
mensagens não correlacionáveis ou tardias sem alterar estados terminais. Uma falha best-effort na
publicação final não desfaz a transição já persistida.

> **Emenda de alinhamento (2026-09-13):** `Confirm()`/`Cancel(string)` e o cálculo de
> `confirmedAt`/`cancelledAt` só no momento da publicação foram substituídos por
> `Confirm(DateTime)`/`Cancel(string, DateTime)`, que persistem `TerminalTransitionAt` no mesmo
> commit da transição, com uma migration aditiva nova. A mudança decorre do handoff EN-01 de
> `tasks/prd-publicacao-reservation-calendar/techspec.md` e da ADR-005 (Accepted); `techspec.md` e
> `1_task.md` já refletem a emenda.

O PRD declara a feature indivisível para a demonstração da coreografia; portanto Domain, Application,
Infrastructure, consumidores, wiring e testes permanecem em uma única V-01. O plano não cria endpoint
HTTP nem altera o frontend.

O estado de `main` foi confirmado durante a criação do plano: o merge `522a6cd` já fornece o
habilitador de F02 (`SagaState.Authorized`/`Rejected`, `ReservationSaga.CancellationReason` e a
migration `20260913005542_AddSagaCancellationReason`), e F03 já fornece
`IReservationRepository.UpdateAsync`. Esses artefatos serão reutilizados, não duplicados.

`techspec.draft.md` não foi consumida.

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|-------|---------|------------|
| `dotnet-architecture` | `.agents/skills/dotnet-architecture/SKILL.md` | Mantém Clean Architecture, CQRS nativo sem MediatR, domínio com invariantes, handlers na Application, consumidores como adapters finos e DI por tipo |
| `dotnet-dependency-config` | `.agents/skills/dotnet-dependency-config/SKILL.md` | Reutiliza EF Core/PostgreSQL e `Rmq.CloudEvents` 1.1.1; declara exchanges/filas via configuração existente, sem pacote/versão nova ou migration nova |
| `dotnet-testing` | `.agents/skills/dotnet-testing/SKILL.md` | Define xUnit + Moq + AwesomeAssertions em testes unitários e `WebApplicationFactory` + Testcontainers/PostgreSQL/RabbitMQ na integração |

Não foram consultadas skills de frontend, REST, performance, observabilidade dedicada, produção ou
design patterns: não há UI/endpoint novo, requisito de performance/telemetria nova ou variação de
algoritmo que exija outro padrão.

## Fases de Implementação

As fases agrupam comportamento e feedback, não camadas arquiteturais.

### Fase 1 — V-01: concluir a saga por autorização e rejeição

Implementar os dois caminhos assíncronos de ponta a ponta, a proteção RN-11, o motivo de negócio de
cancelamento, a publicação CloudEvents e o isolamento da falha best-effort. O checkpoint é a suíte
focalizada unitária e de integração contra Postgres/RabbitMQ reais.

## Mapa de Entrega e Feedback

| Slice | Task | Comportamento observável | Gate executável | Seletor focalizado | Bloqueado por |
|-------|------|--------------------------|-----------------|---------------------|---------------|
| V-01 | 1.0 | Um resultado de Payment correlacionado leva uma Reservation solicitada a `confirmada` ou `cancelada` e publica o evento final com payload contratado; resultado tardio, duplicado, conflitante ou não correlacionável é ignorado/logado; falha de publicação não reverte o estado persistido | `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln` com os 8 filtros declarados em [`1_task.md`](1_task.md) | `ReservationTests`, `ReservationSagaTests`, os 2 handlers, os 2 consumidores e as 2 classes de consumo | Nenhum — F02 e F03 estão disponíveis em `main` |

### Habilitadores inevitáveis

Nenhum. O vocabulário e a coluna de F02, o `UpdateAsync` de F03, a solution, o gate e as fixtures de
integração já existem. O registro de handlers em `ApplicationExtensions.cs` é parte do wiring da
própria V-01, não um habilitador horizontal: o `Dispatcher` atual resolve handlers explicitamente por
tipo e a fatia precisa desse registro para executar.

## Tarefas

- [x] 1.0 Fechar a Reservation Saga por autorização/rejeição: consumo, transição, publicação e proteção terminal (V-01)

## Rastreabilidade US → Tasks

| User Story | Tasks relacionadas | Tipo de cobertura |
|------------|--------------------|-------------------|
| Guest confirma automaticamente a Reservation após pagamento autorizado | 1.0 | Direta — RF-01/RN-07 |
| Guest recebe cancelamento automático com motivo quando o pagamento é rejeitado | 1.0 | Direta — RF-02/RN-08/DP-01 |
| Autor/arquiteto observa o fechamento da saga coreografada por eventos | 1.0 | Direta — consumo, persistência, logs e eventos finais |
| Catalog recebe confirmação somente após autorização e Notification recebe o resultado final | 1.0 | Direta via contrato AsyncAPI e payload publicado; consumidores downstream fora do escopo |

## Validação de Cobertura

### Requisitos Funcionais

| Requisito | Task(s) | Status |
|-----------|---------|--------|
| RF-01 — autorização correlacionada confirma Reservation e Saga | 1.0 | ✅ Coberto |
| RF-01 — autorização para Saga terminal não regride nem publica novamente | 1.0 | ✅ Coberto |
| RF-01 — autorização sem correlação conhecida é ignorada e logada | 1.0 | ✅ Coberto |
| RF-02 — rejeição correlacionada cancela Reservation, grava motivo e publica resultado | 1.0 | ✅ Coberto |
| RF-02 — rejeição duplicada, tardia ou conflitante não altera Saga terminal nem publica novamente | 1.0 | ✅ Coberto |
| RF-02 — rejeição sem correlação conhecida é ignorada e logada | 1.0 | ✅ Coberto |
| RF-03 — falha de publicação não desfaz a transição persistida e não relança exceção | 1.0 | ✅ Coberto |

### Regras e decisões herdadas

| Regra/decisão | Task | Evidência |
|---------------|------|-----------|
| RN-07/RN-08 — somente Payment decide o resultado; Booking reage | 1.0 | Consumidores só extraem correlação; handlers aplicam confirmação/cancelamento |
| RN-09/RN-10 — evento final habilita Catalog/Notification após estado terminal | 1.0 | Integração confere persistência antes do evento final e payload AsyncAPI |
| RN-11/DP-02 — verificar estado antes da transição, sem Outbox/dedup store | 1.0 | Testes terminal/duplicado/conflitante e ausência de segunda publicação |
| DP-01 — motivo fixo de negócio, sem copiar detalhe técnico de Payment | 1.0 | `PaymentRejectedConsumer` passa o texto aprovado ao comando; integração confere o payload |
| DP-03 — correlação inválida/desconhecida não interrompe processamento | 1.0 | Consumer não despacha GUID inválido; handler retorna `NotCorrelatable` sem efeitos |
| DP-04 — publicação final best-effort | 1.0 | Testes unitários simulam publisher com exceção e preservam estado/resultado |

### Artefatos da TechSpec

| Artefato | Tratamento na task | Status |
|----------|--------------------|--------|
| `Domain/Reservations/Reservation.cs` (`TerminalTransitionAt`, `Confirm(DateTime)`, `Cancel(string, DateTime)`, guarda de estado) | Modificar — EN-01/ADR-005 | ✅ |
| `Infra/Persistence/Configurations/ReservationConfiguration.cs` (`terminal_transition_at`) | Modificar — EN-01/ADR-005 | ✅ |
| `Infra/Migrations/*_AddTerminalTransitionAtToReservations` | Criar migration aditiva + constraint | ✅ |
| `Domain/Reservations/ReservationSaga.cs` (`MarkAuthorized`, `MarkRejected`) | Modificar; preservar `CancellationReason` de F02 | ✅ |
| `Domain/Reservations/SagaState.cs` (`Authorized`, `Rejected`) | Reutilizar o arquivo existente de F02; não alterar | ✅ Preexistente |
| `Infra/Persistence/Configurations/ReservationSagaConfiguration.cs` (`cancellation_reason`) | Reutilizar o mapeamento existente de F02; não alterar | ✅ Preexistente |
| `Infra/Migrations/*_AddSagaCancellationReason` | Reutilizar migration já aplicada/produzida por F02; não gerar outra | ✅ Preexistente |
| `Application/Reservations/IReservationConfirmedPublisher.cs` | Criar porta | ✅ |
| `Application/Reservations/IReservationCancelledPublisher.cs` | Criar porta | ✅ |
| `Application/Reservations/ConfirmReservationCommand.cs` e `ConfirmReservationCommandHandler.cs` | Criar comando, outcome e handler | ✅ |
| `Application/Reservations/CancelReservationCommand.cs` e `CancelReservationCommandHandler.cs` | Criar comando, outcome e handler | ✅ |
| `Application/Reservations/IReservationRepository.cs` (`GetByCorrelationIdAsync`) | Modificar; preservar `UpdateAsync` de F03 | ✅ |
| `Infra/Persistence/ReservationRepository.cs` (`Include` + `FirstOrDefaultAsync` por Saga.CorrelationId) | Modificar | ✅ |
| `Infra/Messaging/ReservationConfirmedRmqPublisher.cs` | Criar adapter/payload CloudEvents | ✅ |
| `Infra/Messaging/ReservationCancelledRmqPublisher.cs` | Criar adapter/payload CloudEvents | ✅ |
| `Api/Messaging/PaymentAuthorizedTopology.cs`, `PaymentAuthorizedMessage.cs`, `PaymentAuthorizedConsumer.cs` | Criar topologia, DTO defensivo e adapter | ✅ |
| `Api/Messaging/PaymentRejectedTopology.cs`, `PaymentRejectedMessage.cs`, `PaymentRejectedConsumer.cs` | Criar topologia, DTO defensivo e adapter | ✅ |
| `Api/Extensions/MessagingExtensions.cs` | Modificar exchanges, publishers e topic consumers | ✅ |
| `Api/Extensions/ApplicationExtensions.cs` | Modificar registro explícito dos 2 command handlers; lacuna necessária identificada na evidência do `Dispatcher` | ✅ |
| `tests/LocalizeStay.Booking.UnitTests/LocalizeStay.Booking.UnitTests.csproj` | Modificar com a referência ao `AwesomeAssertions` já pinada centralmente; lacuna de convenção de testes | ✅ |
| `UnitTests/Reservations/ReservationTests.cs` | Modificar com transições e guards | ✅ |
| `UnitTests/Reservations/ReservationSagaTests.cs` | Modificar com `MarkAuthorized`/`MarkRejected` e motivo exato | ✅ |
| `UnitTests/Reservations/ConfirmReservationCommandHandlerTests.cs` | Criar cenários RF-01/RF-03 | ✅ |
| `UnitTests/Reservations/CancelReservationCommandHandlerTests.cs` | Criar cenários RF-02/RF-03 | ✅ |
| `UnitTests/Messaging/PaymentAuthorizedConsumerTests.cs` | Criar cenários de dispatch e correlação inválida | ✅ |
| `UnitTests/Messaging/PaymentRejectedConsumerTests.cs` | Criar cenários de dispatch e correlação inválida | ✅ |
| `IntegrationTests/Reservations/PaymentAuthorizedConsumptionTests.cs` | Criar fluxo RabbitMQ/Postgres de autorização | ✅ |
| `IntegrationTests/Reservations/PaymentRejectedConsumptionTests.cs` | Criar fluxo RabbitMQ/Postgres de rejeição | ✅ |

### Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill relacionada | Status |
|---|-----------|---------------|-------------------|--------|
| 1 | Setup / Configuração | 1.0 — exchanges de entrada/saída, filas, consumers e DI; referência a `AwesomeAssertions` já centralmente versionada; sem pacote/versão nova | `dotnet-dependency-config` | ✅ |
| 2 | Modelos de Dados | 1.0 — usa estados/coluna de F02, consulta por `Saga.CorrelationId` e adiciona `terminal_transition_at` + constraint (EN-01/ADR-005, migration nova) | `dotnet-architecture`, `dotnet-dependency-config` | ✅ |
| 3 | Lógica de Negócio | 1.0 — transições, guards, outcomes e handlers | `dotnet-architecture` | ✅ |
| 4 | Endpoints / Interfaces | 1.0 — interfaces de publisher e contrato AsyncAPI; N/A para endpoint HTTP | `dotnet-architecture` | ✅ |
| 5 | Integrações Externas | 1.0 — RabbitMQ/CloudEvents para consumir e publicar | `dotnet-dependency-config` | ✅ |
| 6 | Validações e Erros | 1.0 — GUID defensivo, não correlação, estado terminal e falha best-effort | `dotnet-architecture` | ✅ |
| 7 | Testes | 1.0 — unitário focalizado e integração com Testcontainers | `dotnet-testing` | ✅ |
| 8 | Observabilidade | 1.0 — logs estruturados nos quatro pontos de cada caminho; métricas/tracing são os da biblioteca já adotada | — (baseline) | ✅ |
| 9 | Documentação | N/A — contrato AsyncAPI já aprovado; task referencia contrato, ADRs e decisões sem alterá-los | — | ✅ Justificado |
| 10 | Segurança | N/A — Fase 0 não tem autenticação; mensagens são tratadas como entrada não confiável e `correlationId` é validado | — (baseline) | ✅ Justificado |

### Coesão e Faixa de Tamanho

| Task | slice_type | Criar | Modificar | Subtarefas | Fatias | Faixa | Justificativa |
|------|------------|-------|-----------|------------|--------|-------|---------------|
| 1.0 | vertical | 21 | 11 | 5 | 1 | ⚠️ Acima do budget | A TechSpec e o PRD fecham confirmação, cancelamento, monotonicidade e falha best-effort como uma única V-01. Os 2 caminhos precisam compartilhar o repositório por correlação, o wiring RabbitMQ e a prova de que apenas o primeiro resultado publica; separar por camada ou por resultado deixaria estados/contratos sem gate independente. A nona modificação é apenas a referência ao `AwesomeAssertions` já pinada centralmente; as duas últimas são o mapeamento EF e o model snapshot exigidos pela migration `AddTerminalTransitionAtToReservations` (EN-01/ADR-005, emenda de alinhamento com F05). Complexidade `high` exige revisão do plano antes da execução. |

Task `vertical` tem exatamente uma fatia. O tamanho elevado não é um agrupamento de camadas por
conveniência: cada arquivo atravessa a mesma jornada observável e os testes são produzidos na task.

### Integridade dos Gates

| Task | Gate | Teste/fixture disponível | Filtro isolado | Repo compilável | Dependência futura | Status |
|------|------|--------------------------|-----------------|-----------------|--------------------|--------|
| 1.0 | `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln` + 8 filtros em `1_task.md` | Classes unitárias e de integração criadas na própria task; `CustomWebApplicationFactory`, coleção, Fake Catalog e Testcontainers já existem | Sim — um filtro por classe; o gate reprova seleção vazia | Sim — F02/F03 já estão em `main`; gate executa build da solution Booking | Não | ✅ Planejado |

O gate não usa `--skip-tests`: falha de seleção vazia, falha unitária ou falha de integração reprova a
task. A falha best-effort de publicação tem evidência unitária; não há teste de derrubar o broker no
meio da integração, conforme decisão aprovada na TechSpec.

Como a task é `high`, o plano permanece `Em revisão` e deve passar pelo validator focused no perfil
standard antes de qualquer execução; não existe uma revisão anterior de F04 para reutilizar.

### Ciclo de Vida de Artefatos Compartilhados

| Artefato | Primeira task produtora | Tasks consumidoras | Dependências consistentes | Status |
|----------|-------------------------|--------------------|----------------------------|--------|
| `Reservation.Confirm/Cancel` e `ReservationSaga.MarkAuthorized/MarkRejected` | 1.0 | handlers, testes unitários e integração | Sim | ✅ |
| `IReservationConfirmedPublisher` / `IReservationCancelledPublisher` | 1.0 | handlers, adapters Infra e testes | Sim | ✅ |
| `IReservationRepository.GetByCorrelationIdAsync` | 1.0 | 2 handlers e testes de handler/integração | Sim | ✅ |
| Topologias, mensagens e consumidores de Payment | 1.0 | wiring e testes unitários/integração | Sim | ✅ |
| Exchanges/eventos `booking.reservation_confirmed` e `booking.reservation_cancelled` | 1.0 | testes de integração; Catalog/Notification em features futuras | Sim | ✅ |
| `CustomWebApplicationFactory`, `BookingIntegrationTestCollection` e `FakeCatalogServerFactory` | Preexistentes F01/F02/F03 | testes de integração de 1.0 | Sim | ✅ Reutilizado |
| `Reservation.TerminalTransitionAt` + migration `AddTerminalTransitionAtToReservations` | 1.0 | testes de 1.0; `integration.reservation_calendar_v1` (F05, fora deste plano) | Sim | ✅ |

Não há contrato ou fixture produzida por task futura. A migration desta task é o único artefato
consumido por outra feature: F05 (`tasks/prd-publicacao-reservation-calendar`) depende de
`terminal_transition_at` para publicar `integration.reservation_calendar_v1`, não o inverso — F05 não
é dependência de compilação ou do gate de 1.0. Catalog/Notification consomem os eventos depois.

## Análise de Paralelização

Não há lane segura entre tasks: existe apenas a V-01, e o executor standard deve manter a ordem interna
Domain → Application/DI → Infrastructure → Api/Messaging → testes → gate. Os dois caminhos de Payment
podem ser implementados como simetria dentro da mesma task, mas compartilham `MessagingExtensions`,
`IReservationRepository`, broker e fixtures; isso não constitui paralelização do plano.

### Caminho Crítico

1. Reutilizar F02/F03 e adicionar as transições de domínio.
2. Adicionar portas, consulta por correlação, commands/handlers e registros explícitos no DI.
3. Adicionar publishers, repositório EF e topologias/consumidores `Rmq.CloudEvents`.
4. Provar cada caminho com testes unitários e integração ponta a ponta.
5. Executar o gate focalizado com build, format e todos os filtros não vazios.

### Diagrama de Dependências

```text
F02/F03 em main
      │
      ▼
1.0 V-01: Domain + Application + Infra + Api/Messaging + testes
      │
      ├── payment.payment_authorized ──→ confirmada ──→ booking.reservation_confirmed
      │                                      └─ duplicado/tardio/não correlacionável → ignorar + log
      │
      └── payment.payment_rejected ───→ cancelada ──→ booking.reservation_cancelled
                                             └─ duplicado/tardio/não correlacionável → ignorar + log
      │
      └── falha no publish final → estado terminal persiste; log; sem retry/Outbox nesta fase
```
