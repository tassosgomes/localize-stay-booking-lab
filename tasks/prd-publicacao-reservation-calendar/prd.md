# Publicação do dataset `reservation_calendar_v1`

## Visão Geral

O Booking precisa compartilhar o calendário de reservas com consumidores que não devem conhecer
suas tabelas internas. Esta feature publica `reservation_calendar_v1` como um dataset contratado,
com os períodos e o estado terminal das Reservations, para que futuros consumidores — por exemplo,
a Busca da Fase 2 — possam consultar esse contexto sem acoplamento direto ao domínio Booking.

O valor desta feature é completar o terceiro estilo de contrato previsto para a Fase 0: além de
OpenAPI e AsyncAPI, o laboratório passa a demonstrar um Data Contract de dados compartilhados. A
feature não cria uma nova jornada para o Guest; ela torna o resultado já produzido por F01–F04
reutilizável, catalogado e protegido pela fronteira de dados de Booking.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade selecionada:** não há `backlog/capabilities.md` formal nesta etapa; a feature vem
  diretamente de `domains/booking/domain.md` (F05).
- **Domínio no Domain Map:** Booking — é dono da Reservation e publica o calendário contratado;
  não mantém disponibilidade de Accommodation, não cria Availability Block e não expõe tabelas
  internas a consumidores (`context/domain-map.md`).
- **Prioridade e dependências:** Should Have; quinta feature da ordem de implementação sugerida do
  domínio. Depende de F01–F04 para que Reservations existam e possam alcançar estados terminais.
  Habilita consumidores futuros do calendário, mas não é o caminho de validação de disponibilidade
  usado por F01.
- **Restrições do baseline:** compartilhamento cross-domain somente por dataset publicado e
  contratado; consumidores não leem o schema interno `booking.*`; o contrato é versionado e uma
  mudança incompatível gera nova versão; o dataset e seu ownership/lineage devem ser catalogados no
  OpenMetadata (`context/architecture-baseline.md`).

### Vision Doc

- **Objetivos de negócio atendidos**: demonstrar um Data Contract real na Fase 0, preservar
  ownership lógico por domínio e permitir evolução de consumidores sem acesso direto ao Booking
  (`vision.md` §1, §4 e §5).
- **Restrições globais aplicáveis**: dados fictícios, uma instância PostgreSQL com ownership lógico
  por schema, contratos explícitos e nenhuma tecnologia de fases futuras antecipada para resolver
  busca ou escala.
- **Non-Goals globais respeitados**: não cria busca Elasticsearch/OpenSearch, não processa
  pagamento real, não implementa autenticação/autorização, não introduz multi-tenancy e não expõe
  dados pessoais reais.

### Domain Doc

- **ID da feature**: F05 — Publicação do dataset `reservation_calendar_v1`
  (`domains/booking/domain.md`).
- **Entidades envolvidas**: Reservation; Reservation Saga apenas informa a situação terminal que
  determina a elegibilidade do registro, sem ser publicada.
- **Regras de negócio referenciadas**: RN-01 (referência à Accommodation), RN-02 (período
  `[check-in, check-out)`), RN-09 (somente a confirmação cria ocupação no Catalog), RN-10 (estado
  terminal observável), RN-11 (estados terminais não regridem nem alternam) e RN-12 (sem
  cancelamento voluntário ou alteração na Fase 0).
- **Dependências upstream**: F04 — Conclusão da Saga, que produz os estados `confirmada` e
  `cancelada`; indiretamente F01–F03, que criam e conduzem a Reservation até esse resultado.
- **Dependências downstream**: futuros consumidores do dataset, como a Busca da Fase 2. Catalog não
  usa este dataset para validar uma reserva nem para criar Availability Block; seu fluxo continua
  baseado na API e nos eventos próprios já definidos.
- **Eventos consumidos**: nenhum. A publicação de um dataset é uma interface de dados, não um evento
  assíncrono.
