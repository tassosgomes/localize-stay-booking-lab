# Conclusão da Saga

## Visão Geral

Depois que Booking publica `booking.payment_requested` (F03), uma Reservation fica em `solicitada`
com a Reservation Saga aguardando o resultado do pagamento — e permanece assim indefinidamente até
que alguém feche esse ciclo. Esta feature faz Booking reagir ao resultado publicado por Payment:
consumindo `payment.payment_authorized`, confirma a Reservation e publica
`booking.reservation_confirmed`; consumindo `payment.payment_rejected`, cancela a Reservation com um
motivo registrado e publica `booking.reservation_cancelled`. Nenhuma ação do Guest ou de qualquer
solicitante é necessária.

Sem esta feature, nenhuma Reservation alcançaria um estado terminal: o critério de conclusão da Fase
0 ("uma reserva percorre o fluxo completo de sucesso e de falha") nunca seria demonstrado, Catalog
nunca receberia o evento que habilita a criação do Availability Block, e Notification nunca teria o
que notificar. Afeta o Guest (que finalmente sabe se sua hospedagem está garantida ou não), o
autor/arquiteto em estudo (que observa o fechamento da coreografia de saga) e, indiretamente, Catalog
e Notification (que dependem do resultado publicado aqui para agir).

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade selecionada:** não há `backlog/capabilities.md` formal nesta etapa; a feature vem
  diretamente de `domains/booking/domain.md` (F04).
- **Domínio no Domain Map:** Booking — dono do estado final da Reservation e da Reservation Saga;
  não decide autorização/rejeição de pagamento (Payment), não cria o Availability Block (Catalog) e
  não entrega notificações (Notification) — apenas publica o resultado que habilita essas ações
  (`context/domain-map.md`).
- **Prioridade e dependências:** Must Have; quarta feature da ordem de implementação sugerida do
  domínio. Depende de F03 (Reservation e Reservation Saga já existentes, com o pedido de pagamento
  publicado). Habilita F05 (`reservation_calendar_v1`, que deve refletir estados terminais) e é
  aprofundada por F06 (Resiliência da Saga), sem alterar a fronteira desta entrega.
- **Restrições do baseline:** saga coreografada via eventos, sem orquestrador externo — Booking
  reage a `PaymentAuthorized`/`PaymentRejected` para confirmar ou cancelar, por ser dono do estado de
  `Reservation`; todo evento da saga carrega `correlationId`/`causationId`; comunicação assíncrona
  via RabbitMQ contratada em AsyncAPI; Fase 0 não implementa autenticação/autorização; toda entrada
  (incluindo eventos consumidos) é tratada como não confiável (`context/architecture-baseline.md`).

### Vision Doc

- **Objetivos de negócio atendidos**: fechar a demonstração completa da saga coreografada — sucesso
  (autorização → confirmação) e falha (rejeição → cancelamento) — que é parte do critério de
  conclusão da Fase 0 (`vision.md` §4).
- **Restrições globais aplicáveis**: escopo funcional congelado da Fase 0; nenhuma tecnologia
  antecipada de fases futuras (sem Outbox, retry, DLQ ou compensação de pagamento autorizado nesta
  feature — isso é F06).
- **Non-Goals globais respeitados**: sem pagamento real, sem múltiplas moedas, sem cancelamento
  voluntário/alteração de reserva, sem autenticação real.

### Domain Doc

- **ID da feature**: F04 — Conclusão da Saga (`domains/booking/domain.md`).
- **Entidades envolvidas**: Reservation (alterada — atinge estado final `confirmada` ou
  `cancelada`); Reservation Saga (atualizada — passa a registrar o resultado recebido e, quando
  aplicável, o motivo do cancelamento).
- **Regras de negócio referenciadas**: RN-07, RN-08, RN-09, RN-10, RN-11.
- **Dependências upstream**: F03 (Solicitação de Pagamento) — publica `booking.payment_requested` e
  deixa a Reservation Saga aguardando resultado.
