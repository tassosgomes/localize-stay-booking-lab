# TechSpec: Solicitação de Pagamento (Booking F03)

> **Modo de operação:** API-First (contrato AsyncAPI — sem endpoint HTTP novo)
> **PRD de origem:** `tasks/prd-solicitacao-pagamento/prd.md`
> **API Contract:** `tasks/prd-solicitacao-pagamento/api-contract.yaml` (AsyncAPI 3.1, v1.0.0, status "Aprovado")
> **Data:** 2026-09-12
> **Status:** Em Revisão
> **Handoff:** draft — não gerar Tasks ainda

---

## Resumo Executivo

Esta TechSpec estende o fluxo síncrono já implementado por F01 (`RequestReservationCommandHandler`,
branch `feature/prd-solicitacao-reserva`, ainda não mesclada em `main`) para, imediatamente após
persistir com sucesso uma `Reservation`/`ReservationSaga`, publicar `booking.payment_requested`
(`PaymentRequested.v1`) com o `correlationId` da saga e o valor total/moeda já congelados (RN-06), e
registrar no próprio `ReservationSaga` que essa solicitação foi enviada. Não há command, endpoint ou
caso de uso novo: DP-01 exige que a publicação seja automática, sem nenhum gatilho externo, então a
implementação mais simples e mais fiel ao baseline ("saga coreografada, Booking dono do fluxo") é
adicionar um segundo passo best-effort ao handler que já existe, seguindo exatamente o mesmo padrão
já usado por F01 para `booking.reservation_requested` (porta de publicação + adapter `Rmq.CloudEvents`
+ falha logada sem exceção).

