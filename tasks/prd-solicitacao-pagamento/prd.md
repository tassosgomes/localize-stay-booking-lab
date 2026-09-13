# Solicitação de Pagamento

## Visão Geral

Depois que uma Reservation é criada com sucesso no estado `solicitada` (F01), ela precisa avançar
para a etapa assíncrona da saga: pedir a Payment que autorize o valor congelado. Esta feature faz
Booking iniciar essa etapa automaticamente — sem que o Guest precise executar qualquer ação
adicional — publicando `booking.payment_requested` com o identificador de correlação da saga e o
valor total e moeda já congelados na Reservation, e registrando na Reservation Saga que a
solicitação foi enviada.

Sem esta feature, toda Reservation criada por F01 ficaria presa indefinidamente em `solicitada`,
sem nenhum caminho para ser confirmada ou cancelada — a saga nunca teria seu segundo passo. Afeta o
Guest (cuja reserva passa a progredir automaticamente rumo a um desfecho), o autor/arquiteto em
estudo (que observa a coreografia de saga via eventos, um dos objetivos centrais da Fase 0) e,
indiretamente, o domínio Payment (que passa a receber o pedido de autorização).

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade selecionada:** não há `backlog/capabilities.md` formal nesta etapa; a feature vem
  diretamente de `domains/booking/domain.md` (F03).
- **Domínio no Domain Map:** Booking — inicia e é dono lógico do fluxo da saga; não decide
  autorização de pagamento (Payment) nem consome o resultado da autorização
  (`context/domain-map.md`, isso é F04).
- **Prioridade e dependências:** Must Have; terceira feature da ordem de implementação sugerida do
  domínio. Depende de F01 já ter criado a Reservation e a Reservation Saga (em `solicitada` /
  pagamento pendente). Habilita F04 (que consome o resultado da autorização desta solicitação).
- **Restrições do baseline:** a saga Booking↔Payment é coreografada via eventos, sem orquestrador
  externo — Booking publica `PaymentRequested` porque é o dono do estado de `Reservation`; todo
  evento da saga carrega `correlationId`/`causationId`; comunicação assíncrona via RabbitMQ
  contratada em AsyncAPI; Fase 0 não implementa autenticação/autorização
  (`context/architecture-baseline.md`).

### Vision Doc

- **Objetivos de negócio atendidos**: praticar coreografia de saga via eventos assíncronos
  contratados (AsyncAPI) entre domínios distintos, um dos objetivos centrais de aprendizado da
  Fase 0 (`vision.md` §1, §4).
- **Restrições globais aplicáveis**: escopo funcional congelado da Fase 0 (pagamento sempre
  simulado); nenhuma tecnologia antecipada de fases futuras (sem Outbox/retry nesta feature — isso
  é F06).
- **Non-Goals globais respeitados**: sem pagamento real, sem múltiplas moedas, sem cancelamento
  voluntário/alteração de reserva, sem autenticação real.

### Domain Doc

- **ID da feature**: F03 — Solicitação de Pagamento (`domains/booking/domain.md`).
- **Entidades envolvidas**: Reservation (lida, não alterada); Reservation Saga (atualizada — passa
  a registrar que a solicitação de pagamento foi enviada).
- **Regras de negócio referenciadas**: RN-06 (o valor solicitado a Payment é noites × preço por
  noite congelado). Esta feature habilita, mas não decide, RN-07 e RN-08 — a decisão de confirmar
  ou cancelar a Reservation a partir do resultado pertence a F04.
- **Dependências upstream**: F01 (Solicitação de Reserva) — cria a Reservation e a Reservation
  Saga em estado inicial de pagamento pendente.
- **Dependências downstream**: F04 (Conclusão da Saga) — consome `payment.payment_authorized` /
  `payment.payment_rejected` correlacionados à solicitação publicada por esta feature.
- **Eventos consumidos**: nenhum.
- **Eventos produzidos**: `booking.payment_requested` — solicita a Payment autorização do valor
  congelado para a Reservation.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Solicitação de pagamento enviada | Registro interno na Reservation Saga indicando que `booking.payment_requested` foi publicado com sucesso para essa saga. Não é um estado adicional exposto na consulta (F02): quem consulta continua vendo apenas "pagamento pendente" até o resultado da autorização chegar (F04). | `domains/booking/domain.md` §3 (atributo "solicitação de pagamento" da Reservation Saga); decisão desta etapa |

