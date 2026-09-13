---
status: done
slice_type: enabling
verification_type: static
parallelizable: true
blocked_by: []
---

<task_context>
<domain>services/booking/domain</domain>
<type>implementation</type>
<scope>configuration</scope>
<complexity>low</complexity>
<dependencies>database</dependencies>
<unblocks>"2.0"</unblocks>
<feedback_checkpoint>`dotnet build services/booking/LocalizeStay.Booking.sln --nologo` sem erros; a migration gerada (`AddSagaCancellationReason`) contém a coluna `cancellation_reason` em `reservation_sagas` e não altera nenhuma tabela existente</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --static</gate_command>
<gate_test_selector>N/A — habilitador estático (sem lógica de transição a testar nesta task; a leitura dos novos valores é validada pelos testes de integração da task 2.0)</gate_test_selector>
<gate_expected_result>`dotnet build` da solution Booking sem erros/warnings novos; `dotnet format --verify-no-changes` (via gate) sem violações nos arquivos alterados</gate_expected_result>
<static_evidence>`dotnet ef migrations add AddSagaCancellationReason --project services/booking/src/4-Infra/LocalizeStay.Booking.Infra --startup-project services/booking/src/1-Services/LocalizeStay.Booking.Api` gera um arquivo cujo método `Up` contém `AddColumn` para `cancellation_reason` em `reservation_sagas` (nullable) e nenhum outro `AlterColumn`/`DropColumn` — conferir com `grep -n "cancellation_reason" <migration gerada>`; `dotnet ef database update` (contra um Postgres efêmero, ex. o mesmo container usado por `CustomWebApplicationFactory`) aplica a migration sem erro</static_evidence>
<vertical_slice>N/A — enabling</vertical_slice>
</task_context>

# Tarefa 1.0: Completar vocabulário de `SagaState`/`CancellationReason` no schema `booking` (EN-01 backend)

## Relacionada às User Stories

- "Como autor/arquiteto em estudo, eu quero consultar a situação da saga (pendente, autorizado,
  rejeitado)..." (suporte — esta task só cria os valores; a consulta em si é a task 2.0)
- "Como Guest, eu quero consultar minha Reservation... para saber se ela foi confirmada, cancelada..."
  (suporte — sem `Authorized`/`Rejected`/`CancellationReason` no domínio, os testes de 2.0 não
  conseguem cobrir os três estados exigidos pelas Métricas de Sucesso do PRD)

## Visão Geral

`domains/booking/domain.md` §3 já documenta que a Reservation Saga distingue pagamento pendente,
autorizado e rejeitado — mas F01 só implementou o primeiro valor (`SagaState.PaymentPending`), porque
era o único alcançável naquela feature. O PRD desta feature (F02) exige que a consulta cubra os três
estados de Reservation e as três situações de saga nos testes (Critério de Conclusão, Métricas de
Sucesso), e o contrato (`api-contract.yaml`) já declara `sagaStatus: [pendente, autorizado, rejeitado]`
e `cancellationReason` nullable. Esta task completa esse vocabulário de **leitura** no domínio e no
schema Postgres — sem introduzir nenhum método público de transição (isso é responsabilidade de uma
feature futura, F04, que ainda não existe). É puramente aditivo: nenhuma linha existente de
`reservation_sagas` é afetada, e nenhum código de produção grava esses valores nesta feature (a task
2.0 os lê via SQL direto apenas nos próprios testes de integração, documentado como dívida técnica na
TechSpec).

## Entrega Observável

- **Entrada ou gatilho:** `dotnet build` da solution Booking após as alterações; `dotnet ef
  migrations add` gerando a migration a partir do modelo estendido.
- **Resultado esperado:** a solution compila sem erros com `SagaState.Authorized`/`Rejected` e
  `ReservationSaga.CancellationReason` disponíveis; a migration gerada adiciona
  `reservation_sagas.cancellation_reason` (nullable, `varchar(500)`) e aplica sem erro contra um
  Postgres real.
- **Checkpoint de feedback:** `scripts/ai-flow/gate.sh --static` aprovado (format + build); inspeção
  manual do arquivo de migration confirmando a única coluna nova.