O registro de "solicitação enviada" não é um novo valor de `SagaState` (esse enum continua
representando apenas a situação observável da saga exposta por F02: pendente/autorizado/rejeitado,
conforme `tasks/prd-consulta-reserva/techspec.draft.md`) — é um atributo interno e independente
(`ReservationSaga.PaymentRequestSentAt`, nullable), coerente com o Termo Canônico do PRD ("não é um
estado adicional exposto na consulta").

**Trade-off primário:** a publicação de `booking.payment_requested` e a atualização do
`PaymentRequestSentAt` são best-effort, não transacionais com a criação da `Reservation` (que já
aconteceu num `SaveChangesAsync` anterior) nem entre si — se a publicação falhar, nada é revertido, o
log registra a falha, e a `Reservation` fica presa em `solicitada` indefinidamente (DP-02, risco já
aceito e documentado em `domains/booking/domain.md` §8). Em troca, esta feature não introduz nenhuma
peça de infraestrutura nova (sem Outbox, sem tabela de tentativas) para um problema que a Fase 0
decidiu explicitamente não resolver agora (F06).

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|-------|---------|------------------------|
| `dotnet-architecture` | `.claude/skills/dotnet-architecture` | Extensão do handler CQRS existente (sem novo command), porta de publicação (`IPaymentRequestedPublisher`) seguindo o mesmo padrão de `IReservationRequestedPublisher` |
| `dotnet-dependency-config` | `.claude/skills/dotnet-dependency-config` | Nova exchange declarada em `MessagingExtensions` via `Rmq.CloudEvents` (já configurado por F01/fundação), migration EF Core aditiva |
| `dotnet-testing` | `.claude/skills/dotnet-testing` | Unitário do handler estendido (mock do novo publisher) e do novo método de domínio; integração via fila real ligada à exchange `booking.payment_requested` (mesmo padrão de `ReservationEndpointTests`) |

Não foram lidas `dotnet-code-quality`, `dotnet-observability`, `dotnet-performance`, `restful-api` e
`design-patterns`: esta feature não introduz endpoint HTTP, não tem requisito de performance/observabilidade
além do logging estruturado já padrão da fundação, e não há variação de algoritmo/estado que justifique
um Design Pattern novo (é uma extensão direta e pequena de um fluxo já decidido).

---

## Arquitetura do Sistema

### Visão Geral dos Componentes

- **`LocalizeStay.Booking.Domain` (estendido):** `ReservationSaga` ganha o atributo
  `PaymentRequestSentAt` (nullable `DateTime`) e o método `MarkPaymentRequestSent(DateTime occurredAt)`
  — nenhuma outra entidade nova. `SagaState` **não é alterado** por esta feature.
- **`LocalizeStay.Booking.Application` (estendido):** nova porta `IPaymentRequestedPublisher`; nova
  operação `IReservationRepository.UpdateAsync` para persistir a mutação da Saga; o
  `RequestReservationCommandHandler` já existente (F01) ganha um segundo bloco best-effort, análogo ao
  que já existe para `booking.reservation_requested`.
- **`LocalizeStay.Booking.Infra` (estendido):** `PaymentRequestedRmqPublisher` (adapta `IRmqPublisher`
  do `Rmq.CloudEvents`, já configurado pela fundação/F01) implementando `IPaymentRequestedPublisher`;
  nova exchange `booking.payment_requested` registrada em `MessagingExtensions`; migration EF Core
  aditiva em `booking.reservation_sagas`; `ReservationRepository.UpdateAsync`.
- **Nenhum componente de Payment é criado ou modificado aqui** — o lado de consumo é decisão da
  própria feature de Payment que reagirá a este evento (Questões em Aberto do `api-contract.md`).

### Diagrama de Componentes

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ RequestReservationCommandHandler (F01, já existente — estendido aqui)     │
│  1..5. [inalterado] valida → Catalog → Reservation.Create → AddAsync      │
│  6. [F01] publisher.PublishAsync(reservation)  → best-effort, já existia  │
│     → booking.reservation_requested                                       │
│  7. [F03 — NOVO] paymentRequestedPublisher.PublishAsync(reservation)      │
│     → booking.payment_requested (correlationId=causationId=Saga.CorrId,  │
│       totalAmount/currency congelados)                                    │
│     ├─ sucesso → reservation.Saga.MarkPaymentRequestSent(utcNow)          │
│     │            → reservationRepository.UpdateAsync(reservation)         │
│     └─ falha    → log de erro, sem exceção, sem retry (DP-02)             │
└──────────────────────────────────────────────────┬───────────────────────┘
                                                     ▼
                                     RabbitMQ vhost /localize-stay
                                     exchange booking.payment_requested (topic)
                                     ← consumidor: Payment (fora do escopo)
```

---

## Estratégia de Entrega Incremental

### Mapa de Fatias Verticais

O PRD marca RF-01 como feature única e indivisível (o objetivo de estudo só se demonstra com sucesso
e falha de publicação juntos) — por isso há uma única fatia vertical, cobrindo todos os cenários do AC.

| Slice | Comportamento observável | RF/RN cobertos | Entrada → processamento → saída | Artefatos principais | Evidência / checkpoint | Bloqueado por |
|-------|--------------------------|-----------------|----------------------------------|-----------------------|--------------------------|----------------|
| V-01 | Toda `Reservation` criada com sucesso por F01 resulta em exatamente uma tentativa de publicação de `booking.payment_requested` com `correlationId`/`totalAmount`/`currency` fiéis aos congelados; o `ReservationSaga` registra `PaymentRequestSentAt` quando a publicação é bem-sucedida; uma falha de publicação não lança exceção, não desfaz a `Reservation` e não grava `PaymentRequestSentAt` | RF-01 (3 cenários do AC), RN-06 | `RequestReservationCommandHandler` (já persistiu `Reservation`/`Saga` via F01) → `IPaymentRequestedPublisher.PublishAsync` → sucesso: `ReservationSaga.MarkPaymentRequestSent` + `IReservationRepository.UpdateAsync`; falha: log, sem re-throw | `Domain/Reservations/ReservationSaga.cs` (estendido); `Application/Reservations/{IPaymentRequestedPublisher.cs,IReservationRepository.cs (estendido),RequestReservationCommandHandler.cs (estendido)}`; `Infra/Messaging/PaymentRequestedRmqPublisher.cs`; `Infra/Persistence/{ReservationRepository.cs (estendido),Configurations/ReservationSagaConfiguration.cs (estendido)}`; migration `AddPaymentRequestSentAtToReservationSagas` | `dotnet test --filter FullyQualifiedName~Booking.UnitTests.Reservations.RequestReservationCommandHandlerTests` cobrindo os 2 cenários novos (sucesso publica + registra; falha não registra e não propaga); `dotnet test --filter FullyQualifiedName~Booking.IntegrationTests.Reservations.PaymentRequestedPublishingTests` verificando a mensagem real na exchange `booking.payment_requested` (Testcontainers RabbitMQ) e a coluna `payment_request_sent_at` persistida | F01 mesclada/disponível no ambiente de implementação (`feature/prd-solicitacao-reserva`) |

O terceiro cenário do AC de RF-01 (reserva rejeitada por F01 — nenhuma `Reservation`/`Saga` criada) já
é garantido pela estrutura do próprio handler: o novo bloco só executa depois do `AddAsync` bem-sucedido,
então nenhuma rejeição de F01 alcança este código — não precisa de teste dedicado além dos já existentes
de F01 que comprovam que `AddAsync`/`PublishAsync` (reservation_requested) não rodam em rejeição.

### Habilitadores inevitáveis

Nenhum. A migration aditiva (`payment_request_sent_at`) e a nova exchange são artefatos da própria
V-01, não desbloqueiam nada além dela — não há segunda fatia nesta feature.

---

## Design de Implementação

### Interfaces Principais

```csharp
// Application — nova porta de publicação (F03), mesmo padrão de IReservationRequestedPublisher
public interface IPaymentRequestedPublisher
{
    Task PublishAsync(Reservation reservation, CancellationToken cancellationToken);
}

// Application — extensão da porta de persistência já existente (F01)
public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);

    // Novo (F03): persiste mutações da Reservation/Saga já carregadas no
    // mesmo DbContext (ex.: ReservationSaga.MarkPaymentRequestSent).
    Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken);
}
```

```csharp
// Domain — ReservationSaga (F01, estendido nesta feature)
public sealed class ReservationSaga
{
    // ... construtor e demais propriedades inalterados (F01) ...