- **Eventos produzidos**: nenhum.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| `reservation_calendar_v1` | Dataset contratado que apresenta uma linha por Reservation em estado terminal, com a Accommodation e o período da estadia, sem expor dados internos de Booking. | `vision.md` §6; `domains/booking/domain.md` F05; decisão desta feature |
| Reservation terminal | Reservation em `confirmada` ou `cancelada`. Esses são os únicos estados publicados por esta versão do calendário. | `domains/booking/domain.md` §3, RN-10 e RN-11; DP-01 |
| Calendário vigente | Visão atual das Reservations terminais, sem histórico de transições e sem Reservations ainda `solicitada`. | Proposta DP-02 |
| Ocupação ativa | Período de uma Reservation com estado `confirmada`. Uma linha `cancelada` permanece identificável no dataset, mas não deve ser interpretada como data ocupada. | RN-09; DP-01 |

## Objetivos

- Permitir que um consumidor autorizado consulte o calendário de Booking sem ler `booking.*` nem
  depender de estruturas internas de Reservation ou Reservation Saga.
- Representar todas as Reservations em estado terminal exatamente uma vez, com identificador
  estável, Accommodation de referência, período e estado atuais.
- Não publicar Reservations `solicitada`, pois elas ainda não representam uma ocupação confirmada e
  não criam Availability Block na Fase 0.
- Tornar explícito que `confirmada` representa ocupação ativa e `cancelada` representa um desfecho
  terminal sem ocupação ativa, evitando interpretações divergentes pelos consumidores.
- Garantir que uma mudança terminal persistida seja visível no dataset na próxima leitura bem-sucedida,
  sem depender de uma atualização manual ou de um lote diário.
- Critério de conclusão: os casos de teste do contrato demonstram 100% de cobertura dos estados
  elegíveis, zero registro para Reservations `solicitada`, uma linha por Reservation e ausência de
  campos de Guest, Payment ou detalhes internos da Saga.

## Histórias de Usuário

- Como **consumidor futuro de Booking**, eu quero consultar períodos e estados de Reservations por
  Accommodation, para construir uma experiência de busca ou análise sem acessar tabelas internas.
- Como **consumidor de dados**, eu quero distinguir `confirmada` de `cancelada`, para considerar
  somente confirmações como ocupação ativa e não bloquear datas canceladas.
- Como **autor/arquiteto em estudo**, eu quero observar um dataset publicado com Data Contract,
  ownership e lineage catalogados, para verificar na prática a fronteira de dados entre domínios.
- Como **Booking**, eu quero compartilhar somente o mínimo necessário para o calendário, para
  preservar a evolução interna de Reservation, Reservation Saga e Payment sem quebrar consumidores.

## Funcionalidades Principais

### RF-01: Publicar os registros elegíveis do calendário

**Descrição**: `reservation_calendar_v1` apresenta uma linha por Reservation em estado terminal.
Cada linha contém, no mínimo, o identificador estável da Reservation, a referência da Accommodation,
o check-in, o check-out e o estado (`confirmada` ou `cancelada`). O período mantém a semântica
`[check-in, check-out)`. Os nomes e tipos exatos dos campos pertencem ao Data Contract/TechSpec,
mas não podem alterar essa semântica.

**Critérios de Aceitação**:

- **Given** uma Reservation em estado `confirmada`
  **When** um consumidor lê `reservation_calendar_v1`
  **Then** encontra exatamente uma linha para a Reservation, com sua Accommodation, período
  `[check-in, check-out)` e estado `confirmada`, e pode interpretar esse período como ocupação ativa.

- **Given** uma Reservation em estado `cancelada`
  **When** um consumidor lê `reservation_calendar_v1`
  **Then** encontra exatamente uma linha para a Reservation, com seu período e estado `cancelada`,
  e esse registro não representa ocupação ativa.

- **Given** uma Reservation ainda em estado `solicitada`
  **When** um consumidor lê `reservation_calendar_v1`
  **Then** não encontra linha para essa Reservation, porque ela ainda não é uma reserva terminal nem
  cria Availability Block no Catalog.

