# TechSpec: Consulta de Reserva (Booking F02)

> **Modo de operação:** API-First
> **PRD de origem:** `tasks/prd-consulta-reserva/prd.md`
> **API Contract:** `tasks/prd-consulta-reserva/api-contract.yaml` (v1.0.0, status "Em Revisão")
> **Data:** 2026-09-12
> **Status:** Aprovado
> **Handoff:** approved — pode alimentar o Task Creator
>
> Aprovado pelo autor nesta revisão, com as três decisões materiais confirmadas (ver "Questões em
> Aberto" para o registro completo): (1) **[atualização 2026-09-12] F01 já foi mesclada em `main`**
> (PR #4, commit `fbeea05`) — o código de `Reservation`/`ReservationSaga`/`Dispatcher`/
> `IReservationRepository` etc. está disponível e idêntico ao que esta TechSpec assumiu; a
> dependência bloqueante abaixo está resolvida; (2) o vocabulário de `SagaState`/`CancellationReason`
> é completado nesta feature (não adiado para F04), sem nenhuma lógica de transição; (3) os testes de
> integração usam `UPDATE` SQL direto para simular os estados `confirmada`/`cancelada`, documentado
> como dívida a revisar quando F04 existir.

---

## Resumo Executivo

Esta TechSpec implementa `GET /v1/reservations/{reservationId}` no serviço `LocalizeStay.Booking`,
a segunda feature de negócio do domínio Booking e a primeira consulta (query) do serviço. O caso de
uso (`GetReservationByIdQuery`) valida o formato do identificador, busca a `Reservation` com sua
`ReservationSaga` associada via uma única leitura consistente, e projeta o estado do ciclo de vida,
a situação observável da saga (`sagaStatus`) e o motivo de cancelamento quando aplicável. Operação
100% somente leitura: não inicia, avança nem compensa a saga.

Adota CQRS nativo estendendo o `Dispatcher` já existente (criado em F01) com o lado de query
(`IQuery<TResponse>`/`IQueryHandler<,>`), exatamente como o comentário já deixado em
`Application/Cqrs/Dispatcher.cs` antecipava ("Queries entram quando a primeira consulta existir
(F02)"). Reaproveita a mesma resposta de erro RFC 9457 e o mesmo `IExceptionHandler` global de F01,
apenas com um novo caso de exceção (`ReservationNotFoundException` → 404).

**Trade-off primário:** o domínio de Booking hoje só sabe produzir o estado `Solicitada`/
`PaymentPending` (F01); os estados `Confirmada`/`Cancelada` e `Autorizado`/`Rejeitado` (F03/F04)
ainda não têm nenhum código que os produza. Para que esta feature cumpra o critério de conclusão do
PRD — consultar uma Reservation "em qualquer ponto do seu ciclo de vida" e cobrir os três estados
nos testes (Métricas de Sucesso do PRD) — esta TechSpec completa apenas o *vocabulário* de leitura
(enum `SagaState` e coluna `CancellationReason`, já descritos em `domains/booking/domain.md` §3) e
usa escrita direta via SQL só no arranjo dos testes de integração, sem introduzir nenhum método de
domínio para *transicionar* para esses estados — essa é responsabilidade de F04. Em troca, aceita-se
uma dívida documentada: os helpers de teste desta feature devem ser revisados/substituídos quando
F04 introduzir a transição real.

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|-------|---------|------------------------|
| `dotnet-architecture` | `.claude/skills/dotnet-architecture` | Extensão do CQRS nativo já usado em F01 com o lado de query (`examples/cqrs.md`), reaproveitando Clean Architecture e o padrão de exceções de domínio (`examples/error-handling.md`) |
| `dotnet-testing` | `.claude/skills/dotnet-testing` | Reaproveita `CustomWebApplicationFactory` (Testcontainers Postgres) e `BookingIntegrationTestCollection` já existentes de F01; unitário para o handler de query com mocks |
| `restful-api` | `.claude/skills/restful-api` | Já aplicada na geração do `api-contract.yaml`; referenciada aqui só para estender o `IExceptionHandler` global com o novo `code` |

Não foram lidos `dotnet-dependency-config` (nenhuma dependência/pacote novo — reaproveita EF Core,
FluentValidation e Testcontainers já configurados por F01), `dotnet-code-quality`,
`dotnet-observability` e `design-patterns` porque nenhuma decisão desta TechSpec depende deles além
do que o baseline e o precedente de F01 já definem.

---

## Arquitetura do Sistema

### Visão Geral dos Componentes

- **`ReservationEndpoints` (API, modificado):** adiciona `GET /v1/reservations/{reservationId}`,
  delegando ao `IDispatcher` já existente.
- **`GetReservationByIdQuery` + `GetReservationByIdQueryValidator` + `GetReservationByIdQueryHandler`
  (Application, novos):** validam o shape do identificador (400 em formato inválido) e orquestram a
  leitura via repositório, lançando `ReservationNotFoundException` (404) quando não existe.
- **`IReservationRepository.GetByIdAsync` (Application/Infra, estendido):** nova leitura com
  `Include(Saga)`, sem tracking — não reaproveita nenhuma lógica de escrita de F01.
- **`ReservationSaga.CancellationReason` + `SagaState.Authorized`/`Rejected` (Domain, estendidos):**
  completam o vocabulário de leitura já descrito em `domains/booking/domain.md` §3; nenhuma lógica
  de transição é adicionada (fica para F04).
- **`ReservationDetailResponseDto` (API, novo):** projeta `Reservation`+`Saga` no schema
  `ReservationDetail` do contrato, incluindo a tradução de `SagaState` para o vocabulário de negócio
  (`pendente`/`autorizado`/`rejeitado`).
- **`GlobalExceptionHandler` (API, estendido):** novo case `ReservationNotFoundException` → 404
  `RESERVATION_NOT_FOUND`, sem alterar nenhum case existente de F01.

### Diagrama de Componentes

```
GET /v1/reservations/{id}
        │
        ▼
ReservationEndpoints.HandleGetByIdAsync
        │  new GetReservationByIdQuery(id)
        ▼
IDispatcher.SendAsync<Reservation>(query)          ← extensão do Dispatcher de F01
        │
        ▼
GetReservationByIdQueryHandler
   │ 1. valida formato (FluentValidation) → 400 VALIDATION_ERROR
   │ 2. Guid.Parse
   │ 3. IReservationRepository.GetByIdAsync(id)
   │    → null? throw ReservationNotFoundException → 404 RESERVATION_NOT_FOUND
   ▼
Reservation (com .Saga carregado) ──► ReservationDetailResponseDto.From(reservation) ──► 200 JSON
```

---

## Estratégia de Entrega Incremental

### Mapa de Fatias Verticais

| Slice | Comportamento observável | US/RF/RN cobertos | Entrada → processamento → saída | Artefatos principais | Evidência / checkpoint | Bloqueado por |
|-------|--------------------------|--------------------|----------------------------------|-----------------------|--------------------------|----------------|
| V-01 | `GET /v1/reservations/{id}` retorna 200 com todos os campos (incluindo os 3 estados de Reservation e as 3 situações de saga) e distingue corretamente 400/404 | RF-01 (todas as ACs), RN-05, RN-06, RN-07, RN-08, RN-10, RN-11 | requisição HTTP → validação de formato → `GetReservationByIdQueryHandler` → `IReservationRepository.GetByIdAsync` → `ReservationDetailResponseDto` | `GetReservationByIdQuery(Handler,Validator)`, `IReservationRepository.GetByIdAsync`, `ReservationDetailResponseDto`, `ReservationEndpoints` (GET), `GlobalExceptionHandler` (novo case), `ReservationNotFoundException` | Os 5 cenários do AC de RF-01 via `WebApplicationFactory` real (Postgres Testcontainers) | Habilitador "Completar vocabulário de SagaState/CancellationReason" |

### Habilitadores inevitáveis

| Habilitador | Por que não pode fazer parte de uma fatia | Menor escopo | Fatias desbloqueadas |
|-------------|--------------------------------------------|--------------|----------------------|
| Completar vocabulário de `SagaState`/`CancellationReason` no schema `booking` | O contrato e o PRD exigem que a consulta represente `autorizado`/`rejeitado` e o motivo de cancelamento, mas F01 só implementou `PaymentPending`; sem esses valores existirem no domínio/schema, a projeção de leitura de V-01 não tem de onde ler. Já são vocabulário de domínio documentado (`domains/booking/domain.md` §3: "Reservation Saga distingue ao menos pagamento pendente, autorizado e rejeitado"), não uma decisão de arquitetura nova. | `SagaState.Authorized`/`Rejected` (só os valores do enum, sem lógica), `ReservationSaga.CancellationReason` (propriedade nullable com setter privado, sem nenhum método público de transição), 1 migration EF (`reservation_sagas.cancellation_reason`) | V-01 |

---

## Design de Implementação

### Interfaces Principais

```csharp
// Application/Cqrs/Dispatcher.cs — extensão do que já existe (F01)
public interface IQuery<TResponse>;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken); // já existe
    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);      // novo
}
```

```csharp
// Application/Reservations/GetReservationByIdQuery.cs (novo)
public sealed record GetReservationByIdQuery(string ReservationId) : IQuery<Reservation>;

// string bruta (não Guid): a validação de formato é responsabilidade desta
// query/validator, não do model binding — permite distinguir 400 de 404
// sem depender de constraint de rota ":guid" (que gera 404 do próprio
// roteamento em vez do 400 controlado pelo contrato).
```

```csharp
// Application/Reservations/IReservationRepository.cs (estendido)
public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken); // já existe
    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken); // novo
}
```

### Modelos de Dados

**Mapeamento Entidade do Domain Doc → Modelo Técnico:**

| Entidade do Domain Doc | Modelo Técnico | Local |
|------------------------|----------------|-------|
| Reservation | `Reservation` (classe já existente, F01) | `Domain/Reservations/Reservation.cs` — **sem alteração** |
| Reservation Saga | `ReservationSaga` (estendida) | `Domain/Reservations/ReservationSaga.cs` — **+ `CancellationReason` (string?, setter privado)** |
| Situação da saga | `SagaState` (estendido) | `Domain/Reservations/SagaState.cs` — **+ `Authorized`, `Rejected`** |

`SagaState` continua mapeado com `.HasConversion<string>()` (já existente em
`ReservationSagaConfiguration`) — os novos valores gravam como `"Authorized"`/`"Rejected"`, mesma
convenção (PascalCase) já usada para `"PaymentPending"`; nenhuma migração de dados é necessária para
o enum em si. `CancellationReason` ganha uma nova coluna `cancellation_reason` (nullable,
`varchar(500)`) em `reservation_sagas`, mapeada em `ReservationSagaConfiguration` — única migração
EF desta feature.

**Tradução `SagaState` → vocabulário de negócio (`sagaStatus` do contrato), feita em
`ReservationDetailResponseDto`:**

| `SagaState` | `sagaStatus` (contrato) |
|---|---|
| `PaymentPending` | `"pendente"` |
| `Authorized` | `"autorizado"` |
| `Rejected` | `"rejeitado"` |

### Endpoints de API

#### Modo API-First

> Os endpoints, schemas, autenticação e formato de erros são definidos no
> [API Contract](api-contract.yaml). Esta TechSpec NÃO duplica essas definições.

**Mapeamento de implementação dos endpoints do contrato:**

| operationId | Caminho de Implementação |
|-------------|--------------------------|
| `getReservationById` | `ReservationEndpoints.HandleGetByIdAsync` → `IDispatcher.SendAsync(GetReservationByIdQuery)` → `GetReservationByIdQueryHandler` → `IReservationRepository.GetByIdAsync` → `ReservationDetailResponseDto.From(reservation)` |

**Validações adicionais** (além das declaradas no contrato):

| Endpoint | Validação | Local na Implementação |
|----------|-----------|-------------------------|
| `getReservationById` | `reservationId` não vazio e parseável como `Guid` (`Guid.TryParse`) | Application — `GetReservationByIdQueryValidator` (FluentValidation) |

**Mapeamento de Exceções → ErrorResponse do Contrato:**

| Exceção de Domínio | HTTP | code (do contrato) |
|---------------------|------|---------------------|
| `ValidationException` (FluentValidation) | 400 | `VALIDATION_ERROR` *(case já existente em `GlobalExceptionHandler`, reaproveitado sem alteração)* |
| `ReservationNotFoundException` (novo) | 404 | `RESERVATION_NOT_FOUND` *(novo case)* |

### Money e formato de datas

Reaproveita `MoneyStringJsonConverter` já existente (F01) para `pricePerNight`/`totalAmount`;
`checkIn`/`checkOut` como `DateOnly`, `createdAt` como `DateTime` — serialização default do
ASP.NET Core já produz `date`/`date-time` ISO 8601, mesma convenção do contrato.

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Fatia | Tipo | Skills Aplicáveis | Descrição |
|---------|-------|------|---------------------|-----------|
| `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Exceptions/ReservationNotFoundException.cs` | V-01 | Exception | `dotnet-architecture` | Lançada pelo handler de query quando o id não existe |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/GetReservationByIdQuery.cs` | V-01 | Query | `dotnet-architecture` | Record `IQuery<Reservation>` com `ReservationId` bruto (string) |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/GetReservationByIdQueryValidator.cs` | V-01 | Validator | `dotnet-architecture` | FluentValidation: não vazio + formato Guid |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/GetReservationByIdQueryHandler.cs` | V-01 | QueryHandler | `dotnet-architecture` | Valida, parseia, busca no repositório, lança 404 se ausente |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Contracts/ReservationDetailResponseDto.cs` | V-01 | DTO | `dotnet-architecture` | Projeta `Reservation`+`Saga` no schema `ReservationDetail`, com tradução de `sagaStatus` |
| Migration EF `AddSagaCancellationReason` (nome definitivo gerado na implementação) | Habilitador | Migration | `dotnet-dependency-config` | Adiciona `reservation_sagas.cancellation_reason` (nullable) |
| `services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/GetReservationByIdQueryHandlerTests.cs` | V-01 | Test | `dotnet-testing` | Mocks de `IReservationRepository`; formato inválido, não encontrado, encontrado |
| `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationQueryEndpointTests.cs` | V-01 | Test | `dotnet-testing` | Os 5 cenários do AC de RF-01 via `WebApplicationFactory` real |

### Arquivos a Modificar

| Caminho | Fatia | Skills Aplicáveis | Alteração |
|---------|-------|---------------------|-----------|
| `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/SagaState.cs` | Habilitador | `dotnet-architecture` | `+ Authorized`, `+ Rejected` |
| `services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/ReservationSaga.cs` | Habilitador | `dotnet-architecture` | `+ CancellationReason` (string?, setter privado, sem mutator público) |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/ReservationSagaConfiguration.cs` | Habilitador | `dotnet-dependency-config` | Mapeia `cancellation_reason` (`varchar(500)`, nullable) |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/IReservationRepository.cs` | V-01 | `dotnet-architecture` | `+ GetByIdAsync(Guid, CancellationToken)` |
| `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/ReservationRepository.cs` | V-01 | `dotnet-architecture` | Implementa `GetByIdAsync` com `Include(Saga)` + `AsNoTracking()` |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Cqrs/Dispatcher.cs` | V-01 | `dotnet-architecture` | `+ IQuery<TResponse>`, `+ IQueryHandler<,>`, `+ SendAsync(IQuery<TResponse>, ...)` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Endpoints/ReservationEndpoints.cs` | V-01 | `restful-api` | `+ MapGet("/v1/reservations/{reservationId}", ...)` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/ErrorHandling/GlobalExceptionHandler.cs` | V-01 | `restful-api` | `+` case `ReservationNotFoundException` → 404 `RESERVATION_NOT_FOUND` |
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/ApplicationExtensions.cs` | V-01 | `dotnet-architecture` | Registra `IQueryHandler<GetReservationByIdQuery, Reservation>` e `IValidator<GetReservationByIdQuery>` |

### Arquivos de Referência (não alterar)

| Caminho | Motivo da Consulta |
|---------|---------------------|
| `services/booking/src/1-Services/LocalizeStay.Booking.Api/Contracts/MoneyStringJsonConverter.cs` | Reaproveitado sem alteração no novo DTO |
| `services/booking/tests/LocalizeStay.Booking.IntegrationTests/CustomWebApplicationFactory.cs` | Reaproveitada sem alteração para os novos testes de integração |
| `services/booking/tests/LocalizeStay.Booking.IntegrationTests/BookingIntegrationTestCollection.cs` | Collection xUnit reaproveitada |
| `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/RequestReservationCommandValidator.cs` | Padrão de FluentValidation a seguir no novo validator |

---

## Análise de Impacto

| Componente Afetado | Tipo de Impacto | Descrição & Risco | Ação Requerida |
|---------------------|------------------|----------------------|------------------|
| `SagaState` (enum) | Modificado (aditivo) | Novos valores não afetam linhas existentes (`PaymentPending`); risco baixo | Nenhuma migração de dados |
| `ReservationSaga` | Modificado (aditivo) | Nova propriedade nullable; default `null` para linhas existentes; risco baixo | Migration EF |
| `IReservationRepository`/`ReservationRepository` | Modificado | Novo método; `AddAsync` inalterado; risco baixo | Nenhuma |
| `Dispatcher`/`IDispatcher` | Modificado | Nova sobrecarga (overload); uso existente de commands inalterado; risco baixo | Nenhuma |
| `GlobalExceptionHandler` | Modificado | Novo `case` no switch expression; casos existentes de F01 inalterados; risco baixo | Nenhuma |
| Schema `booking` (Postgres) | Modificado | +1 coluna nullable em `reservation_sagas`; risco baixo, mas **precisa rodar em qualquer ambiente onde F01 já esteja implantado** | Aplicar migration antes do deploy desta feature |
| API Contract | Novo endpoint | Não modifica o contrato de F01 (`tasks/prd-solicitacao-reserva/api-contract.yaml`); contrato próprio desta feature | Nenhuma |
| **F04 (Conclusão da Saga, ainda não especificada)** | Downstream | F04 deverá **reutilizar** `SagaState.Authorized`/`Rejected` e `ReservationSaga.CancellationReason` já adicionados aqui, em vez de redefini-los | Apontar esta TechSpec como referência quando F04 for especificada |

---

## Abordagem de Testes

### Testes Unitários

- `GetReservationByIdQueryHandlerTests` (mock de `IReservationRepository`):
  - `reservationId` vazio ou não-Guid → `ValidationException`, repositório **nunca chamado**
    (`Verify` de que `GetByIdAsync` não roda).
  - Repositório retorna `null` → `ReservationNotFoundException`.
  - Repositório retorna uma `Reservation` → handler retorna a mesma instância sem transformação
    (é a `ReservationDetailResponseDto`, na camada de API, quem projeta os campos de saga).

### Testes de Integração

- **Endpoint completo (V-01):** os 5 cenários do AC de RF-01 via `WebApplicationFactory` real
  (`CustomWebApplicationFactory` já existente, Postgres via Testcontainers):
  1. `solicitada` + `pendente` — seed via `IReservationRepository.AddAsync` (caminho normal de F01,
     produz exatamente esse estado).
  2. `confirmada` + `autorizado` — seed via `AddAsync` e, em seguida, `UPDATE` SQL direto
     (`dbContext.Database.ExecuteSqlInterpolated`) em `reservations.status` e
     `reservation_sagas.state`, **não** por um método de domínio (nenhum existe ainda; é
     responsabilidade de F04). Documentado como decisão de teste, não de produção.
  3. `cancelada` + `rejeitado` + `cancellationReason` preenchido — mesma técnica de seed, incluindo
     a coluna nova.
  4. Identificador bem formado sem Reservation correspondente → 404 `RESERVATION_NOT_FOUND`.
  5. Identificador malformado (ex.: `"nao-e-um-uuid"`) → 400 `VALIDATION_ERROR`.
- Reaproveita `BookingIntegrationTestCollection` (mesma collection de F01, evita conflito de porta
  entre containers).

### Testes de Contrato

- `npx dredd tasks/prd-consulta-reserva/api-contract.yaml http://localhost:5102` (mesma porta local
  de Booking já usada por F01) contra os exemplos de `GET /reservations/{reservationId}` do
  contrato (sugestão já registrada em `api-contract.md`).

---

## Sequenciamento de Desenvolvimento

### Build Order

1. Habilitador (extensão de `SagaState`/`CancellationReason` + migration) — depende apenas do schema
   `booking` já existente (criado por F01).
2. V-01 (query + handler + repositório + endpoint + tratamento de erro) — depende do Habilitador.

### Dependências Técnicas Bloqueantes

- ~~`main` ainda não contém o código de F01~~ — **Resolvido em 2026-09-12:** `feature/prd-solicitacao-reserva`
  foi mesclada em `main` via PR #4 (commit `fbeea05`). Verificado nesta atualização: `Reservation`,
  `ReservationSaga`, `SagaState`, `Dispatcher`/`IDispatcher`, `IReservationRepository`,
  `GlobalExceptionHandler`, `CustomWebApplicationFactory` etc. estão em `main` e são idênticos ao que
  esta TechSpec assumiu ao ser escrita (`git diff` entre a branch de origem e `main` para
  `services/booking/src` não mostra diferenças). Nenhuma reconciliação de design é necessária antes
  de gerar as tasks.
- Nenhuma dependência de infraestrutura nova: reaproveita schema `booking`, role `booking_role` e a
  fundação técnica já provisionados por F01/fundação Fase 0.

---

## Monitoramento e Observabilidade

- Logging estruturado (mesmo padrão de F01) em cada ponto de decisão: requisição recebida
  (`reservationId`), resultado (`encontrada`/`não encontrada`), rejeição de formato aplicada — sem
  novo campo de correlação: `correlationId` já é devolvido no corpo da resposta (DP-02 do PRD), não
  precisa ser extraído do log para esta feature.
- Nenhuma métrica/tracing formal nesta fase, conforme baseline (Fase 1 trata disso).

---

## Considerações Técnicas

### Decisões Principais

- **Decisão:** Query handler retorna o `Reservation` de domínio diretamente (com `.Saga` carregado);
  a tradução para o schema do contrato (`ReservationDetailResponseDto`, incluindo `sagaStatus`)
  acontece na camada de API.
  **Racional:** mesmo padrão já usado pelo `RequestReservationCommandHandler` de F01 (retorna
  `Reservation`, a API mapeia para `ReservationResponseDto`); manter consistência evita introduzir
  uma segunda convenção para uma feature simples.
  **Trade-offs:** o tipo de domínio atravessa a fronteira Application→API; aceitável porque já é o
  padrão estabelecido nesta base de código.
  **Alternativas rejeitadas:** um DTO de resultado próprio da Application
  (`ReservationDetailResult`) — rejeitado por adicionar uma camada de mapeamento extra sem ganho
  real, dado que já existe precedente do padrão mais simples em F01.

- **Decisão (confirmada pelo autor):** Completar `SagaState` (`Authorized`, `Rejected`) e adicionar
  `ReservationSaga.CancellationReason` nesta feature, mesmo sem nenhum caminho de escrita real
  (F03/F04) ainda existir.
  **Racional:** `domains/booking/domain.md` §3 já documenta esses três estados de saga como
  vocabulário do domínio (não é uma decisão nova); as Métricas de Sucesso do PRD exigem que os testes
  desta feature cubram os três estados de Reservation e as três situações de saga. Sem esses valores
  no schema, essa cobertura de teste é impossível.
  **Trade-offs:** cria colunas/valores "inertes" até F04 existir — nenhum código de produção ainda os
  escreve fora dos testes desta feature.
  **Alternativas rejeitadas:** adiar toda a extensão de schema para a TechSpec de F04 — rejeitada
  porque o próprio PRD desta feature (Critério de Conclusão, Métricas de Sucesso) exige a cobertura
  completa dos três estados nos testes de F02, não apenas do estado inicial.

- **Decisão (confirmada pelo autor):** os testes de integração usam `UPDATE` SQL direto (via
  `ExecuteSqlInterpolated`) para colocar uma `Reservation`/`ReservationSaga` nos estados
  `confirmada`/`cancelada` e `autorizado`/`rejeitado`, em vez de qualquer método de domínio.
  **Racional:** nenhum método de domínio para essas transições existe ainda (pertence a F04); a
  alternativa seria não testar esses estados nesta feature, o que violaria a Métrica de Sucesso do
  PRD ("os três estados... são exercitados nos casos de teste de RF-01").
  **Trade-offs:** dívida técnica documentada — os helpers de seed desta feature devem ser
  revisados/substituídos quando F04 introduzir a transição real, para não haver duas fontes de
  verdade sobre como uma Reservation chega a `confirmada`/`cancelada`.
  **Alternativas rejeitadas:** adicionar um método de domínio "fake" só para testes (ex.:
  `Reservation.ForceStatusForTesting(...)`) — rejeitada por criar uma API pública de mutação que
  parece produção mas não é, um risco maior do que SQL direto isolado no projeto de testes.

- **Decisão:** `reservationId` é recebido como `string` bruta no path (sem constraint `:guid` da
  rota), e a validação de formato acontece no `GetReservationByIdQueryValidator`.
  **Racional:** uma constraint de rota `:guid` faria o próprio ASP.NET Core devolver 404 (rota não
  encontrada) para um valor malformado, o que se sobreporia à distinção 400×404 exigida pelo AC de
  RF-01 e pelo contrato.
  **Trade-offs:** nenhum — é o mesmo nível de controle que F01 já tem sobre suas próprias validações
  de shape via FluentValidation.

### Riscos Conhecidos

- **Estados terminais sem escritor real até F04:** aceito e documentado acima; o risco principal é
  que a TechSpec de F04, ao ser escrita, redefina `SagaState`/`CancellationReason` de forma
  incompatível com o que já existe aqui. Mitigação: a tabela de Análise de Impacto acima já aponta
  este arquivo como referência obrigatória para F04.
- **Leitura inconsistente durante a corrida da saga:** já aceito no PRD (§Riscos e Mitigações) — uma
  consulta entre a publicação de um evento e o processamento do resultado mostra o estado anterior;
  comportamento esperado, sem mitigação nesta feature.
- **`x-backend-notes` do contrato exige que `status` e `sagaStatus` nunca formem uma combinação
  inválida** (ex.: `confirmada` + `pendente`) — esta feature **não pode garantir** essa invariante
  sozinha, porque é somente leitura; a garantia real depende de F04 escrever `status` e `state` de
  forma atômica. Aceito como responsabilidade de F04, fora do escopo desta TechSpec.

### Requisitos Especiais

Não aplicável — sem requisito de performance, segurança adicional (Fase 0 não implementa
autenticação) ou conformidade regulatória.

### Conformidade com Skills

- Segue `dotnet-architecture` (CQRS nativo estendido, Clean Architecture, exceção de domínio
  específica, `ProblemDetails` para erros).
- Segue `dotnet-testing` (unitário para o handler com mocks; integração com
  `WebApplicationFactory`/Testcontainers já configurados por F01).

**Desvios identificados:**

| Desvio | Skill | Justificativa |
|--------|-------|----------------|
| Teste de integração usa `UPDATE` SQL direto para simular estados de saga que nenhum handler real ainda produz | `dotnet-testing` | Ver "Decisões Principais" acima — F04 (a feature que produziria esses estados de verdade) ainda não existe |

---

## Questões em Aberto

- [x] **Ordem de merge** — resolvido: F01 já foi mesclada em `main` (PR #4, commit `fbeea05`,
  2026-09-12), confirmado nesta atualização por comparação direta do código (`git diff` entre a
  branch de origem e `main` para `services/booking/src`, sem diferenças). Deixou de ser uma
  dependência bloqueante.
- [x] **Completar `SagaState`/`CancellationReason` nesta feature (em vez de esperar F04)** —
  confirmado pelo autor nesta revisão, apoiado nas Métricas de Sucesso do PRD.
- [x] **Seed via SQL direto nos testes de integração** — confirmado pelo autor nesta revisão, com a
  dívida técnica documentada (helpers a revisar quando F04 existir).
- [ ] Mesmo conflito de porta de desenvolvimento já resolvido em F01 (não bloqueante, apenas
  reaplicado): `api-contract.yaml` desta feature declara `servers.url` como `http://localhost:5000/v1`,
  mas o ambiente real de Booking usa `:5102` (`tasks/prd-fundacao-fase0`, reafirmado na TechSpec de
  F01). Usar `:5102` na implementação; o `servers.url` do contrato pode ser corrigido num próximo
  ajuste, sem afetar schema/comportamento.
- [ ] Valor exato do limite de `varchar` para `cancellation_reason` (proposto: 500 caracteres) pode
  ser ajustado na implementação sem impacto arquitetural.

---

## Architecture Decision Records

Nenhuma ADR nova é necessária: a extensão do vocabulário de `SagaState`/`CancellationReason` aplica
uma decisão de domínio já registrada em `domains/booking/domain.md` §3 (não uma escolha
arquitetural nova), e a extensão do CQRS nativo para queries reaproveita a decisão de F01 (que já não
gerou ADR própria, por ser escopo local de implementação).

- [ADR-001: Stack de backend — .NET / C# (ASP.NET Core)](../../docs/adr/adr-001-backend-stack-dotnet.md)

---

## Próximos Passos

1. **Implementação:** usar `tsg-flow-task-creator` referenciando esta TechSpec. F01 já está
   disponível em `main` (verificado em 2026-09-12) — sem pré-requisito pendente de merge.
2. **Frontend:** usar `tsg-flow-frontend-techspec-creator` referenciando `api-contract.yaml` e o PRD.
3. **Coordenação com F04:** quando a TechSpec de F04 (Conclusão da Saga) for escrita, apontar este
   documento como referência obrigatória para `SagaState`/`CancellationReason` — não redefinir.