- **Dependências downstream**: F05 (Publicação do dataset `reservation_calendar_v1`) — passa a ter
  estados terminais reais para expor; F06 (Resiliência da Saga) — aprofunda timeout, retry,
  idempotência robusta e compensação sobre o mesmo fluxo. Fora de Booking: Catalog (cria o
  Availability Block ao consumir `booking.reservation_confirmed`) e Notification (notifica as
  partes ao consumir o resultado final).
- **Eventos consumidos**: `payment.payment_authorized` (de: Payment); `payment.payment_rejected`
  (de: Payment).
- **Eventos produzidos**: `booking.reservation_confirmed`; `booking.reservation_cancelled`.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Evento não correlacionável | Evento de resultado de pagamento cujo identificador de correlação não corresponde a nenhuma Reservation Saga conhecida por Booking. Não é tratado como erro de sistema, apenas ignorado para fins de negócio e registrado em log. | Decisão desta etapa |
| Resultado tardio para saga concluída | Evento de resultado de pagamento que chega correlacionado a uma Reservation Saga cuja Reservation já está em estado terminal (`confirmada` ou `cancelada`), seja por duplicidade de entrega, seja por reprocessamento. Não altera o estado já alcançado (RN-11); tratamento mais profundo (compensação, alerta) é reservado à F06. | `domains/booking/domain.md` §8-9; decisão desta etapa |

---

## Objetivos

- Garantir que toda Reservation cuja Reservation Saga esteja aguardando resultado alcance
  exatamente um estado terminal (`confirmada` ou `cancelada`) assim que o resultado de pagamento
  correlacionado for recebido, sem qualquer ação manual.
- Garantir que cada resultado final produza exatamente um evento correspondente
  (`booking.reservation_confirmed` ou `booking.reservation_cancelled`), habilitando Catalog e
  Notification a agir de forma confiável.
- Garantir que uma Reservation já em estado terminal nunca regrida nem alterne para o outro estado
  terminal, mesmo diante de eventos duplicados, fora de ordem ou não correlacionáveis (RN-11).
- Critério de conclusão desta feature: para toda Reservation Saga aguardando resultado que recebe um
  evento de pagamento correlacionado válido, a Reservation atinge o estado terminal correspondente e
  o evento de resultado correspondente é publicado exatamente uma vez; nenhum evento adicional
  (duplicado, fora de ordem ou não correlacionável) altera um estado já terminal ou gera nova
  publicação.

---

## Histórias de Usuário

- Como **Guest**, eu quero que minha reserva seja confirmada automaticamente assim que o pagamento
  for autorizado, para saber que posso contar com a hospedagem sem precisar consultar ativamente.
- Como **Guest**, eu quero que minha reserva seja cancelada automaticamente, com um motivo
  registrado, quando o pagamento for rejeitado, para entender o que houve e decidir o próximo passo.
- Como **autor/arquiteto em estudo**, eu quero observar Booking reagindo a
  `payment.payment_authorized`/`payment.payment_rejected` e publicando o evento final
  correspondente, para verificar na prática o fechamento de uma saga coreografada via eventos.
- Como **domínio Catalog** (consumidor), eu quero receber `booking.reservation_confirmed` apenas
  quando o pagamento realmente foi autorizado, para criar o Availability Block com confiança de que
  a reserva é definitiva.
- Como **domínio Notification** (consumidor), eu quero receber o resultado final da Reservation
  exatamente uma vez por saga, para notificar as partes interessadas sem duplicidade.

---

## Funcionalidades Principais

### RF-01: Confirmar Reservation após Autorização de Pagamento

**Descrição**: Ao consumir `payment.payment_authorized` correlacionado a uma Reservation Saga que
ainda aguarda resultado, Booking transiciona a Reservation para `confirmada`, registra na Saga que o
resultado recebido foi autorização, e publica `booking.reservation_confirmed`. Nenhuma ação do Guest
ou de outro solicitante dispara ou impede esta transição.

**Critérios de Aceitação**:

- **Given** uma Reservation em `solicitada`, com a Reservation Saga aguardando resultado de
  pagamento (F03)
  **When** Booking consome `payment.payment_authorized` correlacionado a essa saga
  **Then** a Reservation passa para `confirmada`, a Saga registra o resultado como autorizado, e
  Booking publica `booking.reservation_confirmed` com os dados necessários para Catalog criar o
  Availability Block e para Notification notificar as partes.