---

## Objetivos

- Garantir que toda Reservation criada com sucesso por F01 tenha, automaticamente e sem ação
  adicional do Guest, exatamente uma solicitação de pagamento publicada a Payment.
- Garantir que o valor total e a moeda enviados a Payment sejam sempre idênticos aos congelados na
  Reservation (RN-06), nunca recalculados no momento da publicação.
- Garantir que uma falha ao publicar a solicitação não seja confundida com uma rejeição de regra de
  negócio nem impeça a Reservation já criada de continuar existindo em `solicitada`.
- Critério de conclusão desta feature: para toda Reservation criada com sucesso, existe exatamente
  uma tentativa de publicação de `booking.payment_requested` com dados fiéis aos congelados, e a
  Reservation Saga reflete se essa solicitação foi enviada com sucesso ou não.

---

## Histórias de Usuário

- Como **Guest**, eu quero que minha reserva avance automaticamente para a etapa de pagamento assim
  que for aceita, para não precisar executar nenhuma ação manual adicional.
- Como **autor/arquiteto em estudo**, eu quero observar Booking publicando
  `booking.payment_requested` com o valor congelado e o identificador de correlação da saga, para
  verificar na prática o padrão de saga coreografada via eventos entre domínios independentes.
- Como **domínio Payment** (consumidor do evento), eu quero receber, junto ao pedido, o valor total,
  a moeda e o identificador de correlação da Reservation, sem precisar consultar dados internos de
  Booking, para poder decidir a autorização de forma independente.

---

## Funcionalidades Principais

### RF-01: Iniciar Solicitação de Pagamento

**Descrição**: Assim que Booking cria com sucesso uma Reservation em `solicitada` (F01), inicia
automaticamente a etapa assíncrona da saga: publica `booking.payment_requested` contendo o
identificador de correlação da Reservation Saga e o valor total e a moeda congelados na
Reservation, e registra na Saga que a solicitação de pagamento foi enviada. Nenhuma ação do Guest
ou de qualquer outro solicitante é necessária ou possível para disparar esta etapa manualmente.

**Critérios de Aceitação**:

- **Given** uma Reservation recém-criada em `solicitada`, com preço por noite, moeda e valor total
  congelados, e a Reservation Saga em pagamento pendente sem solicitação enviada (resultado de F01)
  **When** Booking conclui a criação da Reservation
  **Then** Booking publica `booking.payment_requested` contendo o identificador de correlação da
  saga e o valor total e a moeda (BRL) idênticos aos congelados na Reservation, e a Saga passa a
  registrar que a solicitação de pagamento foi enviada — permanecendo, do ponto de vista de quem
  consulta (F02), com situação de saga "pagamento pendente".

- **Given** a publicação de `booking.payment_requested` falha (ex.: broker indisponível ou erro de
  publicação) imediatamente após a Reservation ter sido criada com sucesso
  **When** Booking tenta publicar o evento
  **Then** a Reservation permanece criada em `solicitada`, a Reservation Saga permanece em
  pagamento pendente sem registro de solicitação enviada, a falha é registrada em log para
  investigação manual, e nenhuma nova tentativa automática de publicação ocorre nesta fase —
  limitação conhecida da Fase 0, tratada por retry/Outbox somente em F06.

- **Given** uma solicitação de reserva rejeitada por F01 (nenhuma Reservation nem Reservation Saga
  chegam a ser criadas)
  **When** a rejeição ocorre
  **Then** nenhuma solicitação de pagamento é publicada, pois não existe Reservation nem saga para
  iniciar esta etapa.

**Prioridade**: Must Have

**Rastreabilidade**: RN-06; habilita RN-07 e RN-08 (decididas em F04).

---

## Experiência do Usuário