    public DateTime? PaymentRequestSentAt { get; private set; }

    // Registro interno de que booking.payment_requested foi publicado com
    // sucesso para esta saga (Termo Canônico do PRD) — não é uma transição
    // de SagaState nem é exposto por F02.
    public void MarkPaymentRequestSent(DateTime occurredAt)
    {
        PaymentRequestSentAt = occurredAt;
    }
}
```

```csharp
// Application — RequestReservationCommandHandler (F01), trecho estendido por F03
// (mantém a estrutura, injeções e log já existentes; adiciona apenas o bloco abaixo,
// logo depois do bloco best-effort já existente de reservation_requested)
try
{
    await paymentRequestedPublisher.PublishAsync(reservation, cancellationToken).ConfigureAwait(false);

    reservation.Saga.MarkPaymentRequestSent(DateTime.UtcNow);
    await reservationRepository.UpdateAsync(reservation, cancellationToken).ConfigureAwait(false);

    logger.LogInformation(
        "Evento booking.payment_requested publicado para a Reservation {ReservationId} " +
        "(correlationId={CorrelationId})", reservation.Id, reservation.Saga.CorrelationId);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    // Best-effort, sem outbox nesta fase (DP-02): a falha não desfaz a
    // Reservation nem o registro de reservation_requested já feitos, e não
    // aciona nenhuma nova tentativa automática.
    logger.LogError(
        ex,
        "Falha best-effort ao publicar booking.payment_requested da Reservation {ReservationId} " +
        "(correlationId={CorrelationId}); ReservationSaga permanece sem registro de solicitação enviada",
        reservation.Id, reservation.Saga.CorrelationId);
}
```

### Modelos de Dados

**Mapeamento Entidade do Domain Doc → Modelo Técnico:**

| Entidade (`domains/booking/domain.md` §3) | Modelo Técnico | Local |
|---|---|---|
| Reservation Saga — atributo "solicitação de pagamento" | `ReservationSaga.PaymentRequestSentAt` (nullable `DateTime`, novo nesta feature) | `Domain/Reservations/ReservationSaga.cs` → coluna `booking.reservation_sagas.payment_request_sent_at` |

Nenhuma tabela nova. Alteração aditiva em `booking.reservation_sagas` (já criada por F01):

| Coluna (nova) | Tipo | Observação |
|---|---|---|
| `payment_request_sent_at` | `timestamptz`, nullable | `NULL` até a primeira (e única) publicação bem-sucedida de `booking.payment_requested`; preenchido pela aplicação (`DateTime.UtcNow`), nunca pelo banco |

Não há alteração em `booking.reservations` nem em `SagaState` (permanece só `PaymentPending` nesta
feature — `Authorized`/`Rejected` são responsabilidade do draft de F02/F04, referenciado, não
redefinido aqui).

### Endpoints de API

Não aplicável — esta feature não introduz nem modifica nenhum endpoint HTTP (Restrições Técnicas do
PRD). O único artefato de contrato é o evento AsyncAPI abaixo.

**Mapeamento de implementação do contrato (AsyncAPI):**

| Operação do contrato | Caminho de Implementação |
|-------------|--------------------------|
| `publishPaymentRequested` (`tasks/prd-solicitacao-pagamento/api-contract.yaml`) | `RequestReservationCommandHandler` (trecho novo acima) → `IPaymentRequestedPublisher` → `PaymentRequestedRmqPublisher` (`Infra/Messaging`) → exchange `booking.payment_requested` |

**Mapeamento de payload do contrato → publisher:**

| Campo do contrato | Origem no código |
|---|---|
| `correlationId` | `reservation.Saga.CorrelationId` |
| `causationId` | `reservation.Saga.CorrelationId` (igual a `correlationId` — evento autocausado, mesma convenção de F01 para `reservation_requested`) |
| `totalAmount` | `reservation.TotalAmount.ToString("F2", CultureInfo.InvariantCulture)` — contrato exige **string** decimal de 2 casas (diferente de `reservation_requested`, cujo payload de evento mantém `decimal` numérico; aqui o contrato AsyncAPI já aprovado fixa `string`, então a formatação acontece no próprio record do publisher, não via `JsonConverter` da Api layer) |
| `currency` | `reservation.Currency` |
| `requestedAt` | `DateTimeOffset.UtcNow` no momento da publicação |
| headers `x-correlation-id`/`x-causation-id` | mesmo valor de `correlationId`/`causationId`, como string |

Nenhuma exceção de domínio nova nem mapeamento HTTP — esta feature não tem caminho de rejeição
observável via API (a única "rejeição" é a falha de publicação, tratada como log, não como erro
retornado a ninguém, já que não há chamador síncrono esperando resposta desta etapa).

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Fatia | Tipo | Skills Aplicáveis | Descrição |
|---------|-------|------|-------------------|-----------|
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IPaymentRequestedPublisher.cs` | V-01 | Port | `dotnet-architecture` | Porta de publicação de `booking.payment_requested` |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/PaymentRequestedRmqPublisher.cs` | V-01 | Infra | `dotnet-dependency-config` | Implementa a porta via `Rmq.CloudEvents`; declara `PaymentRequestedTopology` (`Exchange`/`RoutingKey` = `booking.payment_requested`, `CloudEventType` = `com.localizestay.booking.payment_requested.v1`, conforme `api-contract.yaml`) |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Migrations/*_AddPaymentRequestSentAtToReservationSagas.cs` | V-01 | Migration | `dotnet-dependency-config` | Adiciona `payment_request_sent_at timestamptz null` em `booking.reservation_sagas` |
| `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/ReservationSagaTests.cs` | V-01 | Test | `dotnet-testing` | `MarkPaymentRequestSent` grava o instante recebido |
| `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/PaymentRequestedPublishingTests.cs` | V-01 | Test | `dotnet-testing` | Fila real ligada a `booking.payment_requested` (Testcontainers RabbitMQ, mesmo padrão de `ReservationEndpointTests`); confirma payload/headers e `payment_request_sent_at` persistido |