- **Given** uma Reservation cuja Saga já está em estado terminal (`confirmada` ou `cancelada`)
  **When** chega um novo `payment.payment_authorized` correlacionado à mesma saga (entrega duplicada
  ou fora de ordem)
  **Then** a Reservation permanece no estado terminal já alcançado, a Saga não é alterada, nenhum
  novo `booking.reservation_confirmed` é publicado, e a ocorrência é registrada em log para
  investigação (RN-11).

- **Given** um `payment.payment_authorized` cujo identificador de correlação não corresponde a
  nenhuma Reservation Saga conhecida por Booking
  **When** Booking consome esse evento
  **Then** o evento é ignorado para fins de negócio (nenhuma Reservation é alterada) e a ocorrência é
  registrada em log, sem interromper o processamento de outras mensagens.

**Prioridade**: Must Have

**Rastreabilidade**: RN-07, RN-09, RN-10, RN-11.

---

### RF-02: Cancelar Reservation após Rejeição de Pagamento

**Descrição**: Ao consumir `payment.payment_rejected` correlacionado a uma Reservation Saga que
ainda aguarda resultado, Booking transiciona a Reservation para `cancelada`, registra na Saga que o
resultado recebido foi rejeição junto com um motivo de cancelamento, e publica
`booking.reservation_cancelled`. A rejeição não autoriza Booking a alterar ou reverter nada em
Payment.

**Critérios de Aceitação**:

- **Given** uma Reservation em `solicitada`, com a Reservation Saga aguardando resultado de
  pagamento (F03)
  **When** Booking consome `payment.payment_rejected` correlacionado a essa saga
  **Then** a Reservation passa para `cancelada`, a Saga registra o resultado como rejeitado com um
  motivo de cancelamento associado à rejeição do pagamento, e Booking publica
  `booking.reservation_cancelled` com os dados necessários para Catalog e Notification.

- **Given** uma Reservation cuja Saga já está em estado terminal (`confirmada` ou `cancelada`)
  **When** chega um novo `payment.payment_rejected` correlacionado à mesma saga (entrega duplicada,
  fora de ordem, ou conflitante com uma autorização já processada para a mesma saga)
  **Then** a Reservation permanece no estado terminal já alcançado, a Saga não é alterada, nenhum
  novo `booking.reservation_cancelled` é publicado, e a ocorrência é registrada em log para
  investigação (RN-11).

- **Given** um `payment.payment_rejected` cujo identificador de correlação não corresponde a nenhuma
  Reservation Saga conhecida por Booking
  **When** Booking consome esse evento
  **Then** o evento é ignorado para fins de negócio (nenhuma Reservation é alterada) e a ocorrência é
  registrada em log, sem interromper o processamento de outras mensagens.

**Prioridade**: Must Have

**Rastreabilidade**: RN-08, RN-09, RN-10, RN-11.

---

### RF-03: Isolar Falha de Publicação do Resultado Final

**Descrição**: Se a publicação de `booking.reservation_confirmed` ou `booking.reservation_cancelled`
falhar depois que a Reservation e a Saga já foram atualizadas internamente para o estado terminal
correspondente, essa falha não desfaz nem reverte a transição já decidida — o estado terminal já é o
resultado de negócio válido, apenas a notificação a outros domínios não chegou.

**Critérios de Aceitação**:

- **Given** Booking decidiu confirmar ou cancelar uma Reservation a partir de um resultado de
  pagamento válido, e a atualização interna do estado já foi concluída
  **When** a publicação do evento final (`booking.reservation_confirmed` ou
  `booking.reservation_cancelled`) falha (ex.: broker indisponível)
  **Then** a Reservation e a Saga permanecem no estado terminal já decidido, a falha de publicação é
  registrada em log para investigação manual, e nenhuma nova tentativa automática de publicação
  ocorre nesta fase — limitação conhecida da Fase 0, tratada por retry/Outbox somente em F06.

**Prioridade**: Must Have