Esta feature não introduz nenhuma tela ou ação nova no frontend de teste: do ponto de vista do
Guest, o pedido de pagamento acontece de forma transparente, imediatamente após a confirmação de
que a reserva foi solicitada com sucesso (F01). A única forma de observar o efeito desta feature é
indireta, via F02 (Consulta de Reserva) — que continua mostrando a situação da saga como "pagamento
pendente" — e via observação técnica do evento publicado (log correlacionado do baseline ou
inspeção do broker), destinada ao autor/arquiteto em estudo, não ao Guest.

Não há requisito de acessibilidade ou onboarding aplicável, pois não há interface de usuário nova
nesta feature (`context/architecture-baseline.md`).

---

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | A publicação de `booking.payment_requested` é automática: acontece na sequência da criação bem-sucedida da Reservation por F01, sem nenhuma ação explícita do Guest ou de outro solicitante para disparar o pagamento. | Ação explícita separada (um solicitante chama uma operação "solicitar pagamento" para uma Reservation já `solicitada`) — descartada por exigir uma etapa manual e um endpoint não descritos em `domains/booking/domain.md` nem no baseline, e por contradizer a decisão já registrada de saga "coreografada via eventos, não orquestrada por componente externo" com Booking como dono automático do fluxo. | RF-01, Histórias de Usuário, Experiência do Usuário | — |
| DP-02 | Uma falha ao publicar `booking.payment_requested` não desfaz a criação da Reservation (F01 já a persistiu) nem aciona retry automático nesta fase; é registrada como log e aceita como limitação conhecida da Fase 0. | Reverter/cancelar a Reservation já criada quando a publicação falha — descartada por antecipar uma decisão de compensação que `domains/booking/domain.md` já reserva para a F06 (Resiliência da Saga), e por não haver, na Fase 0, nenhum mecanismo (Outbox) que garanta atomicidade entre persistência e publicação. | RF-01 (segundo critério), Riscos e Mitigações, Não-Objetivos | — |

---

## Restrições Técnicas de Alto Nível

- A solicitação é publicada como evento assíncrono via RabbitMQ, contratado em AsyncAPI, carregando
  `correlationId`/`causationId` — consistente com o padrão já adotado para toda a saga
  (`context/architecture-baseline.md`).
- O evento não deve carregar nenhum dado que Payment não precise para decidir a autorização (ex.:
  detalhes de Accommodation ou de Guest) — Payment decide apenas a partir do valor, moeda e
  identificador de correlação.
- Esta feature não introduz nenhum endpoint HTTP novo; a publicação ocorre como continuação do
  mesmo fluxo lógico de criação da Reservation (F01).
- Nenhum dado real de pagamento é processado; valores e identificadores são fictícios.

---

## Não-Objetivos (Fora de Escopo)

- Decidir se o pagamento é autorizado ou rejeitado — pertence exclusivamente a Payment.
- Consumir `payment.payment_authorized` ou `payment.payment_rejected`, ou confirmar/cancelar a
  Reservation a partir desse resultado — pertence a F04 (Conclusão da Saga).
- Criar a Reservation ou a Reservation Saga — pertence a F01 (Solicitação de Reserva).
- Expor qualquer operação para o Guest ou outro solicitante disparar manualmente a solicitação de
  pagamento — a publicação é sempre automática (DP-01).
- Retry, timeout, idempotência robusta, Outbox e DLQ para a publicação — pertencem à F06
  (Resiliência da Saga).
- Cancelamento voluntário pelo Guest/Host, alteração de datas ou hóspedes de uma Reservation já
  solicitada — fora do escopo funcional congelado da Fase 0 (RN-12).
- Autenticação/autorização real, pagamento real, cupons, pricing dinâmico e múltiplas moedas —
  non-goals globais da Vision.

---

## Plano de Rollout Faseado

Feature única e indivisível para efeito de entrega — não há rollout incremental interno a F03: o
objetivo de estudo (coreografia de saga via eventos) só é demonstrado quando o caminho de sucesso e
o caminho de falha de publicação de RF-01 estão implementados juntos.

### MVP (Fase 1)

- **Funcionalidades incluídas**: RF-01 (caminho de sucesso, falha de publicação e o caso de
  reserva rejeitada por F01).
- **Critérios de sucesso para avançar à Fase 2**: não aplicável — não há Fase 2/3 para esta
  feature; a conclusão de RF-01 habilita diretamente F04 do domínio.