- **Given** qualquer linha publicada
  **When** o consumidor inspeciona seus dados
  **Then** não encontra Guest de referência, preço, valor total, dados de Payment, identificadores
  de correlação/causação, motivo técnico de rejeição ou qualquer outro detalhe interno da Saga que
  não seja necessário para o calendário.

**Prioridade**: Must Have

**Rastreabilidade**: RN-01, RN-02, RN-09, RN-10, RN-11; DP-01.

---

### RF-02: Manter o snapshot atual do calendário

**Descrição**: O dataset representa o estado terminal atual de cada Reservation, com uma linha
  identificada de forma estável por Reservation. Quando F04 conclui uma Reservation, o dataset
  passa a refletir o resultado persistido; não há histórico de transições nem duplicação de linha.

**Critérios de Aceitação**:

- **Given** uma Reservation `solicitada` cuja conclusão persistida passa a ser `confirmada`
  **When** o consumidor realiza uma leitura bem-sucedida após a transição
  **Then** a Reservation aparece uma única vez, com estado `confirmada` e seus dados de período
  correspondentes.

- **Given** uma Reservation `solicitada` cuja conclusão persistida passa a ser `cancelada`
  **When** o consumidor realiza uma leitura bem-sucedida após a transição
  **Then** a Reservation aparece uma única vez, com estado `cancelada`, sem ser interpretada como
  ocupação ativa.

- **Given** uma Reservation já terminal e um evento duplicado, fora de ordem ou conflitante que não
  altera seu estado por RN-11
  **When** o consumidor lê o dataset
  **Then** continua existindo no máximo uma linha para essa Reservation, sem regressão de estado,
  troca de `confirmada` para `cancelada` (ou vice-versa) e sem registro duplicado.

- **Given** um consumidor que espera atualização em lote, notificação push ou histórico de eventos
  **When** consulta o escopo de `reservation_calendar_v1`
  **Then** entende que o contrato oferece apenas o snapshot atual após a mudança persistida; esses
  comportamentos não fazem parte desta feature.

**Prioridade**: Must Have

**Rastreabilidade**: RN-10, RN-11; DP-02 e DP-03.

---

### RF-03: Proteger e catalogar o contrato de dados

**Descrição**: O calendário é consumido como dataset publicado e versionado, com semântica, qualidade,
  frequência de atualização, acesso de leitura, ownership e lineage documentados. Consumidores não
  precisam nem recebem acesso às tabelas internas de Booking; mudanças incompatíveis não alteram
  `reservation_calendar_v1` em lugar, devendo ser publicadas em nova versão.

**Critérios de Aceitação**:

- **Given** um consumidor autorizado ao contrato
  **When** ele lê o calendário
  **Then** acessa somente a interface publicada, conhece o significado dos estados e campos, e não
  precisa consultar `booking.*` para interpretar uma linha.

- **Given** uma tentativa de escrever no dataset ou de consultar diretamente as tabelas internas de
  Booking por meio deste fluxo
  **When** a tentativa é avaliada
  **Then** ela não é uma operação suportada por F05; a publicação permanece sob ownership de Booking
  e os consumidores têm acesso somente de leitura à interface contratada.

- **Given** uma mudança compatível na descrição ou no conteúdo que preserve a semântica de v1
  **When** o contrato é atualizado
  **Then** `reservation_calendar_v1` permanece a interface consumida e a alteração é documentada no
  histórico de versões.

- **Given** uma mudança incompatível no schema ou na semântica de `reservation_calendar_v1`
  **When** a mudança é publicada
  **Then** uma nova versão é criada ao lado de v1, mantendo v1 estável para consumidores existentes.

- **Given** a entrega de F05 pronta para uso
  **When** o catálogo de ativos é verificado
  **Then** `reservation_calendar_v1` possui owner Booking e seu lineage até o estado de Reservation
  está registrado no OpenMetadata.

**Prioridade**: Must Have

**Rastreabilidade**: Vision Fase 0, baseline — regras de Data Contract e catalogação.

## Experiência do Usuário