**Rastreabilidade**: RN-10, RN-11.

---

## Experiência do Usuário

Esta feature não introduz nenhuma tela ou ação nova no frontend de teste: do ponto de vista do
Guest, a confirmação ou o cancelamento acontecem de forma transparente, assim que Payment decide o
resultado. A única forma de observar o efeito desta feature é indireta: via F02 (Consulta de
Reserva), que passa a mostrar `confirmada`/situação "pagamento autorizado" ou
`cancelada`/situação "pagamento rejeitado" com motivo — e via observação técnica dos eventos
publicados (log correlacionado do baseline ou inspeção do broker), destinada ao autor/arquiteto em
estudo, não ao Guest.

Não há requisito de acessibilidade ou onboarding aplicável, pois não há interface de usuário nova
nesta feature (`context/architecture-baseline.md`).

---

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | O motivo de cancelamento registrado na Saga e exposto por F02 reflete o fato de negócio "pagamento rejeitado", não um detalhe técnico interno de Payment repassado sem tratamento. | Propagar integralmente o payload de `payment.payment_rejected` como motivo — descartada porque acopla Booking à estrutura interna do payload de Payment antes de existir um contrato AsyncAPI para esse domínio, e porque RN-08 já estabelece que a rejeição não autoriza Booking a decidir ou repassar detalhes internos de Payment; Booking apenas reflete o fato "rejeitado" em linguagem de negócio. | RF-02, Termos Canônicos, Experiência do Usuário | — |
| DP-02 | Para satisfazer RN-11 nesta fase, Booking verifica o estado atual da Reservation antes de aplicar uma transição: se já terminal, o evento de resultado é ignorado (nenhuma nova transição, nenhuma nova publicação), sem implementar um mecanismo de deduplicação robusto (ex.: registro de eventos processados, Outbox). | Implementar deduplicação/idempotência completa (dedup store, Outbox) já nesta feature — descartada por antecipar escopo explicitamente reservado a F06 (Resiliência da Saga) em `domains/booking/domain.md` §8, quando a verificação de estado atual já é suficiente para garantir estados terminais monotônicos (RN-11) nesta fase. | RF-01 (2º critério), RF-02 (2º critério), Termos Canônicos, Riscos e Mitigações | — |
| DP-03 | Um evento de resultado de pagamento cujo identificador de correlação não corresponde a nenhuma Reservation Saga conhecida é ignorado para fins de negócio e registrado em log, sem falhar o processamento nem interromper outras mensagens. | Rejeitar/falhar o consumo do evento — descartada porque poderia travar o processamento de outras mensagens da fila por um evento potencialmente originado de dados de outro ambiente ou teste manual do laboratório; tratamento robusto de mensagens não processáveis (DLQ) é reservado à F06. | RF-01 (3º critério), RF-02 (3º critério), Termos Canônicos | — |
| DP-04 | Uma falha ao publicar o evento final (`booking.reservation_confirmed`/`booking.reservation_cancelled`) não desfaz a transição de estado já persistida na Reservation/Saga; é registrada como log e aceita como limitação conhecida da Fase 0, análoga à decisão já tomada em F03 para a publicação de `booking.payment_requested`. | Reverter a transição de estado já decidida quando a publicação falha — descartada por antecipar uma decisão de compensação reservada à F06, e por não haver, na Fase 0, mecanismo (Outbox) que garanta atomicidade entre persistência e publicação. | RF-03, Riscos e Mitigações, Não-Objetivos | — |

---

## Restrições Técnicas de Alto Nível

- O resultado é consumido e publicado como eventos assíncronos via RabbitMQ, contratados em
  AsyncAPI, carregando `correlationId`/`causationId` — consistente com o padrão já adotado para toda
  a saga (`context/architecture-baseline.md`).
- Esta feature não introduz nenhum endpoint HTTP novo; a reação ao resultado de pagamento é
  inteiramente orientada a evento.
- Os eventos publicados (`booking.reservation_confirmed`/`booking.reservation_cancelled`) não devem
  carregar nenhum dado que Catalog ou Notification não precisem para agir (ex.: detalhes internos do
  processamento de pagamento).
