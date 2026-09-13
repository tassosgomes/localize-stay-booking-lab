# TechSpec: Publicação do dataset reservation_calendar_v1 (Booking F05)

> Modo de operação: API-First (Data Contract; schema-only, sem transporte HTTP)
> PRD de origem: tasks/prd-publicacao-reservation-calendar/prd.md
> API Contract: tasks/prd-publicacao-reservation-calendar/api-contract.yaml (OpenAPI 3.1 como envelope de schema, paths vazio, status Em Revisão)
> Data: 2026-09-13
> Status: Em Revisão
> Handoff: draft — não gerar Tasks
>
> As decisões de produto DP-01, DP-02 e DP-03 estão aprovadas no PRD. As decisões técnicas
> de view regular e de persistência do instante terminal são propostas nesta revisão e estão
> registradas como ADR-004 e ADR-005, ambas ainda Proposed.

---

## Resumo Executivo

Esta TechSpec publica reservation_calendar_v1 como uma view PostgreSQL regular no schema
integration, com uma projeção explícita de sete colunas de booking.reservations. A view filtra somente
as Reservations em confirmada ou cancelada, mantém uma linha por reservation_id, preserva o período
[check_in, check_out) e não faz join nem expõe campos de ReservationSaga, Guest, Payment, preço ou
detalhes técnicos de Booking. Como é uma view ao vivo, a leitura seguinte a um commit de F04 enxerga
o estado atual sem refresh, lote, evento ou componente de runtime novo.

Para cumprir a semântica de updated_at do contrato, F04 precisa persistir
terminal_transition_at em booking.reservations junto com a mudança de status, no mesmo
SaveChangesAsync. A view apenas projeta esse valor como updated_at; não calcula timestamps durante a
leitura ou publicação. O DDL da interface será versionado em db/integration e executado pelo operador
de implantação depois das migrations do schema booking. As roles de serviço receberão apenas
SELECT no objeto de integration, e a existência, qualidade, owner e lineage serão verificados no
OpenMetadata.

O trade-off primário é trocar uma leitura materializada potencialmente mais barata por consistência
imediata e menor complexidade operacional: cada consulta recalcula a projeção sobre a tabela de
Booking e depende da disponibilidade dessa fonte. Essa escolha é adequada à escala de laboratório da
Fase 0; pressão de carga ou necessidade de independência física exigirá uma nova ADR e uma nova
versão/estratégia de publicação, sem alterar v1 silenciosamente.

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|-------|---------|------------------------|
| dotnet-architecture | .agents/skills/dotnet-architecture/SKILL.md | Mantém Clean Architecture e as regras de domínio em Booking; deixa a interface de dados fora de API/Application e exige que a mudança de timestamp seja um handoff explícito para F04 |
| dotnet-dependency-config | .agents/skills/dotnet-dependency-config/SKILL.md | Reutiliza PostgreSQL/EF Core existentes, cria migration somente no schema booking, não adiciona pacote e mantém DDL de integration fora do boot |
| dotnet-testing | .agents/skills/dotnet-testing/SKILL.md | Usa PostgreSQL real via Testcontainers para o contrato de persistência/view, dados determinísticos, isolamento por fixture e testes de unidade para a transição de timestamp em F04 |

Não foram aplicadas skills de frontend, REST, mensageria, performance ou observabilidade de
runtime: F05 não cria UI, endpoint, evento, broker, requisito de carga nem telemetria avançada.
OpenMetadata é uma verificação de governança manual já prevista pelo baseline e pela fundação.

---

## Arquitetura do Sistema

### Visão Geral dos Componentes

- **Booking / Reservation:** continua sendo o owner do estado de Reservation e da origem
  booking.reservations. O campo interno terminal_transition_at é o instante UTC da entrada em
  estado terminal e não faz parte do contrato publicado.
- **F04 — Conclusão da Saga:** é o escritor do estado terminal. Ao executar Confirm ou Cancel, deve
  atualizar status e terminal_transition_at no mesmo aggregate e na mesma unidade de trabalho.
  Eventos duplicados, tardios ou conflitantes não chamam a transição e não reescrevem o timestamp.
- **EF Core / migration do Booking:** adiciona terminal_transition_at como nullable para permitir
  Reservations solicitada e adiciona constraint para exigir o timestamp quando status for
  confirmada ou cancelada. A migration pertence ao schema booking e é aplicada no fluxo de deploy
  do serviço Booking.
- **View publicada:** integration.reservation_calendar_v1 é a única interface de leitura de F05.
  Sua definição usa somente booking.reservations, uma allow-list explícita e o filtro de estados
  terminais. Não é uma entidade EF, não tem repository, não possui endpoint e não é escrita por
  nenhum serviço.
- **Roles consumidoras:** catalog_role, booking_role e payment_role mantêm USAGE e SELECT em
  integration, mas não possuem CREATE em integration nem qualquer acesso ao schema interno de
  outro domínio. O owner do schema/objeto continua sendo o operador de bootstrap, conforme o
  baseline.
- **OpenMetadata:** a ingestion PostgreSQL já está configurada para incluir views e o schema
  integration. Depois do DDL, o ativo deve aparecer com owner lógico Booking, tag do projeto,
  descrição do contrato e lineage de booking.reservations para a view.