F05 não adiciona tela, botão ou ação ao Guest e não altera o fluxo do frontend de teste. A
experiência do consumidor futuro é a de ler uma interface de dados estável: identificar uma
Accommodation, observar o período de uma Reservation e interpretar seu estado sem conhecer o
modelo interno de Booking.

Para o autor/arquiteto em estudo, a experiência inclui localizar o dataset no OpenMetadata, consultar
seu Data Contract e verificar que uma Reservation `solicitada` não aparece, enquanto uma Reservation
`confirmada` ou `cancelada` aparece com semântica explícita. Não há onboarding, atualização em tempo
real para usuários finais ou requisito de acessibilidade de UI aplicável, pois nenhuma interface
visual nova é criada nesta feature.

## Decisões de Produto

> As decisões DP-01, DP-02 e DP-03 foram aprovadas para esta entrega.

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | Publicar as duas situações terminais (`confirmada` e `cancelada`) e omitir `solicitada`. A linha `cancelada` permanece como desfecho terminal, mas não representa ocupação ativa. | Publicar somente `confirmada` simplificaria o cálculo de ocupação, mas esconderia o resultado terminal de cancelamento que F04 precisa tornar observável. Publicar `solicitada` faria uma tentativa ainda não confirmada parecer ocupação e contrariaria RN-09. | RF-01, RF-02, Termos Canônicos, Não-Objetivos | — |
| DP-02 | O dataset é um snapshot atual, com uma linha por Reservation, e não um histórico append-only de transições. A linha usa o identificador da Reservation como referência estável. | Expandir uma linha por noite aumentaria volume e duplicaria uma semântica que já é expressa pelo intervalo; publicar histórico completo ampliaria F05 para auditoria e evolução de saga, que não fazem parte da feature. | RF-01, RF-02, Métricas, Não-Objetivos | — |
| DP-03 | A atualização é efetiva após a transição terminal persistida e deve aparecer na próxima leitura bem-sucedida; não há lote diário, ação manual de republicação ou notificação push. | Atualização diária ou republicação manual deixaria o calendário defasado para consumidores e não agrega valor ao objetivo de demonstrar um dataset contratado atual. Uma notificação assíncrona adicional duplicaria o papel dos eventos de F04. | Objetivos, RF-02, Restrições de Alto Nível, Métricas | — |

## Restrições Técnicas de Alto Nível

- A interface de dados deve ser publicada no ponto de integração previsto pelo baseline e protegida
  por Data Contract; a decisão entre view e materialized view, o schema exato e as roles pertencem à
  TechSpec.
- Consumidores não acessam tabelas internas de Booking, não escrevem no dataset e não dependem de
  nomes ou estruturas internas que não estejam no contrato publicado.
- O Data Contract deve declarar schema, qualidade, frequência de atualização, acesso, retenção e
  compatibilidade de versões. A versão incompatível deve ser publicada como nova versão, nunca por
  alteração destrutiva de v1.
- A catalogação no OpenMetadata deve registrar ao menos o dataset, owner Booking e o lineage até os
  dados de Reservation.
- Não há novo endpoint HTTP nem novo evento de domínio nesta feature; a publicação é a interface de
  dados compartilhados do Booking.
- A Fase 0 não processa PII real nem dados reais de pagamento. Guest, Payment e detalhes internos da
  Reservation Saga ficam fora do dataset.

## Não-Objetivos (Fora de Escopo)

- Validar disponibilidade no momento da criação de uma Reservation; F01 continua usando a chamada
  síncrona contratada com Catalog.
- Substituir `available_accommodations_v1` ou criar um dataset de disponibilidade de Catalog.
- Criar, remover ou reconciliar Availability Block; Catalog reage aos eventos de F04 dentro de sua
  própria fronteira.
- Publicar Reservations `solicitada` como ocupação, bloquear datas ou reservar inventário antes da
  confirmação.
- Expor histórico completo de transições, eventos da Saga, auditoria operacional ou dados de
  Payment.
- Criar API de busca, filtros, paginação, notificações push, streaming ou uma integração direta com
  a Busca da Fase 2.