- Nenhum dado real de pagamento é processado; valores e identificadores são fictícios.

---

## Não-Objetivos (Fora de Escopo)

- Decidir se o pagamento é autorizado ou rejeitado — pertence exclusivamente a Payment; Booking
  apenas reage ao resultado já decidido.
- Criar, atualizar ou remover o Availability Block em Catalog — Booking apenas publica o evento que
  habilita Catalog a agir de forma idempotente sobre seus próprios dados.
- Entregar notificações ao Guest ou Host — Booking apenas publica o evento final consumido por
  Notification.
- Retry, timeout, deduplicação robusta (dedup store), Outbox e DLQ para consumo e publicação de
  eventos — pertencem à F06 (Resiliência da Saga).
- Tratamento de resultado tardio que exija reabrir uma decisão já compensada, ou solicitar estorno a
  Payment quando uma autorização não puder resultar em confirmação — questões em aberto reservadas
  explicitamente à F06 (`domains/booking/domain.md` §9).
- Reconciliar duas Reservations que disputaram a mesma Accommodation antes do bloqueio do Catalog —
  risco aceito e demonstrado na Fase 0, reconciliação/compensação fica para F06.
- Cancelamento voluntário pelo Guest/Host, alteração de datas ou hóspedes de uma Reservation já
  solicitada — fora do escopo funcional congelado da Fase 0 (RN-12).
- Autenticação/autorização real, pagamento real, cupons, pricing dinâmico e múltiplas moedas —
  non-goals globais da Vision.

---

## Plano de Rollout Faseado

Feature única e indivisível para efeito de entrega — não há rollout incremental interno a F04: o
objetivo de estudo (fechamento da saga coreografada, sucesso e falha) só é demonstrado quando
confirmação, cancelamento e a proteção de estados terminais monotônicos (RN-11) estão implementados
juntos.

### MVP (Fase 1)

- **Funcionalidades incluídas**: RF-01, RF-02 e RF-03 (caminhos de autorização, rejeição, evento
  duplicado/fora de ordem, evento não correlacionável e falha de publicação).
- **Critérios de sucesso para avançar à Fase 2**: não aplicável — não há Fase 2/3 para esta feature;
  a conclusão de RF-01/RF-02/RF-03 fecha o ciclo de vida da saga da Fase 0 e habilita F05.

---

## Métricas de Sucesso

Como o Localize Stay v2 é um laboratório de estudo pessoal, sem usuários reais nem métricas
comerciais (`vision.md` §1), as métricas de sucesso aqui são de verificação de comportamento, não de
adoção ou engajamento:

- **Cobertura de conclusão**: toda Reservation Saga aguardando resultado que recebe um evento de
  pagamento correlacionado válido atinge um estado terminal e tem exatamente um evento de resultado
  publicado — verificável a 100% pelos casos de teste de RF-01 e RF-02.
- **Monotonicidade de estado terminal**: nenhum caso de teste de evento duplicado, fora de ordem ou
  conflitante resulta em regressão de estado, alternância entre terminais ou publicação duplicada —
  verificável a 100% pelos casos de teste de RF-01/RF-02 (2º critério).
- **Isolamento de correlação**: eventos não correlacionáveis nunca alteram o estado de nenhuma
  Reservation existente — verificável a 100% pelos casos de teste de RF-01/RF-02 (3º critério).

---

## Riscos e Mitigações

- **Risco de disputa pela mesma Accommodation entre duas Reservations concorrentes**: como o
  Availability Block só é criado por Catalog após `booking.reservation_confirmed` (RN-09), duas
  Reservations podem ter sido validadas contra a mesma disponibilidade antes que a primeira seja
  confirmada e bloqueada — Mitigação: aceito e demonstrado como limitação conhecida da Fase 0
  (`domains/booking/domain.md` §8); reconciliação/compensação é decisão de F06.
- **Risco de estado divergente entre Payment e Booking após falha de publicação**: se
  `booking.reservation_confirmed`/`cancelled` falhar ao publicar após a Reservation já ter sido
  atualizada internamente, Catalog e Notification nunca saberão do resultado até uma nova tentativa
  manual — Mitigação: aceito como limitação conhecida da Fase 0 (DP-04); resolvido apenas pelo
  Outbox/retry da F06.