### Diagrama de Componentes

    F04 Confirm/Cancel
            |
            | status + terminal_transition_at no mesmo commit
            v
    booking.reservations  --------------------------+
            |                                       |
            | view regular, sem SELECT *            | ingestion PostgreSQL
            v                                       v
    integration.reservation_calendar_v1       OpenMetadata
            |
            | SELECT somente
            v
    catalog_role / booking_role / payment_role
            |
            v
    Consumidores futuros, por exemplo Busca Fase 2

F04 continua sendo o único componente autorizado a decidir o estado terminal. Catalog não consulta
esta view para validar disponibilidade e não recebe qualquer alteração em seu schema nesta feature.

---

## Estratégia de Entrega Incremental

A feature entrega uma interface de dados, não uma jornada HTTP. As fatias abaixo são verticais no
sentido de cada uma produzir uma evidência observável do contrato: primeiro o snapshot correto,
depois sua proteção e governança. O habilitador de timestamp é mantido separado porque pertence ao
escritor de Reservation em F04 e não pode ser substituído por lógica de publicação de F05.

### Mapa de Fatias Verticais

| Slice | Comportamento observável | US/RF/RN cobertos | Entrada → processamento → saída | Artefatos principais | Evidência / checkpoint | Bloqueado por |
|-------|--------------------------|-------------------|--------------------------------|----------------------|-----------------------|---------------|
| V-01 | Uma leitura de integration.reservation_calendar_v1 retorna exatamente as Reservations confirmada/cancelada, com sete colunas contratadas, sem solicitada, sem dados proibidos e com updated_at estável; a mudança terminal persistida aparece na próxima leitura | RF-01, RF-02; RN-01, RN-02, RN-09, RN-10, RN-11; DP-01, DP-02, DP-03 | linha persistida em booking.reservations → view regular filtra/projeta → role autorizada lê uma linha por Reservation | db/integration/001-reservation-calendar-v1.sql; migration/campo de origem do EN-01; ReservationCalendarContractTests; contrato YAML e documentação legível | PostgreSQL 16 descartável: DDL idempotente + teste de schema, elegibilidade, cardinalidade, período, transição pós-commit e timestamp; dotnet test focalizado da suíte de contrato | EN-01 |
| V-02 | As roles de serviço conseguem somente ler a interface publicada, não escrevem nela nem leem booking.*; o ativo e seu lineage aparecem catalogados como Booking no OpenMetadata | RF-03; guardrails do baseline; compatibilidade de versões | bootstrap/DDL → GRANT SELECT explícito → ingestion da view → owner/tag/lineage verificados → v1 permanece estável | db/integration/verify-reservation-calendar.sql; scripts/openmetadata/README.md; ADR-004; ADR-005 | psql contra Postgres descartável prova grants, relkind de view, ausência de CREATE e negação de INSERT/UPDATE; reingestão manual no OpenMetadata confirma dataset, owner, descrição e lineage | V-01 |

### Habilitadores inevitáveis

| Habilitador | Por que não pode fazer parte de uma fatia | Menor escopo | Fatias desbloqueadas |
|-------------|--------------------------------------------|--------------|----------------------|
| EN-01 — instante da transição terminal fornecido por F04 | F05 não pode calcular um instante histórico na view nem assumir a responsabilidade de transicionar Reservation. O contrato exige que updated_at seja gravado com status no commit do owner; portanto a capacidade precisa existir no escritor antes de a view ser liberada | Em F04: Reservation.TerminalTransitionAt nullable; Confirm/Cancel recebem instante UTC e o atribuem uma vez; mapeamento EF e migration em booking; constraint solicitada-null/terminal-non-null; testes de monotonicidade e persistência | V-01 e, por dependência, V-02 |

O EN-01 não adiciona um novo estado de negócio, não muda o ciclo de vida permitido e não publica
terminal_transition_at. Ele corrige a divergência entre o contrato de F05 e o draft atual de F04,
que calcula timestamps no momento da publicação. A alternativa de calcular updated_at na leitura
foi rejeitada e está registrada na ADR-005.

---

## Design de Implementação

### Interfaces Principais

F05 não cria uma interface C# de aplicação. A interface pública é a relation SQL
integration.reservation_calendar_v1 e o seu schema é definido pelo API Contract. Não haverá
IReservationCalendarRepository, controller, endpoint, DTO HTTP ou consumidor dentro do serviço
Booking para servir o dataset; criar um desses componentes criaria uma API fora do escopo.

O DDL deve ser equivalente ao seguinte, sem SELECT * e com a ordem de colunas do contrato:

    CREATE OR REPLACE VIEW integration.reservation_calendar_v1 AS
    SELECT
        r.id AS reservation_id,
        r.accommodation_id,
        r.check_in,
        r.check_out,
        r.status,
        r.created_at,
        r.terminal_transition_at AS updated_at
    FROM booking.reservations AS r
    WHERE r.status IN ('confirmada', 'cancelada');

Regras do script de publicação:

- executar com o operador/owner de integration, depois da migration de Booking, nunca no Program.cs
  nem com booking_role, catalog_role ou payment_role;
- usar DDL transacional e nomes totalmente qualificados;
- executar GRANT SELECT explícito no objeto para as três roles, mesmo que o bootstrap tenha
  default privileges;
- adicionar COMMENT ON VIEW e COMMENT ON COLUMN para permitir que o catálogo preserve a semântica;
- ser idempotente quando executado duas vezes no mesmo banco;
- não usar REFRESH, job, trigger de publicação, Outbox, CDC, broker ou chamada HTTP;
- não alterar a definição de v1 para remover/adicionar colunas; mudança incompatível cria v2.

O view owner executa a leitura da tabela fonte com seus próprios privilégios PostgreSQL. Isso permite
que um consumidor tenha SELECT na view sem USAGE/SELECT em booking, mantendo a fronteira definida pelo
baseline. A solução não usa security_invoker, porque isso reintroduziria dependência de grants no
schema interno para cada consumidor.

### Handoff obrigatório com F04

O contrato entre F04 e F05 deve ser equivalente ao seguinte recorte de domínio, adaptado ao padrão
existente de Reservation:

    public DateTime? TerminalTransitionAt { get; private set; }

    public void Confirm(DateTime terminalTransitionAt)
    {
        EnsureSolicitada();
        Status = ReservationStatus.Confirmada;
        TerminalTransitionAt = EnsureUtc(terminalTransitionAt);
        Saga.MarkAuthorized();
    }

    public void Cancel(string reason, DateTime terminalTransitionAt)
    {
        EnsureSolicitada();
        Status = ReservationStatus.Cancelada;
        TerminalTransitionAt = EnsureUtc(terminalTransitionAt);
        Saga.MarkRejected(reason);
    }

O nome final de EnsureSolicitada/EnsureUtc pode seguir as convenções da implementação, mas o
comportamento é normativo: somente solicitada transiciona; o instante tem Kind UTC; status,
timestamp e Saga são persistidos em uma única unidade de trabalho; repetição de uma transição
terminal não modifica o valor anterior. F04 deve atualizar seu draft/implementação que atualmente
descreve Confirm/Cancel sem timestamp persistido.

### Modelo de persistência da origem

A migration aditiva do serviço Booking deve:

1. adicionar booking.reservations.terminal_transition_at como timestamptz nullable;
2. validar dados existentes antes de instalar a constraint; não preencher uma Reservation terminal
   antiga com created_at, pois isso inventaria o instante de transição;
3. instalar uma constraint equivalente a:

       (status = 'solicitada' AND terminal_transition_at IS NULL)
       OR (status IN ('confirmada', 'cancelada') AND terminal_transition_at IS NOT NULL)

4. atualizar o model snapshot do EF Core;
5. deixar a aplicação da migration como step de deploy do schema booking, nunca no boot.

Se houver Reservation já terminal com timestamp ausente, a migration deve falhar de forma segura e
o operador deve reconciliar os dados antes de habilitar a view. Não há backfill automático nesta
feature. A migration usa a versão já fixada de EF Core/dotnet-ef do repositório e não adiciona
pacote.

### Modelos de Dados

O schema publicado, tipos, enum, exemplos e compatibilidade pertencem a
tasks/prd-publicacao-reservation-calendar/api-contract.yaml. A TechSpec mapeia a origem sem duplicar
o contrato completo:

| Campo publicado | Origem técnica | Tipo PostgreSQL | Regra |
|-----------------|----------------|-----------------|-------|
| reservation_id | Reservation.Id → reservations.id | uuid NOT NULL | chave estável; uma linha |
| accommodation_id | Reservation.AccommodationId → reservations.accommodation_id | uuid NOT NULL | referência opaca ao Catalog |
| check_in | Reservation.CheckIn → reservations.check_in | date NOT NULL | inclusivo |
| check_out | Reservation.CheckOut → reservations.check_out | date NOT NULL | exclusivo; posterior ao check-in |
| status | Reservation.Status → reservations.status | varchar(12) NOT NULL | somente confirmada ou cancelada |
| created_at | Reservation.CreatedAt → reservations.created_at | timestamptz NOT NULL | criação da Reservation |
| updated_at | reservations.terminal_transition_at | timestamptz NOT NULL na view | instante UTC da transição terminal; não recalcular |

ReservationSaga não é fonte de coluna publicada e não participa do SELECT da view. Seu estado
explica como F04 chegou ao resultado terminal, mas expô-lo acoplaria o consumidor à saga. Guest,
GuestsCount, PricePerNight, TotalAmount, Currency, correlation_id, causation_id, PaymentRequestSentAt,
CancellationReason e qualquer coluna futura ficam fora da allow-list.

No modelo de domínio, terminal_transition_at é nullable somente enquanto a Reservation está
solicitada. No dataset, o filtro de status e a constraint de origem garantem que toda linha elegível
tenha updated_at não nulo. O intervalo continua sendo semifechado [check_in, check_out), inclusive
no dia de entrada e exclusivo no dia de saída; a view não expande períodos para noites.

### Endpoints de API

