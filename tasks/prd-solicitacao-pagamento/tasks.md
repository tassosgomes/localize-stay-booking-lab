# Resumo de Tarefas de Implementação — Solicitação de Pagamento (Booking F03)

> **TechSpecs de origem:** [`techspec.md`](techspec.md) (rev. 2026-09-12, Status: Aprovado)
> **PRD de origem:** [`prd.md`](prd.md)
> **API Contract:** [`api-contract.yaml`](api-contract.yaml) (AsyncAPI 3.1, v1.0.0, Aprovado)
> **ADRs pertinentes:** [ADR-001: Stack .NET](../../docs/adr/adr-001-backend-stack-dotnet.md), [ADR-002: RabbitMQ](../../docs/adr/adr-002-broker-fase0-rabbitmq.md)
> **Status do plano:** Confirmado para implementação
> **Regra de entrega:** cada task de comportamento é uma fatia vertical validável isoladamente

## Visão Geral

Estende o `RequestReservationCommandHandler` de F01 (já mesclado em `main`) para, imediatamente após
persistir com sucesso uma `Reservation`/`ReservationSaga`, publicar `booking.payment_requested`
(`PaymentRequested.v1`) com o `correlationId` da Saga e o `totalAmount`/`currency` já congelados
(RN-06), e registrar em `ReservationSaga.PaymentRequestSentAt` que a solicitação foi enviada. Sem
command, endpoint ou entidade nova — é um segundo bloco best-effort no mesmo handler, seguindo
exatamente o padrão já usado por F01 para `booking.reservation_requested`. O PRD marca RF-01 como
feature única e indivisível (sucesso e falha de publicação só demonstram o objetivo de estudo da saga
coreografada quando implementados juntos) — por isso há uma única task cobrindo Domain, Application,
Infra, wiring e os dois níveis de teste.

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|-------|---------|------------|
| `dotnet-architecture` | `.claude/skills/dotnet-architecture` | Extensão do handler CQRS existente (sem novo command), porta de publicação `IPaymentRequestedPublisher` seguindo o mesmo padrão de `IReservationRequestedPublisher` |
| `dotnet-dependency-config` | `.claude/skills/dotnet-dependency-config` | Nova exchange declarada em `MessagingExtensions` via `Rmq.CloudEvents`, migration EF Core aditiva |
| `dotnet-testing` | `.claude/skills/dotnet-testing` | Unitário do handler estendido (mock do novo publisher) e do novo método de domínio; integração via fila real ligada à exchange `booking.payment_requested` (mesmo padrão de `ReservationEndpointTests`) |

Não foram lidas `dotnet-code-quality`, `dotnet-observability`, `dotnet-performance`, `restful-api` e
`design-patterns` — decisão já registrada e justificada na TechSpec (sem endpoint HTTP novo, sem
requisito de performance/observabilidade além do logging já padrão, sem variação de algoritmo/estado
que justifique um Design Pattern novo).

## Fases de Implementação

As fases agrupam uma sequência de comportamento e feedback, não uma camada arquitetural.

### Fase 1 — V-01: Solicitação de pagamento automática, com sucesso e falha best-effort

Única fase: publica `booking.payment_requested` com dados fiéis aos congelados, registra
`PaymentRequestSentAt` no sucesso, e uma falha de publicação não propaga exceção nem desfaz a
Reservation. Checkpoint: unitário do handler (sucesso + falha) e integração ponta a ponta contra
RabbitMQ real (Testcontainers), confirmando payload/headers na exchange e a coluna persistida.

## Mapa de Entrega e Feedback

| Slice | Task | Comportamento observável | Gate executável | Seletor focalizado | Bloqueado por |
|-------|------|---------------------------|-------------------|----------------------|----------------|
| V-01 | 1.0 | Toda `Reservation` criada com sucesso por F01 resulta em exatamente uma tentativa de publicação de `booking.payment_requested` fiel aos valores congelados; sucesso registra `PaymentRequestSentAt`; falha não propaga exceção nem registra o atributo | `scripts/ai-flow/gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationSagaTests" --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.RequestReservationCommandHandlerTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.PaymentRequestedPublishingTests"` | Classes `ReservationSagaTests`, `RequestReservationCommandHandlerTests` (2 cenários novos) e `PaymentRequestedPublishingTests` | Nenhum |

### Habilitadores inevitáveis

Nenhum. A migration aditiva (`payment_request_sent_at`) e a nova exchange são artefatos da própria
V-01 (TechSpec §Habilitadores inevitáveis) — não há segunda fatia nesta feature.

## Tarefas

- [ ] 1.0 Solicitação automática de pagamento: publisher, registro na Saga e testes (V-01)

## Rastreabilidade US → Tasks

| User Story | Tasks Relacionadas | Tipo de Cobertura |
|------------|---------------------|----------------------|
| Guest — reserva avança automaticamente para pagamento | 1.0 | Direta |
| Autor/arquiteto — observa a coreografia de saga via evento publicado | 1.0 | Direta |
| Payment — recebe valor/moeda/correlationId sem dados internos de Booking | 1.0 | Direta |

## Validação de Cobertura

### Requisitos Funcionais

| Requisito | Task(s) | Status |
|-----------|---------|--------|
| RF-01 (sucesso, falha de publicação, reserva rejeitada por F01) | 1.0 | ✅ Coberto |

### Artefatos da TechSpec