- **Seletor focalizado:** N/A — verificação estática justificada (ver `gate_test_selector` acima).
- **Fora deste checkpoint:** nenhuma leitura HTTP desses valores (isso é a task 2.0); nenhum método
  de domínio que transicione `SagaState` ou grave `CancellationReason` em produção (fica para F04);
  nenhuma linha real do banco recebe esses valores fora dos testes de 2.0.

## Requisitos

- `SagaState` (enum) ganha `Authorized` e `Rejected`, na mesma convenção PascalCase de
  `PaymentPending`; nenhum valor existente é removido ou renomeado.
- `ReservationSaga` ganha a propriedade `CancellationReason` (`string?`, setter privado); nenhum
  método público (construtor ou mutator) é adicionado para defini-la — a classe continua sem nenhuma
  forma pública de transição de estado.
- `ReservationSagaConfiguration` mapeia `CancellationReason` para a coluna `cancellation_reason`
  (`varchar(500)`, nullable) em `reservation_sagas`.
- A conversão de `SagaState` para string (`.HasConversion<string>()`, já existente) continua
  funcionando sem alteração — os novos valores gravam como `"Authorized"`/`"Rejected"`, mesma
  convenção já usada para `"PaymentPending"`; nenhuma migração de dados é necessária para o enum em
  si.
- Uma única migration EF (`AddSagaCancellationReason`) adiciona a coluna nova; nenhuma tabela
  existente é alterada além dessa adição.

## Arquivos Envolvidos