Não aplicável. O arquivo de contrato mantém paths vazio por decisão explícita do PRD: F05 é uma
interface de dados PostgreSQL, não uma operação HTTP. Não haverá autenticação HTTP, paginação,
filtros, response JSON, status code ou ProblemDetails novos.

O mapeamento do contrato é:

| Elemento do contrato | Caminho de implementação |
|----------------------|--------------------------|
| x-data-contract.name = reservation_calendar_v1 | integration.reservation_calendar_v1 |
| recordSchema ReservationCalendarRecord | colunas da view na ordem definida pelo YAML |
| snapshotSchema ReservationCalendarSnapshot | leitura SQL que retorna zero ou mais linhas; consumidor decide a serialização |
| lineage source booking.Reservation | booking.reservations → view; ReservationSaga excluída |

### Versionamento e compatibilidade

- Compatível: comentários, documentação, exemplos e mudanças de conteúdo que preservem as sete
  colunas, tipos, estados, cardinalidade, intervalo e semântica de timestamps.
- Incompatível: renomear/remover/adicionar campo, mudar tipo/enum, publicar histórico, alterar a
  interpretação de cancelada ou mudar o snapshot para uma linha por noite.
- Toda incompatibilidade cria integration.reservation_calendar_v2 ao lado de v1. A view v1 não é
  substituída até existir decisão de compatibilidade e migração dos consumidores.
