# ADR-005: Persistência do instante da transição terminal da Reservation

## Status

Accepted

## Identidade e escopo

- Caminho: docs/adr/adr-005-terminal-transition-timestamp.md
- Domínios/componentes afetados: Booking Reservation, migration do schema booking e Data Contract reservation_calendar_v1
- Origem histórica: tasks/prd-publicacao-reservation-calendar
- Substitui: Nenhuma

## Data

2026-09-13

## Contexto

O Data Contract de reservation_calendar_v1 exige updated_at como o instante UTC em que a Reservation
entrou no estado terminal atual. O valor precisa ser gravado na mesma transação que muda status, ser
obrigatório para linhas confirmada/cancelada e permanecer estável em leituras e reprocessamentos.

Reservation já possui created_at, mas não possui um timestamp de transição terminal. A publicação no
momento da leitura ou no momento de um eventual evento downstream não representa o instante da decisão
persistida. O draft atual de F04 calcula timestamps na publicação, o que é incompatível com esta exigência
do contrato e precisa ser alinhado antes do handoff de F05.

## Decisão

Booking persistirá um campo interno nullable terminal_transition_at em booking.reservations, mapeado em
Reservation como DateTime? e armazenado como timestamptz. Os métodos de F04 que confirmam ou cancelam uma
Reservation receberão um instante UTC, atribuirão terminal_transition_at uma única vez e atualizarão
status no mesmo aggregate antes de um único SaveChangesAsync.

Uma constraint de banco permitirá terminal_transition_at nulo somente para solicitada e exigirá valor
não nulo para confirmada ou cancelada. A view de F05 fará alias desse campo para updated_at. O caminho
de duplicidade, atraso ou conflito de F04 não chama o método de transição e não reescreve o timestamp.

## Alternativas Consideradas

### Alternativa 1: Calcular updated_at na view ou na leitura

- **Descrição:** usar now() na view, ou o instante em que o consumidor consulta o dataset.
- **Prós:** não exige coluna ou migration adicional.
- **Contras:** o valor muda entre leituras e não representa a transição persistida.
- **Por que rejeitada:** viola diretamente a semântica de auditoria e a estabilidade exigidas pelo contrato.

### Alternativa 2: Usar o timestamp do evento de Payment

- **Descrição:** copiar authorizedAt ou rejectedAt para o campo publicado.
- **Prós:** reaproveita uma informação já presente no fluxo assíncrono.
- **Contras:** o evento pode chegar atrasado, pode não representar o commit em Booking e não é uma fonte
  comum para todos os caminhos de transição ou testes de recuperação.
- **Por que rejeitada:** o owner do estado terminal é Booking; o instante do commit de Booking deve ser
  produzido pelo próprio domínio.

## Consequências

### Positivas

- updated_at é determinístico, auditável e estável entre consultas.
- A constraint protege a qualidade do contrato mesmo diante de escrita SQL acidental.
- F04 e F05 compartilham uma fonte de verdade sem expor ReservationSaga ou detalhes de Payment.

### Negativas

- F04 precisa alterar sua interface de transição e seus testes para fornecer o instante UTC.
- A migration é bloqueante para publicar a view e não pode inventar timestamps para Reservations terminais
  antigas; dados existentes nessa condição devem ser reconciliados antes da aplicação da constraint.
- Há uma coluna interna adicional que não faz parte da allow-list pública.

### Riscos

- Se F04 persistir status e timestamp em saves separados, a view poderá observar uma linha inválida;
  o contrato de implementação exige uma única unidade de trabalho e a constraint deve falhar a escrita
  incompleta.
- Relógios locais incorretos podem produzir instantes inválidos; a aplicação deve normalizar para UTC,
  e a validação operacional deve rejeitar valores sem UTC.

## Notas de Implementação

A migration deve adicionar terminal_transition_at como nullable, aplicar a constraint depois de validar
dados existentes e atualizar o model snapshot do EF Core. O view script deve ser aplicado somente após
essa migration. A definição exata dos métodos de F04 pode preservar o padrão atual de métodos de domínio
com timestamp explícito; esta ADR não introduz TimeProvider nem pacote novo.

## Referências

- context/architecture-baseline.md, seções Propriedade dos Dados e Guardrails Arquiteturais
- domains/booking/domain.md, RN-10 e RN-11
- tasks/prd-publicacao-reservation-calendar/api-contract.yaml, x-data-contract.quality.terminal-transition-timestamp
- tasks/prd-publicacao-reservation-calendar/prd.md, RF-02 e DP-03