- Definir retenção comercial, arquivamento ou exclusão de Reservations; não existe exclusão de
  Reservation no escopo funcional da Fase 0.
- Implementar retry, Outbox, CDC, broker adicional, Elasticsearch/OpenSearch, autenticação,
  autorização ou observabilidade avançada.
- Alterar o ciclo de vida da Reservation, permitir cancelamento voluntário ou alterar datas e
  quantidade de hóspedes.

## Plano de Rollout Faseado

F05 é uma entrega única de governança e compartilhamento de dados. Não há valor em liberar apenas
parte do dataset: ele só cumpre seu objetivo quando a semântica, o contrato, a proteção de acesso e
a catalogação estão disponíveis em conjunto.

### MVP (entrega da Fase 0)

- **Funcionalidades incluídas**: RF-01, RF-02 e RF-03.
- **Critérios de sucesso para avançar à Fase 2**: 100% dos testes de elegibilidade, atualização,
  unicidade, ausência de dados internos e compatibilidade do contrato aprovados; dataset e lineage
  visíveis no OpenMetadata. A Busca da Fase 2 pode consumir o contrato, mas sua implementação não é
  critério de conclusão de F05.

### Fase 2

- **Funcionalidades adicionais**: nenhuma dentro de F05. Um consumidor de busca pode ser criado em
  PRD próprio, sem alterar o contrato v1 sem revisão de compatibilidade.
- **Critérios de sucesso para avançar à Fase 3**: não aplicável a esta feature.

### Fase 3 (Conjunto Completo)

- **Funcionalidades restantes**: nenhuma planejada para F05; novas necessidades exigem nova versão
  ou novo PRD.
- **Critérios de sucesso de longo prazo**: preservar consumidores existentes durante qualquer
  evolução e publicar uma nova versão quando a semântica de v1 deixar de ser suficiente.

## Métricas de Sucesso

Como o Localize Stay v2 é um laboratório de estudo sem usuários reais, as métricas são de
verificação do comportamento do contrato:

- **Cobertura de elegibilidade**: 100% das Reservations `confirmada` e `cancelada` têm exatamente
  uma linha no dataset; 100% das Reservations `solicitada` não têm linha — até a conclusão do MVP.
- **Fidelidade do período e estado**: 100% das linhas verificadas preservam identificador,
  Accommodation, check-in, check-out, intervalo `[check-in, check-out)` e estado terminal da
  Reservation — até a conclusão do MVP.
- **Atualização do snapshot**: 100% das transições terminais dos cenários de aceitação aparecem na
  próxima leitura bem-sucedida após a persistência — durante os testes do MVP.
- **Unicidade e monotonicidade**: 0 linhas duplicadas por Reservation e 0 regressões ou trocas de
  estado após duplicidade/conflito — durante os testes do MVP.
- **Minimização de dados**: 0 ocorrências de Guest, Payment, correlation/causation IDs ou detalhes
  internos da Saga nas colunas contratadas — na validação do Data Contract.
- **Governança**: 100% dos metadados obrigatórios de owner Booking, descrição, qualidade, acesso,
  compatibilidade e lineage registrados no OpenMetadata — antes de marcar F05 como concluída.

## Riscos e Mitigações

- **Consumidor interpreta `cancelada` como ocupação**: uma linha cancelada continua visível para
  preservar o resultado terminal, mas não bloqueia datas — Mitigação: campo de estado obrigatório,
  definição de Ocupação ativa no contrato, exemplos de dados e teste que considera somente
  `confirmada` como ocupação.
- **Consumidor usa F05 como fonte da validação síncrona de disponibilidade**: isso poderia introduzir
  defasagem no caminho crítico de criação de reserva — Mitigação: declarar no contrato e no README
  que F01 usa a API de Catalog; F05 é para consumidores independentes.
- **Ausência de consumidor real na Fase 0 reduz feedback de usabilidade do contrato**: o dataset
  pode ficar formalmente correto, mas pouco claro para um leitor futuro — Mitigação: fixture com os
  três estados de Reservation, exemplos de interpretação e registro de lineage no OpenMetadata.