- **Risco de motivo de cancelamento pouco informativo**: registrar apenas o fato de negócio
  "pagamento rejeitado" (DP-01), sem detalhe técnico de Payment, pode limitar o valor didático de
  observar por que um pagamento foi rejeitado — Mitigação: aceito nesta fase; o motivo pode ser
  enriquecido sem quebrar este PRD quando o domínio Payment tiver seu próprio contrato AsyncAPI
  detalhado.

---

## Alternativas Consideradas

### Abordagem Escolhida: Reação coreografada a eventos de resultado, com verificação de estado atual para garantir estados terminais monotônicos

- **Descrição**: Booking consome `payment.payment_authorized`/`payment.payment_rejected`
  correlacionados, verifica se a Reservation Saga ainda aguarda resultado e, em caso afirmativo,
  aplica a transição terminal correspondente e publica o evento final; caso contrário (saga já
  terminal ou evento não correlacionável), ignora o evento e registra em log.
- **Por que foi escolhida**: coerente com a decisão já registrada em
  `context/architecture-baseline.md` de saga "coreografada via eventos, não orquestrada por um
  componente externo", com Booking como dono do fluxo por ser dono do estado de `Reservation`;
  satisfaz RN-11 (estados terminais monotônicos) sem antecipar a infraestrutura de idempotência
  robusta reservada à F06.

### Alternativa Rejeitada: Orquestrador central decidindo confirmação/cancelamento

- **Descrição**: um componente orquestrador externo chamaria Booking e Payment de forma síncrona
  para decidir e aplicar o resultado final da saga.
- **Trade-offs**: centralizaria a lógica de decisão em um único lugar, mas introduziria um
  componente e um padrão de comunicação (orquestração síncrona) não descritos em nenhum documento
  upstream.
- **Por que foi rejeitada**: contradiz diretamente a decisão já registrada no baseline arquitetural
  de que a saga Booking↔Payment é coreografada via eventos, sem orquestrador externo.

### Alternativa Rejeitada: Implementar deduplicação/idempotência robusta (dedup store, Outbox) já nesta feature

- **Descrição**: além de verificar o estado atual da Reservation, Booking manteria um registro
  persistente de eventos já processados e usaria Outbox para garantir atomicidade entre persistência
  e publicação.
- **Trade-offs**: tornaria a conclusão da saga mais resiliente a falhas de infraestrutura desde já,
  mas introduziria complexidade e componentes (Outbox, dedup store) explicitamente reservados à F06
  em `domains/booking/domain.md` §8, sem um problema atual desta feature que os justifique — a
  verificação de estado atual já é suficiente para o critério de conclusão desta feature (RN-11).
- **Por que foi rejeitada**: decisão confirmada nesta etapa (DP-02); antecipa escopo e tecnologia sem
  problema atual que a justifique (regra do roadmap da Vision).

---

## Questões em Aberto

- Formato exato do payload de `payment.payment_authorized`/`payment.payment_rejected` (nomes de
  campos, se inclui um motivo técnico detalhado de rejeição) depende do contrato AsyncAPI do domínio
  Payment, que ainda não tem `domain.md` formal neste repositório — fica para a TechSpec/contrato de
  F04, que deve tratar o payload recebido de forma defensiva quanto a campos além do
  identificador de correlação. Não bloqueia este PRD (DP-01 já define o piso do motivo de negócio
  exposto).
- Nível de log e eventual alerta para "evento não correlacionável" e "resultado tardio para saga
  concluída" ficam para a TechSpec, seguindo apenas o logging básico já previsto no baseline nesta
  fase; um mecanismo mais robusto (alerta, métrica) só é tratado na F06. Não bloqueia este PRD.
- Enriquecimento futuro do motivo de cancelamento com detalhe de Payment (quando esse domínio tiver
  contrato próprio) não está decidido e não precisa ser decidido agora (DP-01); revisitar sem reabrir
  DP-01 sem evidência nova. Responsável: autor do laboratório, sem prazo definido.
