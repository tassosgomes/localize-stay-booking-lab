---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: true
blocked_by: []
---

<task_context>
<domain>services/booking</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"3.0"</unblocks>
<feedback_checkpoint>`dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationTests"` verde (todas as combinações de `AvailabilityFacts`); `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationPersistenceTests"` verde (cria via `Reservation.Create` e lê de volta `booking.reservations`/`booking.reservation_sagas`)</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.Reservations"</gate_command>
<gate_test_selector>Classes `ReservationTests` (`LocalizeStay.Booking.UnitTests`) e `ReservationPersistenceTests` (`LocalizeStay.Booking.IntegrationTests`)</gate_test_selector>
<gate_expected_result>Todos os testes de ambas as classes passam (verde); 0 falhas; a migration `AddReservationAndSaga` substitui `__bootstrap_check` e a leitura via `BookingDbContext` confirma uma linha em `booking.reservations` com `status='solicitada'` e uma em `booking.reservation_sagas` com `state='PaymentPending'`</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>`Reservation.Create` aplica RN-02 a RN-06 sobre `AvailabilityFacts` (todas as combinações de rejeição + sucesso com preço/total corretos), e uma `Reservation`/`ReservationSaga` válidas persistem e são lidas de volta no schema `booking` real, substituindo a tabela sentinela `__bootstrap_check`</vertical_slice>
</task_context>

# Tarefa 1.0: Domínio e persistência da Reservation com RN-02 a RN-06 (V-01)

## Relacionada às User Stories

- "Como Guest, eu quero solicitar uma reserva... para que meu pedido seja registrado com o preço
  vigente garantido" (cobertura parcial — aqui só a regra e a persistência, sem HTTP)
- "Como Guest, eu quero ser informado imediatamente quando meu pedido não pode ser aceito..."
  (cobertura parcial — aqui a regra pura; a superfície HTTP é a task 3.0)

## Visão Geral

Modela o aggregate `Reservation` e a entidade filha `ReservationSaga` como lógica de domínio pura
(sem HTTP, EF Core ou RabbitMQ), implementando as seis regras de negócio de RF-01 (RN-02 a RN-06)
como métodos estáticos/fábrica testáveis sem mock. Em seguida, persiste esse aggregate no schema
`booking` real via EF Core, substituindo a tabela sentinela `__bootstrap_check` entregue pela
fundação técnica da Fase 0. É a primeira fatia porque nenhuma outra parte de RF-01 (cliente Catalog,
endpoint) depende de HTTP ou de decisões ainda em aberto — só do próprio domínio.

## Entrega Observável

- **Entrada ou gatilho:** (a) chamada direta a `Reservation.Create(...)` com combinações de
  `AvailabilityFacts`; (b) um teste de integração chama `IReservationRepository.AddAsync` com uma
  `Reservation` válida e em seguida lê de volta via `BookingDbContext`.
- **Resultado esperado:** (a) `Reservation.Create` lança a exceção de domínio correta para cada
  violação e retorna uma `Reservation` "solicitada" com preço/total corretos no caminho de sucesso;
  (b) a leitura de volta confirma uma linha em `booking.reservations` e uma em
  `booking.reservation_sagas` com `state='PaymentPending'` e `correlation_id = reservation_id`.
- **Checkpoint de feedback:** `dotnet test --filter "FullyQualifiedName~ReservationTests"` (unitário)
  e `dotnet test --filter "FullyQualifiedName~ReservationPersistenceTests"` (integração, Testcontainers
  Postgres) — ambos verdes.
- **Seletor focalizado:** `LocalizeStay.Booking.UnitTests.Reservations.ReservationTests`,
  `LocalizeStay.Booking.IntegrationTests.Reservations.ReservationPersistenceTests`