- **Exposição acidental de campos internos**: adicionar dados de Guest, Payment ou Saga por
  conveniência pode acoplar consumidores ao Booking — Mitigação: allow-list semântica no PRD/Data
  Contract e teste de ausência de campos proibidos.
- **Necessidade futura de histórico ou retenção diferente**: consumidores podem pedir auditoria ou
  séries históricas além do snapshot — Mitigação: manter o escopo atual explícito; tratar a
  necessidade em novo PRD e, se necessário, nova versão/dataset, sem sobrecarregar v1.

## Alternativas Consideradas

### Abordagem Escolhida: Snapshot terminal contratado, uma linha por Reservation

- **Descrição**: publicar `confirmada` e `cancelada`, omitir `solicitada`, manter uma linha por
  Reservation com o período `[check-in, check-out)` e atualizar o snapshot depois da transição
  terminal persistida. O Data Contract descreve a semântica e a fronteira de acesso.
- **Por que foi escolhida**: reflete os estados terminais habilitados por F04, evita tratar uma
  solicitação ainda não confirmada como ocupação, atende o objetivo de Data Contract da Fase 0 e
  permite que consumidores distingam desfecho de ocupação ativa sem acessar Booking internamente.

### Alternativa Rejeitada 1: Publicar somente Reservations `confirmada`

- **Descrição**: o dataset conteria apenas períodos atualmente ocupados; uma Reservation cancelada
  desapareceria completamente.
- **Trade-offs**: simplificaria consumidores interessados exclusivamente em ocupação, mas esconderia
  o estado terminal de cancelamento, dificultaria observar o fechamento completo da saga e não
  refletiria integralmente os estados terminais de F04.
- **Por que foi rejeitada**: a Fase 0 precisa demonstrar tanto sucesso quanto falha da saga, e o
  estado `cancelada` deve continuar distinguível de ausência de uma Reservation.

### Alternativa Rejeitada 2: Publicar Reservations `solicitada` junto com as terminais

- **Descrição**: toda Reservation apareceria desde sua criação, com o estado atual alterando a
  interpretação do registro.
- **Trade-offs**: permitiria acompanhar a jornada desde o início, mas faria consumidores de
  calendário receberem períodos que não são ocupados e poderia reproduzir a janela que RN-09
  deliberadamente aceita entre solicitação e confirmação.
- **Por que foi rejeitada**: `reservation_calendar_v1` representa o resultado estável/terminal para
  consumidores independentes, não o monitoramento da saga; F02 já atende a consulta de uma
  Reservation solicitada.

### Alternativa Rejeitada 3: Dataset histórico append-only de eventos e transições

- **Descrição**: publicar uma linha para cada mudança de estado, incluindo timestamps e eventos da
  Saga, para permitir auditoria completa.
- **Trade-offs**: teria valor para auditoria e reprocessamento, mas aumentaria o escopo, exporia
  detalhes de integração e misturaria o calendário atual com um histórico operacional.
- **Por que foi rejeitada**: F05 precisa demonstrar um dataset de estado contratado; histórico de
  transições, retries e resiliência pertencem a necessidades futuras e não ao escopo da Fase 0.

## Questões em Aberto

- **Nomes, tipos e regras de qualidade de cada campo do Data Contract** — responsável: autor da
  TechSpec/contrato; prazo desejável: antes da criação de `contracts/data-contracts/reservation_calendar_v1.md`;
  impacto se não resolvido: impede a validação técnica do contrato, mas não altera o comportamento
  de produto proposto neste PRD.
- **Escolha entre view e materialized view, roles e mecanismo de atualização** — responsável: autor
  da TechSpec; prazo desejável: durante a especificação técnica; impacto se não resolvido: impede a
  implementação do requisito de atualização após commit, mas não é uma decisão de produto deste
  PRD.
- **Responsável nominal pelo ativo no OpenMetadata** — responsável: autor do laboratório; prazo
  desejável: antes do rollout do MVP; impacto se não resolvido: deixa a governança operacional
  incompleta, embora o owner de negócio Booking já esteja definido.

