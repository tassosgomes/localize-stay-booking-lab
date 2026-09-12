# Task 1.0 — Relatório de Revisão (focused)

- Mode: focused (primeira revisão)
- Validator: worker fresco, sem reutilizar evidência do implementer
- HEAD revisado: `08daf5e32cca43d0713600d0c495afeaebf211de` (= base; código da task em unstaged + untracked)
- Escopo revisado: diff `git diff HEAD` + untracked declarados (Domain/Reservations, Domain/Exceptions, Application/Reservations, Configurations, ReservationRepository, migration AddReservationAndSaga + Designer + Snapshot, UnitTests, IntegrationTests/Reservations, modificações em BookingDbContext, PersistenceExtensions, Infra.csproj, sln, BookingReadinessCheck, HealthCheckTests; deleções BootstrapCheck*)

## Gate (executado pelo validator)

Comando (contrato da task, seletores exatos):

```
scripts/ai-flow/gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.UnitTests.Reservations.ReservationTests" --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationPersistenceTests"
```

Resultado: **exit 0 — GATE: APROVADO**

- format: dotnet format ok (booking sln, 24 arquivos)
- build: 5 solutions ok, 0 warnings/0 errors
- testes: `ReservationTests` = 21, `ReservationPersistenceTests` = 2, 0 falhas (Testcontainers Postgres disponível)

## Verificação por critério

### 1. RN-02 a RN-06 (corretas e completas)

| RN | Implementação | Teste |
|----|---------------|-------|
| RN-02 período `[check-in, check-out)` ≥ 1 noite | `Reservation.EnsurePeriodIsValid` lança `PeriodoInvalidoException` se `checkOut <= checkIn` (Reservation.cs:107-113); `nights = checkOut.DayNumber - checkIn.DayNumber` | igual/antes/depois (3 testes) |
| RN-03 hóspedes positiva e ≤ capacidade | `EnsureGuestsCountIsValid` (`<=0`) + `guestsCount > facts.MaxGuests` → `CapacidadeExcedidaException` (Reservation.cs:77-80) | 0/negativos/1 + acima e igual à capacidade |
| RN-04 facts do Catalog | `!Active` → `AcomodacaoIndisponivelException`; `!AvailableForPeriod` → `PeriodoIndisponivelException` (Reservation.cs:72-85); ordem Active → capacidade → período conforme techspec | 3 rejeições isoladas + combinações com precedência ("first") |
| RN-05 preço/moeda congelados | `PricePerNight`/`Currency` copiados de `facts` na criação (Reservation.cs:98-100), persistidos `numeric(10,2)`/`varchar(3)` | sucesso + read-back integração |
| RN-06 total = noites × preço | `facts.PricePerNight * nights` (Reservation.cs:88-99) | 1/2/5 noites + fracionário 275.50×3=826.50 |
| RN-10 parcial (saga) | filho `SagaState.PaymentPending`, `CorrelationId = Reservation.Id` (Reservation.cs:103) | unitário + read-back integração |

### 2. Persistência real no schema `booking`

- Migration `20260912181605_AddReservationAndSaga` (gerada, não manuscrita): `Up` remove `booking.__bootstrap_check` e cria `reservations`/`reservation_sagas` com tipos exatos dos Modelos de Dados (uuid, varchar(255), date, integer, numeric(10,2), varchar(3), timestamptz); `accommodation_id` sem FK física; FK 1:1 `reservation_id` com índice **unique**; `Down` restaura sentinela.
- `BookingDbContext`: `HasDefaultSchema("booking")` + `ApplyConfigurationsFromAssembly` + `DbSet`s novos.
- `ReservationRepository.AddAsync` persiste aggregate + saga em uma operação (`AddAsync` + `SaveChangesAsync`).
- DI registrada (`AddScoped<IReservationRepository, ReservationRepository>` em PersistenceExtensions).
- `ReservationPersistenceTests`: leitura de volta por escopo/DbContext limpo (`AsNoTracking` + `Include(Saga)`) **e** SQL raw confirmando valores exatos de coluna (`status='solicitada'`, `state='PaymentPending'`, `correlation_id = reservation_id`) + sentinela ausente + 2 tabelas presentes.

### 3. Qualidade mínima do diff / Clean Architecture

- `rg "HttpClient|DbContext|Rmq|CloudEvents|Microsoft.EntityFrameworkCore"` em Domain e Application: **0 matches** (regra não negociável atendida).
- Infra.csproj passa a referenciar Application (Infra → Application → Domain, coerente).
- Exceções: uma classe por arquivo, herdam `DomainException` com mensagem descritiva, sem lógica extra (conforme convenção).
- Construtores privados + factory estática; `internal` no saga; encapsulamento com setters privados.
- **Sem escopo de tasks futuras**: nenhum endpoint, DTO, Catalog client, publisher, ProblemDetails ou handler no diff. Mudanças em `BootstrapCheck*` (deletados), `BookingReadinessCheck` (sonda passa a usar `Reservations`) e `HealthCheckTests` são consequência obrigatória da remoção da sentinela, não vazamento de escopo.
- Diff de `1_task.md` restrito a `status: pending→in_progress` e correção do `gate_command` para os seletores exatos (procedural, coerente com o contrato executado).

### 4. Testes provam o comportamento observável declarado

- 21 casos unitários: casos-limite de período/hóspedes, as 3 rejeições de facts isoladas, 3 combinações com precedência verificada, período/hóspedes inválidos dentro de `Create` (precedência sobre facts), sucesso 1/2/5 noites, preço fracionário, limite de capacidade, saga PaymentPending com correlation = reservation id.
- 2 casos de integração: round-trip via repositório + valores exatos nas colunas + migração substitui sentinela.
- Nota do implementer verificada: `Reservation.Create` chama `EnsurePeriodIsValid`/`EnsureGuestsCountIsValid` **antes** dos checks de `AvailabilityFacts` (Reservation.cs:69-70), ordem documentada por testes; autoproteção correta (`nights<=0` corromperia `totalAmount`) e compatível com a task 3.0, que continua podendo chamar os `Ensure*` públicos antes de consultar Catalog.

### 5. Gate reexecutado pelo validator

Verde (exit 0), evidência acima. HEAD e árvore não mudaram durante a revisão.

## Bloqueios

Nenhum.

## Recomendações (não bloqueantes)

1. **Redundância pequena entre testes**: `HealthCheckTests.Migration_creates_reservation_tables_and_removes_bootstrap_sentinel` e `ReservationPersistenceTests.AddReservationAndSaga_migration_stores_exact_column_values_and_drops_sentinel` verificam ambos sentinela ausente + 2 tabelas. Aceitável (protege a suíte existente), mas a sobreposição pode ser consolidada futuramente.
2. **Estilo de conversão de enums distinto**: `ReservationStatus` grava lowercase custom (`solicitada`) e `SagaState` grava PascalCase (`PaymentPending`) via `HasConversion<string>()`. Ambos os valores são exatamente os exigidos pelo `gate_expected_result`/techspec e verificados por SQL raw; a inconsistência é apenas estilística e pode ser uniformizada em task futura se desejado.

## Resultado

**VALIDAÇÃO APROVADA** (com 2 recomendações não bloqueantes)