| Artefato | Task | Status |
|----------|------|--------|
| `Domain/Reservations/ReservationSaga.cs` (`PaymentRequestSentAt`, `MarkPaymentRequestSent`) | 1.0 | ✅ |
| `Application/Reservations/IPaymentRequestedPublisher.cs` | 1.0 | ✅ |
| `Application/Reservations/IReservationRepository.cs` (`+ UpdateAsync`) | 1.0 | ✅ |
| `Application/Reservations/RequestReservationCommandHandler.cs` (bloco best-effort novo) | 1.0 | ✅ |
| `Infra/Messaging/PaymentRequestedRmqPublisher.cs` + `PaymentRequestedTopology` | 1.0 | ✅ |
| `Infra/Persistence/ReservationRepository.cs` (`UpdateAsync`) | 1.0 | ✅ |
| `Infra/Persistence/Configurations/ReservationSagaConfiguration.cs` (coluna nova) | 1.0 | ✅ |
| `Infra/Migrations/*_AddPaymentRequestSentAtToReservationSagas.cs` | 1.0 | ✅ |
| `Api/Extensions/MessagingExtensions.cs` (exchange + DI) | 1.0 | ✅ |
| `UnitTests/Reservations/ReservationSagaTests.cs` | 1.0 | ✅ |
| `UnitTests/Reservations/RequestReservationCommandHandlerTests.cs` (+2 cenários) | 1.0 | ✅ |
| `IntegrationTests/Reservations/PaymentRequestedPublishingTests.cs` | 1.0 | ✅ |

### Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill Relacionada | Status |
|---|-----------|------------------|----------------------|--------|
| 1 | Setup / Configuração | 1.0 (exchange em `MessagingExtensions`, migration) | `dotnet-dependency-config` | ✅ |
| 2 | Modelos de Dados | 1.0 (`PaymentRequestSentAt`, coluna nova) | `dotnet-architecture` | ✅ |
| 3 | Lógica de Negócio | 1.0 (bloco best-effort do handler, `MarkPaymentRequestSent`) | `dotnet-architecture` | ✅ |
| 4 | Endpoints / Interfaces | N/A — sem endpoint HTTP novo (PRD, Restrições Técnicas) | — | ✅ |
| 5 | Integrações Externas | 1.0 (publicação RabbitMQ via `Rmq.CloudEvents`) | `dotnet-dependency-config` | ✅ |
| 6 | Validações e Erros | 1.0 (falha de publicação: log, sem exceção, sem retry — DP-02) | `dotnet-architecture` | ✅ |
| 7 | Testes | 1.0 (unitário + integração) | `dotnet-testing` | ✅ |
| 8 | Observabilidade | 1.0 (logging estruturado de sucesso/falha, mesmo padrão de F01) | — | ✅ |
| 9 | Documentação | N/A — nenhuma doc nova além do contrato já aprovado | — | ✅ |
| 10 | Segurança | N/A — Fase 0 não implementa autenticação/autorização (baseline) | — | ✅ |

### Coesão e Faixa de Tamanho

| Task | slice_type | Criar | Modificar | Subtarefas | Fatias | Faixa | Justificativa |
|------|------------|-------|-----------|------------|--------|-------|----------------|
| 1.0 | vertical | 5 | 7 | 6 | 1 | Acima do budget (criar 4-8 ok; modificar 7 > 4) | TechSpec declara RF-01 indivisível: sucesso e falha de publicação só provam o objetivo de estudo (coreografia de saga) juntos, e todos os arquivos modificados pertencem ao mesmo fluxo síncrono já existente de F01 (handler único, porta única, saga única). Fragmentar quebraria a compilação (ex.: `IReservationRepository.UpdateAsync` sem chamador) ou o gate comportamental (handler parcialmente estendido não é testável ponta a ponta). Complexidade marcada `high` — mesmo padrão já aceito em `prd-solicitacao-reserva/3_task.md` (acoplamento irredutível ao handler único do domínio). |

Task `vertical` com exatamente uma fatia (V-01), conforme exigido. `high` é exceção documentada, não
grosseria de fragmentação — reflete o próprio "Habilitadores inevitáveis: Nenhum" da TechSpec.

### Integridade dos Gates

| Task | Gate | Teste/fixture disponível | Filtro isolado | Repo compilável | Dependência futura | Status |
|------|------|------------------------------|-------------------|--------------------|------------------------|--------|
| 1.0 | `scripts/ai-flow/gate.sh --filter="...ReservationSagaTests" --filter="...RequestReservationCommandHandlerTests" --filter="...PaymentRequestedPublishingTests"` | Criado nesta própria task (as 3 classes) | Sim | Sim | Não | ✅ |

### Ciclo de Vida de Artefatos Compartilhados

| Artefato | Primeira task produtora | Tasks consumidoras | Dependências consistentes | Status |
|----------|-----------------------------|------------------------|---------------------------------|--------|
| `IPaymentRequestedPublisher` / `PaymentRequestedRmqPublisher` | 1.0 | 1.0 (handler, testes) | Sim | ✅ |
| `ReservationSaga.PaymentRequestSentAt` / `MarkPaymentRequestSent` | 1.0 | 1.0 (handler, testes, configuração EF) | Sim | ✅ |
| `IReservationRepository.UpdateAsync` | 1.0 | 1.0 (handler, testes) | Sim | ✅ |

Nenhuma task futura desta feature depende de artefato produzido depois — é a única task do plano.

## Análise de Paralelização

Não aplicável: única task do plano, sem oportunidade de paralelização interna (TechSpec §Sequenciamento
de Desenvolvimento define uma ordem interna Domain → Infra → Application → Wiring/Testes dentro da
mesma fatia, não fatias independentes).