---

## Métricas de Sucesso

Como o Localize Stay v2 é um laboratório de estudo pessoal, sem usuários reais nem métricas
comerciais (`vision.md` §1), as métricas de sucesso aqui são de verificação de comportamento, não
de adoção ou engajamento:

- **Cobertura de publicação**: toda Reservation criada com sucesso por F01 resulta em exatamente
  uma tentativa de publicação de `booking.payment_requested` — verificável a 100% pelos casos de
  teste de RF-01.
- **Fidelidade do valor**: o valor total e a moeda publicados no evento são idênticos aos
  congelados na Reservation no momento da criação — verificável a 100% pelos casos de teste de
  RF-01.
- **Isolamento de domínio**: o payload publicado não inclui nenhum dado que Payment não precise
  para decidir a autorização — verificável por revisão de TechSpec/contrato AsyncAPI.

---

## Riscos e Mitigações

- **Risco de inconsistência entre Reservation criada e solicitação não publicada**: se a
  publicação falhar após a Reservation já ter sido persistida por F01, a Reservation fica presa em
  `solicitada` indefinidamente, sem que Payment nunca receba o pedido — Mitigação: aceito como
  limitação conhecida da Fase 0 (DP-02), já alinhada ao risco equivalente registrado em
  `domains/booking/domain.md` §8 para falhas tardias da saga; resolvido apenas pelo Outbox/retry
  da F06.
- **Risco de aprendizado incompleto**: se o valor ou a moeda publicados divergirem dos congelados
  na Reservation (ex.: recálculo indevido no momento da publicação), o objetivo de estudo de
  "congelamento de preço propagado pela saga" não é atingido — Mitigação: o primeiro critério de
  aceitação de RF-01 exige fidelidade exata aos valores já congelados por F01.

---

## Alternativas Consideradas

### Abordagem Escolhida: Publicação automática, na sequência da criação da Reservation

- **Descrição**: Booking publica `booking.payment_requested` automaticamente logo após criar com
  sucesso a Reservation (F01), sem gatilho externo.
- **Por que foi escolhida**: coerente com a decisão já registrada em
  `context/architecture-baseline.md` de que a saga é "coreografada via eventos, não orquestrada por
  um componente externo", com Booking como dono automático do fluxo por ser dono do estado de
  `Reservation`; também coerente com a ordem de implementação sugerida em
  `domains/booking/domain.md` (F01 → F03 como continuação natural, sem endpoint adicional).

### Alternativa Rejeitada: Ação explícita separada para solicitar pagamento

- **Descrição**: um solicitante (Guest ou frontend de teste) chamaria uma operação distinta, do
  tipo "solicitar pagamento", para uma Reservation já `solicitada`.
- **Trade-offs**: tornaria o passo assíncrono mais visível/didático como uma ação isolada, mas
  introduziria uma etapa manual e um endpoint não descritos em nenhum documento upstream, e
  contradiria a coreografia automática já decidida no baseline.
- **Por que foi rejeitada**: decisão confirmada nesta etapa (DP-01); sem base em nenhum documento
  upstream e sem problema atual do laboratório que a justifique.

---

## Questões em Aberto

- Formato exato do payload de `booking.payment_requested` (nomes de campos, tipo do identificador
  de correlação, tipo do evento versionado) fica para a TechSpec/contrato AsyncAPI de F03, que
  herda o identificador de correlação já definido para a Reservation Saga em
  `domains/booking/domain.md` — não bloqueia este PRD.
- Mecanismo de observação da falha de publicação (DP-02) é apenas o logging básico já previsto no
  baseline nesta fase; um mecanismo mais robusto (alerta, retry) só é tratado na F06 — não bloqueia
  este PRD.
- Necessidade de garantir retry confiável da publicação (ex.: padrão Outbox) foi levantada durante
  o discovery desta feature; confirmado que permanece no escopo já reservado a F06 (Resiliência da
  Saga) em `domains/booking/domain.md` e `vision.md` (Fase 1), não sendo antecipada para F03. Fica
  registrado aqui para não ser reaberto sem evidência nova quando a TechSpec de F06 for criada.