- **Criar:**
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Migrations/*_AddSagaCancellationReason.cs`
    (+ `.Designer.cs`; gerada via `dotnet ef migrations add`, não escrita à mão)
- **Modificar:**
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/SagaState.cs`
    (`+ Authorized`, `+ Rejected`)
  - `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/ReservationSaga.cs`
    (`+ CancellationReason`, string?, setter privado)
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/ReservationSagaConfiguration.cs`
    (mapeia `cancellation_reason`)
- **Referência:**
  - `domains/booking/domain.md` §3 — vocabulário de negócio já documentado (situação da saga)
  - `techspec.md` (Modelos de Dados, Habilitadores inevitáveis) — nomes de coluna e racional
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Migrations/20260912181605_AddReservationAndSaga.cs`
    — padrão da migration anterior a seguir
- **Skills para consultar durante implementação:**
  - `dotnet-architecture` — enum/propriedade aditivos sem lógica de transição
  - `dotnet-dependency-config` — geração e aplicação de migration EF Core

## Subtarefas

- [x] 1.1 Adicionar `SagaState.Authorized` e `SagaState.Rejected` (sem nenhuma lógica associada)
- [x] 1.2 Adicionar `ReservationSaga.CancellationReason` (string?, setter privado, sem mutator público)
- [x] 1.3 Mapear `cancellation_reason` (`varchar(500)`, nullable) em `ReservationSagaConfiguration`
- [x] 1.4 Gerar a migration `AddSagaCancellationReason` via `dotnet ef migrations add`, aplicar contra
      um Postgres efêmero e confirmar `dotnet build`/`gate.sh --static` verdes

## Sequenciamento

- Bloqueado por: Nenhuma (F01 já em `main`; schema/role `booking` já provisionados)
- Desbloqueia: 2.0 (consome `SagaState.Authorized`/`Rejected` e `ReservationSaga.CancellationReason`
  para projetar `sagaStatus`/`cancellationReason` na resposta)
- Paralelizável: Sim, com 3.0 (frontend) — nenhum arquivo compartilhado

## Rastreabilidade

- Esta tarefa cobre: vocabulário de `domains/booking/domain.md` §3 (situação da saga: pendente,
  autorizado, rejeitado); suporte a RN-07, RN-08 (a leitura desses estados, não a escrita).
- Evidência esperada: build verde após as alterações; migration gerada com a única coluna nova;
  aplicação da migration sem erro contra Postgres real.

## Detalhes de Implementação

Estado atual (F01, já em `main`):

```csharp
// Domain/Reservations/SagaState.cs
public enum SagaState
{
    PaymentPending
}

// Domain/Reservations/ReservationSaga.cs
public sealed class ReservationSaga
{
    private ReservationSaga() { }

    internal ReservationSaga(Guid id, Guid reservationId, Guid correlationId, SagaState state, DateTime createdAt)
    { /* ... */ }

    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public SagaState State { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
```

Alteração desta task (aditiva, sem tocar no construtor nem nas propriedades existentes):

```csharp
public enum SagaState
{
    PaymentPending,
    Authorized,
    Rejected
}

public sealed class ReservationSaga
{
    // ...propriedades existentes inalteradas...

    public string? CancellationReason { get; private set; }
}
```

Mapeamento atual (`ReservationSagaConfiguration`, já em `main`):

```csharp
builder.Property(saga => saga.State).HasColumnName("state")
    .HasColumnType("varchar(32)")
    .HasConversion<string>();
builder.Property(saga => saga.CreatedAt).HasColumnName("created_at");
```

Adicionar, na mesma classe:

```csharp
builder.Property(saga => saga.CancellationReason).HasColumnName("cancellation_reason")
    .HasColumnType("varchar(500)");
```

`HasConversion<string>()` já grava o **nome** do enum (`"PaymentPending"`) — os novos valores gravam
automaticamente como `"Authorized"`/`"Rejected"`, sem nenhuma função de conversão manual (diferente de
`ReservationStatus`, que usa `ToLowerInvariant()`; `SagaState` não precisa dessa tradução porque nunca
é serializado diretamente para o contrato — quem traduz para `sagaStatus` é a task 2.0).

**Convenções da stack:**
- Migration gerada via `dotnet ef migrations add AddSagaCancellationReason`, nunca escrita à mão
  (`dotnet-dependency-config`).
- Nenhuma classe de `Domain/Reservations/**` referencia `DbContext` — regra do baseline preservada
  (o enum/propriedade em si não violam isso).

## Prontidão para Implementação

- **Decisões fechadas:** nomes `Authorized`/`Rejected` (PascalCase, mesma convenção de
  `PaymentPending`); coluna `cancellation_reason`, `varchar(500)`, nullable; nenhum método público de
  transição é adicionado nesta task (decisão confirmada na TechSpec — fica para F04).
- **Limites de decisão do implementer:** nome exato do arquivo de migration gerado (timestamp do EF).
- **Dependências disponíveis:** schema/role `booking`, `BookingDbContext`, migration
  `AddReservationAndSaga` (F01, já em `main`).
- **Artefatos exigidos pelo gate:** nenhum teste behavioral nesta task; o gate `--static` roda apenas
  build/format sobre os arquivos alterados.
- **Dependências futuras:** Nenhuma — a task 2.0 consome os novos valores já prontos, sem precisar
  reabrir este arquivo.
- **Ambiguidades bloqueantes:** Nenhuma. O valor exato do limite de `varchar` (500) é uma decisão já
  fechada na TechSpec, mas pode ser ajustado sem impacto arquitetural (Questões em Aberto da
  TechSpec) — não é uma ambiguidade bloqueante, só um detalhe de implementação já proposto.

## Critérios de Sucesso (Verificáveis)

- [ ] `dotnet build services/booking/LocalizeStay.Booking.sln --nologo` compila sem erros
- [ ] `SagaState` expõe `Authorized` e `Rejected` além de `PaymentPending` (sem remover nenhum valor)
- [ ] `ReservationSaga.CancellationReason` existe (string?, setter privado); nenhum construtor ou
      método público novo foi adicionado para defini-la
- [ ] A migration `AddSagaCancellationReason` gerada contém apenas `AddColumn` para
      `cancellation_reason` em `reservation_sagas` (nullable, `varchar(500)`) — sem `AlterColumn`
      nem `DropColumn` em nenhuma tabela existente
- [ ] `dotnet ef database update` aplica a migration sem erro contra um Postgres real (efêmero)
- [ ] `scripts/ai-flow/gate.sh --static` aprovado (format + build, sem testes — modo static)
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente o vocabulário/schema, não a leitura HTTP (task 2.0)