- A aplicação da view deve falhar se o schema fonte não possuir a coluna necessária, em vez de
  publicar um snapshot parcial ou usar um valor calculado.

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Fatia | Tipo | Skills Aplicáveis | Descrição |
|---------|-------|------|-------------------|-----------|
| db/integration/001-reservation-calendar-v1.sql | V-01 | DDL/Data Contract | dotnet-dependency-config | Cria ou substitui a view regular, documenta colunas e concede SELECT explícito sem permitir escrita das roles de serviço |
| db/integration/verify-reservation-calendar.sql | V-02 | Contract test SQL | dotnet-testing | Verifica relkind view, schema/colunas/tipos, qualidade, grants e negação de escrita/acesso cruzado em Postgres descartável |
| services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationCalendarContractTests.cs | V-01/V-02 | Integration test | dotnet-testing | Testa a view contra PostgreSQL real: estados elegíveis, ausência de solicitada, fidelidade, update após commit, timestamp estável e acesso |
| services/booking/tests/LocalizeStay.Booking.UnitTests/Reservations/ReservationTerminalTransitionTests.cs | EN-01 | Unit test | dotnet-testing | Testa que Confirm/Cancel registram instante UTC e que chamadas fora de solicitada não alteram estado/timestamp; pode ser absorvido pelos testes equivalentes de F04 |
| services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Migrations/*_AddTerminalTransitionAtToReservations.cs | EN-01 | Migration | dotnet-dependency-config | Adiciona terminal_transition_at e a constraint de consistência no schema booking; nome/timestamp definitivos são gerados pelo EF |
| docs/adr/adr-004-reservation-calendar-live-view.md | V-01/V-02 | ADR Proposed | tsg-flow-techspec-creator | Registra view regular, DDL versionado, owner de integração e roles somente leitura |
| docs/adr/adr-005-terminal-transition-timestamp.md | EN-01/V-01 | ADR Proposed | tsg-flow-techspec-creator | Registra a persistência do instante terminal na Reservation e o conflito a resolver com F04 |

Os artefatos EN-01 são pré-requisito de F04/F05. Se F04 for implementada antes desta feature, o
Task Creator deve transformar o handoff em alteração da tarefa de F04 ou confirmar que os mesmos
arquivos já entregues satisfazem o contrato; não deve duplicar migration, propriedade ou teste.

### Arquivos a Modificar

| Caminho | Fatia | Skills Aplicáveis | Alteração |
|---------|-------|-------------------|-----------|
| services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Reservation.cs | EN-01 | dotnet-architecture | Adiciona TerminalTransitionAt e faz os métodos de transição de F04 receberem/persistirem um instante UTC sem alterar RN-10/RN-11 |
| services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/ReservationConfiguration.cs | EN-01 | dotnet-dependency-config | Mapeia TerminalTransitionAt para terminal_transition_at como timestamptz nullable e configura a constraint via migration |
| scripts/openmetadata/README.md | V-02 | — | Adiciona o procedimento F05 para reingerir PostgreSQL, validar integration.reservation_calendar_v1 e registrar/confirmar owner e lineage sem versionar PAT |
| docs/adr/index.md | EN-01/V-01 | tsg-flow-techspec-creator | Reserva ADR-004/ADR-005 globalmente com status Proposed |

### Arquivos de Referência (não alterar nesta TechSpec)

| Caminho | Motivo da Consulta |
|---------|-------------------|
| tasks/prd-publicacao-reservation-calendar/prd.md | Requisitos RF-01/RF-02/RF-03, DP-01/02/03, non-goals e questões abertas |
| tasks/prd-publicacao-reservation-calendar/api-contract.yaml | Fonte única dos tipos, allow-list, qualidade e metadados x-data-contract; paths vazio |
| tasks/prd-publicacao-reservation-calendar/api-contract.md | Representação legível do contrato e checklist de validação Spectral |
| contracts/data-contracts/reservation_calendar_v1.md | Documento durável do dataset, lineage, acesso e histórico de versão; está em revisão do usuário |
| context/architecture-baseline.md | Service-Based Architecture, schema integration, ownership, roles, versionamento e limites da Fase 0 |
| domains/booking/domain.md | Reservation, ReservationSaga, RN-01/RN-02/RN-09/RN-10/RN-11 e dependência F04 → F05 |
| tasks/prd-conclusao-saga/prd.md | Fonte da transição terminal que alimenta o dataset |
| tasks/prd-conclusao-saga/techspec.draft.md | Draft atual de F04; evidencia o conflito dos timestamps calculados na publicação e deve ser alinhado |
| services/booking/src/3-Domain/LocalizeStay.Booking.Domain/Reservations/Reservation.cs | Implementação atual da origem; não possui ainda TerminalTransitionAt |
| services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/Configurations/ReservationConfiguration.cs | Convenção atual de mapeamento EF/Npgsql do schema booking |
| services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Persistence/BookingDbContext.cs | Default schema booking e registro das configurações EF |
| services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Migrations/BookingDbContextModelSnapshot.cs | Snapshot EF que deve ser atualizado pelo EN-01 |
| services/booking/tests/LocalizeStay.Booking.IntegrationTests/CustomWebApplicationFactory.cs | Fixture existente com PostgreSQL 16/Testcontainers e migration real |
| services/booking/tests/LocalizeStay.Booking.IntegrationTests/Reservations/ReservationPersistenceTests.cs | Padrão de seed/leitura SQL do Booking |
| db/bootstrap/003-schemas.sql | Owner de integration permanece com operador; roles de serviço são owners somente dos seus schemas |
| db/bootstrap/004-grants.sql | USAGE/SELECT em integration e ausência de CREATE para as três roles |
| db/bootstrap/verify-grants.sql | Verificação existente da matriz de grants; o teste F05 acrescenta a asserção específica da view |
| scripts/openmetadata/ingestion-postgres.yaml | Ingestion existente com includeViews e filtros para os quatro schemas |
| Directory.Packages.props | Versões centralizadas já usadas por EF Core/Npgsql/Testcontainers; nenhum upgrade novo |
| .config/dotnet-tools.json | Versão fixada do dotnet-ef para a migration |

Os documentos de contrato e o draft de F04 possuem alterações não produzidas por esta TechSpec e
devem ser preservados durante a implementação. A promoção deste draft deve incluir a sincronização
dos seus status/perguntas em uma etapa de contrato própria, sem alterar a allow-list.

---

## Pontos de Integração

### PostgreSQL compartilhado

- **Propósito:** expor o dataset sob integration sem acesso cross-domain ad hoc.
- **Origem:** booking.reservations, propriedade de Booking.
- **Publicação:** operador/owner de integration executa DDL versionado após
  dotnet ef database update do Booking. Nenhuma aplicação escreve em integration.
- **Autenticação/autorização:** a autenticação é a role PostgreSQL da conexão; Fase 0 não possui
  autenticação de usuário. catalog_role, booking_role e payment_role têm SELECT na view e não têm
  CREATE, INSERT ou UPDATE nela.
- **Falhas:** se a migration, a coluna terminal ou a definição da view não estiver disponível,
  a implantação falha; não produzir snapshot parcial. Não há retry, Outbox, CDC ou refresh.
- **Idempotência:** executar o DDL duas vezes deve convergir para a mesma view e grants. Alteração
  incompatível não usa CREATE OR REPLACE em v1; publica v2.
- **Lineage:** booking.reservations → integration.reservation_calendar_v1; ReservationSaga é
  contexto interno e fica fora da projeção.

### OpenMetadata

- **Propósito:** catalogar o ativo, owner de negócio Booking, schema/qualidade/compatibilidade e
  lineage exigidos pelo RF-03.
- **Ingestion:** usar scripts/openmetadata/ingestion-postgres.yaml depois de a view existir; a
  configuração já inclui views e o schema integration.
- **Owner/tag:** usar o owner lógico/time Booking e a tag localize-stay já adotada no laboratório.
  Não é necessário definir uma pessoa como owner nominal para o contrato do domínio; a instância
  real deve confirmar que o time/entidade Booking existe.
- **Lineage:** validar a relação entre booking.reservations e a view. Se o conector não inferir
  lineage a partir da definição SQL, registrar a relação manualmente na UI/API do OpenMetadata
  com o PAT de escopo mínimo do procedimento existente; essa ação não entra no código nem no gate.
- **Segredos:** OM_POSTGRES_PASSWORD, OM_SERVER_HOST_PORT, OM_INGESTION_JWT e eventual
  OPENMETADATA_PAT permanecem somente no ambiente do operador, nunca em arquivo versionado.
- **Checkpoint:** a entrega não é concluída enquanto a view não estiver visível com owner,
  descrição e lineage; o gate automatizado não substitui essa verificação manual contra o
  OpenMetadata real.

---

## Análise de Impacto

| Componente Afetado | Tipo de Impacto | Descrição & Risco | Ação Requerida |
|---------------------|------------------|-------------------|----------------|
| booking.reservations | Modificado aditivo | Nova coluna interna terminal_transition_at e constraint; migration pode falhar com dados terminais sem timestamp | Aplicar EN-01 antes do DDL de integration; não fazer backfill inventado |
| Reservation/F04 | Modificado por dependência | Confirm/Cancel precisam persistir timestamp e status juntos; o draft atual de F04 calcula timestamp na publicação | Alinhar F04 e seus testes; bloquear handoff se o contrato não for aceito |
| integration.reservation_calendar_v1 | Novo | View regular, sete colunas, filtro de dois estados e grants somente leitura; risco de DDL fora de ordem | Executar script após migration; verificar relkind, schema, colunas e grants |
| Roles catalog_role/booking_role/payment_role | Configuração aplicada | Leem a view, continuam sem CREATE em integration e sem acesso a schemas alheios | Reexecutar grants e verificar com SET ROLE/has_table_privilege |
| Booking Application/API | Nenhum runtime novo | Não há endpoint, DTO, handler, repository ou pacote para F05 | Não adicionar camada HTTP nem registrar DI |
| F01–F04 | Dependência upstream | F01 cria Reservations solicitada; F03/F04 conduzem e persistem os estados terminais | Rodar suíte Booking existente e contrato de F04 antes de V-01 |
| Catalog | Nenhuma alteração de domínio | Não usa F05 para disponibilidade nem para Availability Block; só ganha SELECT futuro no contrato | Manter F01 síncrono e eventos de F04 inalterados |
| Payment | Nenhuma alteração | Role payment_role pode ler integration por baseline, sem acesso a booking e sem obrigação de consumir F05 | Nenhuma |
| Contrato reservation_calendar_v1 | Governança | Mudança compatível pode atualizar descrição; incompatível exige v2; YAML e documento Markdown estão Em Revisão/Rascunho | Sincronizar status/perguntas após aprovação desta TechSpec, sem mudar schema sem nova revisão |
| OpenMetadata | Catálogo modificado | Novo dataset/view precisa owner e lineage; conector/instância são externos ao gate | Rodar ingestion e confirmar manualmente no checkpoint V-02 |
| Performance de leitura | Risco potencial | View regular recalcula a projeção e não tem índice/materialização dedicado | Aceitar na Fase 0; medir antes de propor nova estratégia/ADR |

Não há mudança em APIs OpenAPI, AsyncAPI, frontend, autenticação, pagamentos, schemas Catalog/Payment,
RabbitMQ ou observabilidade de runtime.

---

## Abordagem de Testes

### Testes Unitários

O EN-01 deve ter testes unitários no projeto LocalizeStay.Booking.UnitTests, seguindo xUnit, AAA,
nomes descritivos e sem mockar entidades do domínio:

- Confirmar Reservation solicitada registra um instante UTC fornecido e muda status para confirmada;
- Cancelar Reservation solicitada registra um instante UTC fornecido e muda status para cancelada;
- o Saga continua recebendo o resultado correspondente, mas nenhum campo da Saga é projetado no
  dataset;
- chamar Confirm/Cancel fora de solicitada lança a proteção de RN-11 e deixa status e
  TerminalTransitionAt inalterados;
- o timestamp recebido não é substituído por um segundo valor em uma chamada repetida;
- valor sem UTC é normalizado/rejeitado conforme a convenção escolhida por F04, sempre deixando o
  valor persistível como UTC.

Esses testes pertencem primariamente a F04. F05 só pode depender deles como evidência do contrato
de origem; não deve criar uma regra paralela de terminalidade.

### Testes de Integração

ReservationCalendarContractTests deve usar PostgreSQL 16 real via Testcontainers, não SQLite/InMemory:

1. aplicar as migrations do Booking e o script DDL da view em um banco descartável;
2. confirmar que relkind é view e que as colunas são exatamente
   reservation_id, accommodation_id, check_in, check_out, status, created_at, updated_at, na
   ordem e nos tipos do contrato;
3. semear uma Reservation solicitada e confirmar que ela não aparece;
4. semear/produzir uma confirmada e uma cancelada com datas distintas e confirmar exatamente uma
   linha por Reservation, fidelidade de Accommodation/período/status, intervalo
   [check_in, check_out) e updated_at não nulo;
5. mudar uma Reservation solicitada para terminal em uma transação, fazer commit e ler por uma
   conexão nova; a próxima leitura deve incluir a linha com o mesmo terminal_transition_at salvo;
6. repetir a leitura e entradas que F04 classifica como duplicadas/tardias; a view deve continuar
   com uma linha e o mesmo updated_at. A não alteração do estado deve ser coberta pelo teste
   unitário/integrado de F04, não por uma lógica dentro da view;
7. consultar information_schema.columns e garantir que Guest, Payment, preço, moeda,
   correlation/causation, Saga e qualquer coluna interna não estão na interface;
8. executar o DDL duas vezes e confirmar convergência, grants preservados e nenhuma relação
   duplicada;
9. criar roles descartáveis equivalentes a catalog_role, booking_role e payment_role, verificar
   SELECT permitido na view, INSERT/UPDATE/DELETE negados, CREATE negado em integration e SELECT
   direto em booking.reservations negado. Limpar as roles ao final da fixture.

Os testes que verificam a view podem usar a connection string superuser do Testcontainer para
provisionar DDL/roles e conexões separadas para provar o comportamento das roles. Nenhum teste deve
tocar o postgres-main do homelab.

### Testes de Contrato

- Validar o YAML com o comando já documentado no contrato:
  npx --yes @stoplight/spectral-cli lint tasks/prd-publicacao-reservation-calendar/api-contract.yaml --ruleset .agents/skills/tsg-flow-contract-creator/rulesets/openapi.yaml --fail-severity=error
- Não usar Dredd nem criar endpoint fake: paths é vazio e o envelope OpenAPI só fornece schemas.
- Comparar os metadados x-data-contract do YAML com a definição SQL e com a documentação legível:
  nome, versão, owner, lineage, statuses elegíveis, cardinalidade, intervalo, qualidade,
  compatibilidade e roles.
- O teste SQL é a prova de runtime do schema; o teste manual de OpenMetadata é a prova de
  catalogação/lineage e fica explicitamente separado do gate automatizado.

### Gate e evidência

O gate focalizado deve executar o teste de contrato da solução Booking, por exemplo:

    scripts/ai-flow/gate.sh --filter="Booking.ReservationCalendarContractTests"

O script verify-reservation-calendar.sql deve ser executado contra um PostgreSQL 16 descartável
como evidência complementar de DDL/grants. O resultado manual do OpenMetadata deve ser anexado à
revisão da task; ausência de credenciais do homelab não transforma o teste automatizado em falso
positivo.

---

## Sequenciamento de Desenvolvimento

### Build Order

1. **Resolver EN-01 e alinhar F04** — depende de decisão/aprovação de ADR-005; implementar
   TerminalTransitionAt, Confirm/Cancel com instante UTC, migration, constraint e testes. Evidência:
   suíte unitária e migration do Booking verdes.
2. **Publicar a view V-01** — depende de 1 e das migrations de F01–F04 aplicadas; criar
   db/integration/001-reservation-calendar-v1.sql com projeção explícita, comments e grants.
   Evidência: script roda duas vezes sem erro e a view existe no schema integration.
3. **Provar contrato V-01** — depende de 2; criar/executar ReservationCalendarContractTests com
   PostgreSQL real e validar os cenários de RF-01/RF-02. Evidência: teste focalizado verde e
   Spectral sem erros.
4. **Provar proteção V-02** — depende de 2 e 3; executar verify-reservation-calendar.sql,
   incluindo roles/negações, e confirmar que a view não dá acesso às tabelas booking. Evidência:
   assertions SQL verdes.
5. **Catalogar V-02** — depende de 2; executar ingestion-postgres.yaml e confirmar
   localize_stay.integration.reservation_calendar_v1 no OpenMetadata com owner Booking,
   descrição, contrato/tag e lineage. Evidência: checklist manual no ambiente real.

Cada passo posterior declara a dependência e deixa uma evidência independente. Nenhuma etapa cria
endpoint, evento, consumer runtime ou mecanismo de sincronização.

### Dependências Técnicas Bloqueantes

- **F04 precisa ser alinhada antes de F05:** o draft atual de
  tasks/prd-conclusao-saga/techspec.draft.md calcula confirmedAt/cancelledAt na publicação e não
  persiste terminal_transition_at. Esse comportamento não satisfaz updated_at do contrato F05.
  Resolver o conflito no draft/implementação de F04 ou aprovar explicitamente uma alteração
  equivalente antes de promover esta TechSpec.
- **Migration order:** F01–F04 e a migration EN-01 devem estar aplicadas antes do DDL da view.
  O script não pode executar contra uma tabela Booking sem terminal_transition_at.
- **PostgreSQL:** o gate precisa de Docker/Testcontainers; o ambiente real precisa do database
  localize_stay, schema integration e grants do bootstrap já executados.
- **Privilégio de publicação:** o operador do homelab precisa executar DDL como owner de integration
  ou role com CREATE/ownership apropriado. As roles de serviço não recebem essa autoridade.
- **OpenMetadata:** o ambiente real e credenciais de ingestão/PAT são necessários para o checkpoint
  de owner/lineage. A documentação existente trata essa verificação como manual.
- **Owner nominal:** o owner de negócio Booking está fechado; a instância OpenMetadata deve ter uma
  entidade/time Booking ou uma decisão operacional equivalente. Se não existir, o catálogo fica
  pendente mesmo com o gate local verde.

Nenhum pacote NuGet novo, serviço externo de negócio, endpoint ou infraestrutura stateful nova é
necessário.

---

## Monitoramento e Observabilidade

F05 não adiciona logging, métricas, tracing ou health check de runtime. O baseline reserva
OpenTelemetry e métricas formais para a Fase 1; não há um processo de publicação que possa ser
observado por logs.

A visibilidade operacional da entrega é:

- teste SQL bloqueante para cardinalidade, estados, período, timestamp, allow-list e grants;
- comments no objeto/colunas para que o catálogo retenha a semântica;
- OpenMetadata com owner Booking, descrição, qualidade, compatibilidade e lineage;
- registro da execução manual de ingestion e do resultado da verificação no checkpoint da task.

Os campos correlationId e causationId são deliberadamente ausentes da view; nenhum log ou dado
publicado por F05 deve reintroduzi-los. Falha de DDL, migration ou ingestion deve interromper a
etapa correspondente, nunca produzir um snapshot parcial silencioso.

---

## Considerações Técnicas

### Decisões Principais

| Decisão | Estado | Racional |
|---------|--------|----------|
| Classificar F05 como Data Contract cross-domain | Herdada/aprovada no PRD e baseline | O consumidor depende de uma interface de dados, não de endpoint ou evento |
| Publicar uma view regular ao vivo | Nova, ADR-004 Proposed | Satisfaz next-successful-read sem refresh e evita componente de runtime na escala Fase 0 |
| Criar DDL em arquivo SQL versionado fora do boot | Nova, ADR-004 Proposed; ownership herdado do baseline | Integration é schema compartilhado e não pode ser migrado pela role de serviço; operador controla DDL |
| Persistir terminal_transition_at na Reservation | Nova, ADR-005 Proposed | Só Booking conhece o instante da transição; leitura/publicação não pode inventá-lo |
| Usar Reservation.Status como filtro e não ReservationSaga | Fechada pela implementação proposta | Status é o estado público do aggregate; Saga permanece detalhe interno e não precisa ser exposta/joinada |
| Conceder somente SELECT às roles | Herdada do baseline e bootstrap | Consumidores leem o contrato, não escrevem nem acessam booking.* |
| Sem materialização, refresh, Outbox, CDC, retry, push ou histórico | Fechada pelo PRD | São non-goals de F05/Fase 0 e não resolvem requisito presente |

### Liberdade de Implementação

- O nome exato da migration EF e a organização interna da fixture podem seguir as convenções
  existentes, desde que a migration seja aditiva, reversível de forma segura e atualize o snapshot.
- O script pode usar CREATE OR REPLACE VIEW e comments equivalentes, desde que mantenha a ordem,
  tipos e allow-list e conceda SELECT explicitamente.
- O teste pode reutilizar CustomWebApplicationFactory ou uma fixture Postgres dedicada, desde que
  use banco real e isole os dados.
- O owner/lineage pode ser aplicado pela UI ou pela API do OpenMetadata, usando o mecanismo já
  documentado; a evidência final precisa ser verificável na instância.

### Limites e Pendências

- **Aprovação necessária:** ADR-004 e ADR-005 estão Proposed. Esta TechSpec não deve ser promovida
  para techspec.md/Aprovado antes da aprovação dessas escolhas e do alinhamento de F04.
- **Contrato de dados:** o YAML e os documentos Markdown existentes ainda estão em revisão. A
  aprovação deve confirmar as sete colunas e a semântica de updated_at; esta TechSpec não amplia o
  schema.
- **Owner operacional:** Booking é owner de negócio. Falta confirmar a entidade nominal/team
  disponível no OpenMetadata; isso não muda o Data Contract, mas impede o checkpoint de governança.
- **Dados anteriores:** não há política de retenção/arquivamento nem backfill de timestamps no
  escopo. Reservation terminal pré-existente sem timestamp exige reconciliação operacional.
- **Evolução futura:** se o consumidor exigir histórico, linhas por noite, filtro/paginação, push,
  alta escala ou retenção comercial, abrir PRD/versão próprios; não sobrecarregar v1.

### Conflito a resolver antes do handoff

O contrato F05 define updated_at como o instante UTC da transição terminal persistida e estável.
O draft atual de F04 define timestamps no momento da publicação do evento final e não persiste esse
instante. São semânticas diferentes: a primeira é auditável e estável; a segunda pode variar ou
representar o downstream, não o commit de Booking.

A proposta desta TechSpec é manter a regra no owner Booking com
Reservation.TerminalTransitionAt, migration/constraint e uma única unidade de trabalho. A view
deve ser implementada somente após esse contrato ser aceito. Sem essa resolução, V-01 não é
executável e o handoff permanece bloqueado.

### ADRs Afetadas

- [ADR-004 — Publicação do calendário como view PostgreSQL ao vivo](../../docs/adr/adr-004-reservation-calendar-live-view.md) — criada como Proposed nesta revisão.
- [ADR-005 — Persistência do instante da transição terminal da Reservation](../../docs/adr/adr-005-terminal-transition-timestamp.md) — criada como Proposed nesta revisão.
- ADR-001, ADR-002 e ADR-003 são apenas herdadas quando aplicável; não sofrem alteração.

### Critério para promoção

Promover este arquivo para tasks/prd-publicacao-reservation-calendar/techspec.md somente quando:

1. o autor aprovar o draft e as decisões de ADR-004/ADR-005;
2. F04 estiver alinhada para persistir o timestamp no mesmo commit;
3. o contrato YAML/documentação estiverem sincronizados com a decisão de view;
4. as ADRs mudarem para Accepted e o índice refletir o ciclo;
5. o Handoff puder ser marcado approved — pode alimentar o Task Creator.