### Arquivos a Modificar

| Caminho | Fatia | Skills Aplicáveis | Alteração |
|---------|-------|-------------------|-----------|
| `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/ReservationSaga.cs` | V-01 | `dotnet-architecture` | `+ PaymentRequestSentAt` (nullable `DateTime`), `+ MarkPaymentRequestSent(DateTime)` |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationRepository.cs` | V-01 | `dotnet-architecture` | `+ UpdateAsync(Reservation, CancellationToken)` |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/RequestReservationCommandHandler.cs` | V-01 | `dotnet-architecture` | Injeta `IPaymentRequestedPublisher`; adiciona o bloco best-effort de publicação + registro (trecho no Design de Implementação), logo após o bloco já existente de `reservation_requested` |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/ReservationRepository.cs` | V-01 | `dotnet-dependency-config` | Implementa `UpdateAsync` (`dbContext.Update(reservation)` + `SaveChangesAsync`) |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/ReservationSagaConfiguration.cs` | V-01 | `dotnet-dependency-config` | Mapeia `PaymentRequestSentAt` → coluna `payment_request_sent_at` (`timestamptz`, nullable) |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/MessagingExtensions.cs` | V-01 | `dotnet-dependency-config` | Registra `options.Exchanges[PaymentRequestedTopology.Exchange]` (topic, durable) e `services.AddScoped<IPaymentRequestedPublisher, PaymentRequestedRmqPublisher>()` |
| `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/RequestReservationCommandHandlerTests.cs` | V-01 | `dotnet-testing` | `+2` cenários: publicação de `payment_requested` bem-sucedida registra `PaymentRequestSentAt` via `UpdateAsync`; falha de publicação não registra e não propaga exceção (mesmo padrão do teste já existente `HandleAsync_when_publisher_fails_returns_reservation_anyway_best_effort`, agora para o novo publisher) |

### Arquivos de Referência (não alterar)

| Caminho | Motivo da Consulta |
|---------|-------------------|
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/{RequestReservationCommandHandler,IReservationRequestedPublisher}.cs` (branch `feature/prd-solicitacao-reserva`) | Padrão exato a replicar para a nova porta/publisher e para o segundo bloco best-effort do handler |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Messaging/ReservationRequestedRmqPublisher.cs` (idem) | Modelo do adapter `Rmq.CloudEvents` (exchange/routing key/cloudEventType, headers de correlação) |
| `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationEndpointTests.cs` (idem) | Padrão de teste de integração que liga fila real à exchange e lê a mensagem publicada via `RabbitMQ.Client` |
| `tasks/prd-solicitacao-pagamento/api-contract.yaml` | Fonte única do schema do evento, exchange/routing key, headers e envelope CloudEvents |
| `domains/booking/domain.md` | RN-06; F03 no roadmap; eventos produzidos/consumidos |
| `tasks/prd-consulta-reserva/techspec.draft.md` | Confirma que `SagaState` é vocabulário à parte de `PaymentRequestSentAt` — evita colisão com a extensão de `Authorized`/`Rejected` planejada por F02 |

---

## Pontos de Integração

- **RabbitMQ (`ecad-dev-rabbitmq`, vhost `/localize-stay`):** publica `booking.payment_requested` na
  exchange topic `booking.payment_requested` (durable), routing key `booking.payment_requested` —
  nomes idênticos ao já aprovado em `api-contract.yaml`. Reaproveita a mesma instância de
  `Rmq.CloudEvents` já configurada por F01/fundação (`MessagingExtensions`); apenas registra mais uma
  exchange na topologia idempotente do boot, ao lado de `booking.reservation-events` e
  `diagnostics.topic`. Sem binding de fila de consumo declarado aqui — é decisão da feature de Payment
  (Questões em Aberto do `api-contract.md`).
- **Sem chamada síncrona:** esta feature não depende de nenhum serviço externo síncrono; a única
  dependência de infraestrutura é o broker já provisionado.

---

## Análise de Impacto

| Componente Afetado | Tipo de Impacto | Descrição & Risco | Ação Requerida |
|--------------------|-----------------|-------------------|-----------------|
| `booking.reservation_sagas` (Postgres) | Modificado (aditivo) | Nova coluna nullable, sem afetar linhas existentes; risco baixo | Migration na pipeline de deploy do serviço, não no boot (convenção já da fundação) |
| Vhost `/localize-stay` (RabbitMQ) | Modificado | Nova exchange `booking.payment_requested` ao lado das já existentes; risco baixo, topologia declarada idempotente no boot | Nenhuma ação manual |
| `RequestReservationCommandHandler` (F01) | Modificado | Adiciona um segundo bloco best-effort após o já existente; mesma forma, mesmo tratamento de exceção — risco baixo de regressão nos cenários já cobertos por F01, desde que o bloco novo não altere o `return reservation` existente nem capture exceções fora do próprio `try` | Rodar toda a suíte de `RequestReservationCommandHandlerTests`/`ReservationEndpointTests` de F01 após a mudança, não só os testes novos |
| `IReservationRepository` (porta) | Modificado (aditivo) | Novo método `UpdateAsync`; qualquer implementação alternativa futura (hoje só há uma) precisa implementá-lo | Nenhuma ação além da implementação em `ReservationRepository` |
| Payment (domínio) | Nenhum nesta TechSpec | Passa a ter uma exchange real de onde consumir; o binding de fila é responsabilidade da própria feature de Payment | Nenhuma ação agora — já registrado como pendência em `api-contract.md` |
| Booking F04 (Conclusão da Saga, futura) | Nenhum nesta TechSpec | Reutilizará `IReservationRepository.UpdateAsync` para persistir a confirmação/cancelamento da `Reservation`, em vez de introduzir um método próprio | Nenhuma ação agora — registrar como referência quando F04 for especificada |

---

## Abordagem de Testes

### Testes Unitários

- `ReservationSagaTests.MarkPaymentRequestSent_sets_the_received_instant`: chamada única grava
  `PaymentRequestSentAt` exatamente com o valor recebido.
- `RequestReservationCommandHandlerTests` (estendido, mocks de `IPaymentRequestedPublisher` além dos
  já existentes):
  - Sucesso: `PublishAsync` do novo publisher é chamado com a `Reservation` criada; após sucesso,
    `reservation.Saga.PaymentRequestSentAt` não é mais `null` e `IReservationRepository.UpdateAsync` é
    chamado exatamente uma vez.
  - Falha: `IPaymentRequestedPublisher.PublishAsync` lança `InvalidOperationException` → o handler
    ainda retorna a `Reservation` normalmente (sem lançar), `PaymentRequestSentAt` permanece `null`, e
    `UpdateAsync` **não** é chamado (mesmo padrão do teste já existente para o publisher de
    `reservation_requested`).
  - Todos os cenários de rejeição de F01 já existentes (período/hóspedes/Catalog/capacidade/etc.)
    continuam verdes sem modificação — nenhum deles chega ao novo bloco, então nenhuma asserção nova é
    necessária ali além de garantir que o mock de `IPaymentRequestedPublisher` nunca é invocado nesses
    casos.

### Testes de Integração

- **`PaymentRequestedPublishingTests` (novo, mesma coleção/fixture de `ReservationEndpointTests`):**
  usa `CustomWebApplicationFactory` (Postgres + RabbitMQ Testcontainers já existentes) para criar uma
  Reservation válida via `POST /v1/reservations`, liga uma fila real à exchange
  `booking.payment_requested` (routing key `booking.payment_requested`, mesmo mecanismo de
  `RabbitMQ.Client` já usado por `BindReservationEventsQueueAsync`), consome a mensagem publicada e
  confere: `correlationId`/`causationId` == id da Reservation criada, `totalAmount` é string decimal de
  2 casas idêntica ao `totalAmount` retornado pela resposta HTTP, `currency` == `"BRL"`, headers AMQP
  `x-correlation-id`/`x-causation-id` presentes e corretos, `cloudEventType` ==
  `com.localizestay.booking.payment_requested.v1`. Em seguida, lê `booking.reservation_sagas` via
  `BookingDbContext` (mesmo padrão de `ReservationPersistenceTests`) e confirma
  `payment_request_sent_at` preenchido.
- Não há teste de integração dedicado ao cenário de falha de publicação (broker indisponível) — testar
  isso contra um Testcontainer real exigiria derrubar o broker no meio do teste, complexidade
  desproporcional ao valor já coberto pelo teste unitário do handler (mesma decisão implícita de F01,
  que também só cobre a falha de `reservation_requested` no nível unitário).

### Testes de Contrato

- Não aplicável neste ciclo: o `api-contract.yaml` já foi validado via Spectral na etapa de contrato
  (`api-contract.md` §Validação, 0 erros). Não há ferramenta de contrato para AsyncAPI equivalente ao
  Dredd usado em `api-contract.yaml` de F01 (REST); a garantia de fidelidade ao schema do evento vem do
  teste de integração acima, que lê a mensagem real publicada e compara campo a campo com os exemplos
  do contrato.

---

## Sequenciamento de Desenvolvimento

### Build Order

1. **Domain** (`ReservationSaga.PaymentRequestSentAt`/`MarkPaymentRequestSent`) — sem dependências além
   do próprio F01 já mesclado/disponível.
2. **Infra** (`PaymentRequestedRmqPublisher`, migration, `ReservationSagaConfiguration`,
   `ReservationRepository.UpdateAsync`) — depende de 1.
3. **Application** (`IPaymentRequestedPublisher`, `IReservationRepository.UpdateAsync`, extensão do
   `RequestReservationCommandHandler`) — depende de 1 e 2.
4. **Wiring** (`MessagingExtensions`) e testes (unitários + integração) — depende de 1, 2 e 3.

Como há uma única fatia vertical, esta ordem é interna a V-01, não uma sequência de fatias
independentes.

### Dependências Técnicas Bloqueantes

- **F01 (`tasks/prd-solicitacao-reserva`) precisa estar disponível no ambiente onde esta feature será
  implementada** — hoje ela existe apenas na branch `feature/prd-solicitacao-reserva`, ainda não
  mesclada em `main`. Esta TechSpec modifica diretamente `RequestReservationCommandHandler.cs`,
  `ReservationSaga.cs`, `IReservationRepository.cs` e `ReservationSagaConfiguration.cs` de F01 — sem
  esses arquivos existentes, não há o que estender. Mesma natureza de dependência que a TechSpec de F01
  já registrou para a fundação técnica da Fase 0.
- Nenhuma dependência de Catalog ou Payment: esta feature não chama nenhum dos dois sincronamente, e o
  lado de consumo do evento (Payment) é responsabilidade de uma feature própria ainda não especificada.

---

## Monitoramento e Observabilidade

- Logging estruturado (mesmo padrão de F01) em dois pontos: sucesso da publicação de
  `booking.payment_requested` (com `ReservationId`/`CorrelationId`) e falha best-effort (nível `Error`,
  com a exceção original e os mesmos identificadores) — permite localizar manualmente, via log, toda
  `Reservation` presa em `solicitada` por falha de publicação (mitigação já prevista no PRD para o
  risco de DP-02).
- Nenhuma métrica/tracing formal nesta fase, conforme baseline (igual a F01).

---

## Considerações Técnicas

### Decisões Principais

- **Decisão:** estender diretamente `RequestReservationCommandHandler` (F01) com um segundo bloco
  best-effort, em vez de introduzir um mecanismo de evento de domínio interno (ex.: domain
  events + handler separado) para desacoplar F01 de F03.
  **Racional:** DP-01 exige publicação automática, na mesma sequência lógica da criação, sem gatilho
  externo; o projeto já decidiu não usar MediatR nem um barramento de eventos internos
  (`dotnet-architecture` — CQRS nativo). Introduzir um mecanismo de eventos de domínio só para
  desacoplar duas linhas de código dentro do mesmo caso de uso seria uma abstração sem consumidor
  real hoje (Booking é o único domínio nesta fase que reage a "Reservation criada").
  **Trade-offs:** `RequestReservationCommandHandler` acumula responsabilidades de duas features (F01 e
  F03) no mesmo arquivo; aceitável enquanto o handler continuar pequeno e cada bloco continuar
  independente (falha de um não afeta o outro).
  **Alternativas rejeitadas:** publicar `payment_requested` a partir de um consumidor interno do
  próprio `booking.reservation_requested` (Booking publicando e consumindo seu próprio evento) —
  rejeitada por adicionar uma volta desnecessária pelo broker para uma decisão 100% síncrona ao próprio
  processo, sem nenhum ganho de desacoplamento real nesta fase.

- **Decisão:** `PaymentRequestSentAt` é um atributo simples (`DateTime?`) no `ReservationSaga`, não uma
  extensão de `SagaState`.
  **Racional:** Termo Canônico do PRD é explícito — "não é um estado adicional exposto na consulta"; o
  draft de F02 já planeja estender `SagaState` com `Authorized`/`Rejected` para representar a situação
  observável da saga. Misturar um flag de auditoria interna no mesmo enum que representa o resultado
  do pagamento confundiria dois conceitos distintos (o que a saga sabe internamente vs. o que é
  exposto).
  **Trade-offs:** nenhum significativo.

- **Decisão:** `IReservationRepository.UpdateAsync` chama `dbContext.Update(reservation)` antes de
  `SaveChangesAsync`, mesmo a entidade já estando rastreada pelo mesmo `DbContext` desde o `AddAsync`
  anterior na mesma requisição.
  **Racional:** `Update()` explícito deixa a intenção clara e mantém o método correto mesmo se um
  chamador futuro (ex.: F04, atualizando `Reservation.Status`) o invocar a partir de uma instância
  carregada em outro momento/contexto — não é uma otimização prematura, é a forma convencional do
  padrão Repository.
  **Trade-offs:** gera um `UPDATE` com todas as colunas de `reservations`/`reservation_sagas`, não só
  `payment_request_sent_at` (EF Core marca a árvore inteira como `Modified` ao chamar `Update()`
  explicitamente). Aceitável nesta escala de laboratório; revisitar se algum dia houver requisito real
  de performance de escrita.

### Riscos Conhecidos

- **Falha de publicação deixa a Reservation presa em `solicitada` indefinidamente:** já aceito e
  documentado (DP-02, `domains/booking/domain.md` §8); resolvido apenas por F06 (Outbox/retry).
- **Publicação de `reservation_requested` bem-sucedida e de `payment_requested` com falha (ou
  vice-versa) são possíveis e independentes:** aceitável porque nenhuma das duas tem consumidor
  obrigatório nesta fase que dependa da outra ter ocorrido — `payment_requested` carrega tudo que
  Payment precisa por si só, sem depender de `reservation_requested` ter sido recebido por ninguém.
- **`RequestReservationCommandHandler` cresce a cada feature de Booking que reage à criação da
  Reservation:** aceito nesta fase (F01 + F03); se uma terceira reação automática aparecer no futuro
  (fora do escopo hoje), vale reconsiderar um mecanismo de despacho de eventos internos — não
  antecipado agora por não haver um terceiro caso real.

### Requisitos Especiais

Não aplicável — sem requisito de performance, segurança adicional (Fase 0 não implementa autenticação)
ou conformidade regulatória.

### Conformidade com Skills

- Segue `dotnet-architecture` (Clean Architecture, sem introduzir MediatR/barramento de eventos,
  porta+adapter para a nova integração assíncrona).
- Segue `dotnet-dependency-config` (EF Core com `IEntityTypeConfiguration` já existente apenas
  estendido, `Rmq.CloudEvents` reaproveitado sem nova dependência de pacote).
- Segue `dotnet-testing` (unitário para a regra/orquestração, integração com Testcontainers reais para
  o broker e o banco).

**Desvios identificados:** nenhum.

---

## Questões em Aberto

- [ ] Nome exato do arquivo/timestamp da migration (`*_AddPaymentRequestSentAtToReservationSagas.cs`)
  fica para a implementação, seguindo a convenção já usada por F01.
- [ ] Confirmar, no momento da implementação, que a branch `feature/prd-solicitacao-reserva` (F01)
  está mesclada ou disponível no ambiente de trabalho onde esta feature será implementada — sem isso,
  os arquivos listados em "Arquivos a Modificar" não existem ainda.
- Nenhum conflito identificado com o API Contract (`api-contract.yaml`, já "Aprovado") — esta TechSpec
  não propõe nenhuma mudança de schema, exchange ou routing key além do que já está lá.

---

## Architecture Decision Records

Nenhuma ADR nova é necessária: esta TechSpec aplica decisões já aceitas (stack, broker, convenção de
correlação e nomenclatura de eventos) sem introduzir escolha arquitetural nova. A opção de estender o
handler existente em vez de introduzir um mecanismo de eventos internos é uma decisão de implementação
de escopo local (um handler, dois blocos best-effort), não uma decisão estrutural durável.

- [ADR-001: Stack de backend — .NET / C# (ASP.NET Core)](../../docs/adr/adr-001-backend-stack-dotnet.md)
- [ADR-002: Broker de eventos da Fase 0 — RabbitMQ](../../docs/adr/adr-002-broker-fase0-rabbitmq.md) — define a convenção de nomes reaproveitada por `booking.payment_requested`.

---

## Próximos Passos

1. **Confirmar o estado de F01** (mesclada ou acessível no ambiente de implementação) antes de acionar
   o Task Creator — é pré-requisito bloqueante, não apenas referência.
2. **Implementação:** usar `tsg-flow-task-creator` referenciando esta TechSpec após promoção para
   `techspec.md` (`Aprovado`).
3. **Frontend:** não aplicável — o PRD confirma que esta feature não introduz nenhuma tela ou ação nova.