- **Fora deste checkpoint:** nenhuma chamada HTTP a Catalog (isso é a task 2.0); nenhum endpoint
  público, `ProblemDetails` ou publicação de evento (isso é a task 3.0); a ordem "período/hóspedes
  antes de chamar Catalog" só é observável end-to-end na task 3.0.

## Requisitos

- `Reservation.EnsurePeriodIsValid(checkIn, checkOut)`: lança `PeriodoInvalidoException` quando
  `checkOut <= checkIn` (RN-02).
- `Reservation.EnsureGuestsCountIsValid(guestsCount)`: lança `QuantidadeHospedesInvalidaException`
  quando `guestsCount <= 0` (RN-03).
- `Reservation.Create(accommodationId, guestReference, checkIn, checkOut, guestsCount, facts)`:
  - `facts.Active == false` → `AcomodacaoIndisponivelException` (RN-04, Catalog RN-02 herdada).
  - `guestsCount > facts.MaxGuests` → `CapacidadeExcedidaException` (RN-03, Catalog RN-03 herdada).
  - `facts.AvailableForPeriod == false` → `PeriodoIndisponivelException` (RN-04, Catalog RN-04
    herdada).
  - Caminho de sucesso: `totalAmount = facts.PricePerNight * (checkOut.DayNumber - checkIn.DayNumber)`;
    constrói `Reservation` em `ReservationStatus.Solicitada` com `pricePerNight`/`currency` copiados
    de `facts` (RN-05, RN-06) e um `ReservationSaga` filho com `SagaState.PaymentPending` e
    `CorrelationId = Reservation.Id` (RN-10 parcial).
- Nenhuma classe de `Domain/Reservations/**` referencia `HttpClient`, `DbContext` ou
  `Rmq.CloudEvents` — regra não negociável do baseline (Domain não depende de infraestrutura).
- `ReservationRepository.AddAsync` persiste `Reservation` e seu `ReservationSaga` filho em uma única
  operação (schema `booking`, via `booking_role`).
- Migration `AddReservationAndSaga` remove `__bootstrap_check` e cria `booking.reservations` e
  `booking.reservation_sagas` conforme os modelos de dados da TechSpec.

## Arquivos Envolvidos

- **Criar:**
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Reservation.cs`
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/ReservationSaga.cs`
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/AvailabilityFacts.cs`
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/ReservationStatus.cs`
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/SagaState.cs`
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Exceptions/{PeriodoInvalidoException,QuantidadeHospedesInvalidaException,AcomodacaoIndisponivelException,CapacidadeExcedidaException,PeriodoIndisponivelException}.cs`
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationRepository.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/{ReservationConfiguration,ReservationSagaConfiguration}.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Migrations/*_AddReservationAndSaga.cs`
    (gerada via `dotnet ef migrations add`, não escrita à mão)
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/ReservationRepository.cs`
  - `services/booking/tests/LocalizeStay.Booking.UnitTests/LocalizeStay.Booking.UnitTests.csproj` (novo
    projeto de teste — a fundação não criou nenhum projeto unitário, só integração) +
    `Reservations/ReservationTests.cs`
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationPersistenceTests.cs`
    (projeto já existe, criado pela fundação — só adiciona a classe)
- **Modificar:**
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/BookingDbContext.cs`
    (adiciona `DbSet<Reservation>`, `DbSet<ReservationSaga>` e aplica as novas
    `IEntityTypeConfiguration<T>`; a fundação já criou este arquivo apenas com a tabela sentinela)
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/PersistenceExtensions.cs`
    (registra `IReservationRepository → ReservationRepository`; a fundação já criou este arquivo só
    com o `AddDbContext`)
- **Referência:**
  - `tasks/prd-fundacao-fase0/techspec.md` — estrutura de solution, convenção de `Extensions/*`
  - `domains/booking/domain.md` (RN-01 a RN-06, RN-10) — definição das regras
  - `techspec.md` (Design de Implementação, Modelos de Dados) — assinaturas de `Reservation.Create` e
    schema das duas tabelas
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — exceções de domínio específicas, aggregate/entidade filha
  - `dotnet-dependency-config` — `IEntityTypeConfiguration<T>`, migration EF Core
  - `dotnet-testing` — `xUnit`/`Moq` para unitário; `CustomWebApplicationFactory` + Testcontainers
    Postgres para integração (mesmo padrão de `HealthCheckTests` da fundação)

## Subtarefas

- [ ] 1.1 Implementar `Reservation`, `ReservationSaga`, `AvailabilityFacts`, `ReservationStatus`,
      `SagaState` e as 5 exceções de domínio
- [ ] 1.2 Implementar `EnsurePeriodIsValid`, `EnsureGuestsCountIsValid` e `Create` com as 4 ramificações
      de rejeição + caminho de sucesso (preço/total)
- [ ] 1.3 Escrever `ReservationTests` cobrindo casos-limite de período/hóspedes e todas as combinações
      de `AvailabilityFacts` (inclusive sucesso para 1, 2 e N noites)
- [ ] 1.4 Implementar `IReservationRepository`, `ReservationConfiguration`/`ReservationSagaConfiguration`
      e `ReservationRepository`; gerar migration `AddReservationAndSaga` removendo `__bootstrap_check`
- [ ] 1.5 Registrar `IReservationRepository` em `PersistenceExtensions.cs`
- [ ] 1.6 Escrever `ReservationPersistenceTests` (Testcontainers Postgres): cria via
      `Reservation.Create` + `AddAsync`, lê de volta e confirma os dois registros

## Sequenciamento

- Bloqueado por: Nenhuma (a fundação técnica da Fase 0 — schema/role `booking`, `BookingDbContext`,
  projeto de integration tests — é pré-requisito externo já provisionado, não uma task deste plano)
- Desbloqueia: 3.0 (consome `Reservation`, as 5 exceções e `IReservationRepository`)
- Paralelizável: Sim, com 2.0 — nenhum arquivo compartilhado

## Rastreabilidade

- Esta tarefa cobre: RN-01, RN-02, RN-03, RN-04 (regra, não a origem HTTP dos fatos), RN-05, RN-06,
  RN-10 (parcial); Catalog RN-02/RN-03/RN-04 herdadas do ponto de vista de Booking como consumidor da
  forma de `AvailabilityFacts`.
- Evidência esperada: `ReservationTests` verde cobrindo as 4 rejeições + sucesso (1/2/N noites);
  `ReservationPersistenceTests` verde confirmando as duas tabelas com os valores corretos.

## Detalhes de Implementação

Assinaturas normativas (`techspec.md`, Design de Implementação):

```csharp
public sealed class Reservation
{
    public static Reservation Create(
        Guid accommodationId, string guestReference, DateOnly checkIn, DateOnly checkOut,
        int guestsCount, AvailabilityFacts facts)
    {
        if (!facts.Active) throw new AcomodacaoIndisponivelException();
        if (guestsCount > facts.MaxGuests) throw new CapacidadeExcedidaException();
        if (!facts.AvailableForPeriod) throw new PeriodoIndisponivelException();

        var nights = checkOut.DayNumber - checkIn.DayNumber;
        var totalAmount = facts.PricePerNight * nights;
        // constrói Reservation "solicitada" + ReservationSaga(PaymentPending, CorrelationId=Id)
    }

    public static void EnsurePeriodIsValid(DateOnly checkIn, DateOnly checkOut)
    {
        if (checkOut <= checkIn) throw new PeriodoInvalidoException();
    }

    public static void EnsureGuestsCountIsValid(int guestsCount)
    {
        if (guestsCount <= 0) throw new QuantidadeHospedesInvalidaException();
    }
}

public sealed record AvailabilityFacts(bool Active, int MaxGuests, bool AvailableForPeriod,
    decimal PricePerNight, string Currency);
```

Modelos de dados (schema `booking`):

- `reservations`: `id` (uuid PK), `accommodation_id` (uuid, sem FK física), `guest_reference`
  (varchar(255)), `check_in`/`check_out` (date), `guests_count` (int), `status` (enum
  `solicitada|confirmada|cancelada` — só `solicitada` é gravado por esta feature),
  `price_per_night`/`total_amount` (numeric(10,2)), `currency` (varchar(3), sempre `BRL`),
  `created_at` (timestamptz UTC).
- `reservation_sagas`: `id` (uuid PK), `reservation_id` (uuid, FK única 1:1), `correlation_id` (uuid,
  igual a `reservation_id` nesta feature), `state` (enum, só `PaymentPending` aqui), `created_at`.

`EnsurePeriodIsValid`/`EnsureGuestsCountIsValid` são expostos como métodos estáticos separados de
`Create` porque a task 3.0 precisa chamá-los **antes** de consultar Catalog (ordem normativa da AC de
RF-01) — não são só passos internos de `Create`.

**Convenções da stack:**
- Exceções de domínio herdam o padrão de `dotnet-architecture/examples/error-handling.md`
  (`DomainException` como base, mensagem descritiva); cada uma das 5 é uma classe própria, sem lógica
  além do construtor.
- Testes seguem Arrange-Act-Assert (`dotnet-testing/examples/unit-tests.md`); testes de integração
  reutilizam o padrão `CustomWebApplicationFactory` + `ICollectionFixture` já usado por
  `HealthCheckTests` da fundação.

## Prontidão para Implementação

- **Decisões fechadas:** fórmula de preço (`pricePerNight * nights`, `nights = checkOut.DayNumber -
  checkIn.DayNumber`); `currency` sempre `BRL`; `ReservationSaga.CorrelationId = Reservation.Id`;
  schema/role `booking` já existentes (fundação); nomes de tabela/coluna conforme Modelos de Dados
  acima.
- **Limites de decisão do implementer:** nome exato do arquivo de migration gerado; organização
  interna de `Domain/Reservations/Exceptions/` (uma classe por arquivo, sem namespace adicional
  exigido).
- **Dependências disponíveis:** `BookingDbContext` e schema/role `booking` (fundação, externos);
  projeto `LocalizeStay.Booking.IntegrationTests` já existe (fundação).
- **Artefatos exigidos pelo gate:** `ReservationTests.cs` e o novo projeto
  `LocalizeStay.Booking.UnitTests` são criados nesta própria task; `ReservationPersistenceTests.cs` é
  criado nesta própria task no projeto de integração já existente; o Testcontainers Postgres é
  efêmero.
- **Dependências futuras:** Nenhuma — a task 3.0 consome `Reservation`/exceções/`IReservationRepository`
  já prontos e testados por esta task, sem precisar reabri-los.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationTests"`
- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationPersistenceTests"`
- [ ] Os dois seletores encontram pelo menos um teste cada e não executam casos sem relação com esta
      task
- [ ] Build compila sem erros: `dotnet build services/booking/LocalizeStay.Booking.sln`
- [ ] `Reservation.Create` lança a exceção correta para `Active=false`, `guestsCount>MaxGuests` e
      `AvailableForPeriod=false`, isoladamente e combinados
- [ ] `Reservation.Create` no caminho de sucesso produz `totalAmount` correto para 1, 2 e N noites e
      `ReservationSaga.State == PaymentPending`
- [ ] Após `AddAsync`, uma nova leitura via `BookingDbContext` confirma a linha em
      `booking.reservations` (`status='solicitada'`) e em `booking.reservation_sagas`
      (`state='PaymentPending'`, `correlation_id = reservation_id`)
- [ ] A migration aplicada remove `__bootstrap_check` e cria as duas tabelas novas
- [ ] Checkpoint de feedback executado conforme descrito acima
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente domínio + persistência, não a superfície HTTP nem o cliente
      Catalog (tasks 2.0/3.0)
