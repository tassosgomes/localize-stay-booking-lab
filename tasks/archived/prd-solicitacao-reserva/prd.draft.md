# Solicitação de Reserva

## Visão Geral

Um Guest que encontrou uma Accommodation no catálogo precisa registrar formalmente o desejo de
ocupá-la em um período específico. Esta feature permite que um Guest solicite uma Reservation
informando a Accommodation, o período (check-in/check-out) e a quantidade de hóspedes; Booking
valida essa solicitação de forma síncrona contra o Catalog (existência, status ativo, capacidade e
disponibilidade), congela o preço vigente e cria a Reservation no estado inicial `solicitada`.

É o ponto de entrada de todo o fluxo de negócio do laboratório: sem uma Reservation solicitada com
sucesso, não há saga de pagamento (F03/F04) nem dataset de calendário (F05) para demonstrar. Do
ponto de vista de estudo, é também a primeira integração síncrona entre domínios (Booking → Catalog)
a ser exercitada via contrato OpenAPI, um dos objetivos centrais da Fase 0.

Afeta o Guest (que solicita), o autor/arquiteto em estudo (que observa o comportamento) e o frontend
de teste (que envia a solicitação e exibe o resultado).

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade selecionada:** não há `backlog/capabilities.md` formal nesta etapa; a feature vem
  diretamente de `domains/booking/domain.md` (F01).
- **Domínio no Domain Map:** Booking — orquestra o ciclo de vida da Reservation e a saga com
  Payment; não mantém catálogo nem decide pagamento (`context/domain-map.md`).
- **Prioridade e dependências:** Must Have; primeira feature da ordem de implementação sugerida do
  domínio, sem dependência de outra feature de Booking. Depende de Catalog já expor consulta
  síncrona de existência/status/capacidade/disponibilidade/preço (Catalog F04, upstream).
- **Restrições do baseline:** serviço próprio em .NET/C#, schema `booking.*`; validação de
  disponibilidade é chamada síncrona via OpenAPI a Catalog (não o dataset `available_accommodations_v1`);
  toda entrada externa é tratada como não confiável e validada; Fase 0 não implementa
  autenticação/autorização (`context/architecture-baseline.md`).

### Vision Doc

- **Objetivos de negócio atendidos**: praticar contrato síncrono formal entre domínios (OpenAPI) e
  ownership de dados (Booking não acessa tabelas de Catalog) — objetivo de aprendizado da Fase 0
  (`vision.md` §1, §4).
- **Restrições globais aplicáveis**: escopo funcional congelado da Fase 0 (reserva simples,
  pagamento simulado); nenhuma tecnologia antecipada de fases futuras.
- **Non-Goals globais respeitados**: sem pagamento real, sem múltiplas moedas, sem cancelamento
  voluntário/alteração de reserva, sem autenticação real.

### Domain Doc

- **ID da feature**: F01 — Solicitação de Reserva (`domains/booking/domain.md`).
- **Entidades envolvidas**: Reservation, Reservation Saga (criada em estado inicial de pagamento
  pendente, ainda sem interação com Payment nesta feature).
- **Regras de negócio referenciadas**: RN-01, RN-02, RN-03, RN-04, RN-05, RN-06, RN-10 (parcial —
  apenas o estado inicial), RN-12. Também Catalog RN-02, RN-03, RN-04, RN-06 (herdadas via
  `domains/catalog/domain.md`, aplicadas do ponto de vista de Booking como consumidor).
- **Dependências upstream**: Catalog — existência, status, capacidade, disponibilidade, preço por
  noite da Accommodation para o período solicitado (chamada síncrona).
- **Dependências downstream**: F02 (Consulta de Reserva) passa a poder exibir a Reservation criada;
  F03 (Solicitação de Pagamento) consome uma Reservation em `solicitada` para iniciar a saga.
- **Eventos consumidos**: nenhum.
- **Eventos produzidos**: `booking.reservation_requested` (sem consumidor obrigatório na Fase 0).

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Guest de referência | Identificador informado por quem solicita a reserva; não é autenticado nem validado contra uma identidade real nesta fase. | PD-002, `domains/booking/domain.md` |
| Moeda do laboratório | Real brasileiro (BRL); única moeda usada em todo valor monetário do sistema, sem seleção ou conversão. | PD-001 |
| Preço congelado | Preço por noite e moeda registrados na Reservation no momento da solicitação; não muda mesmo que o Catalog altere o preço depois. | `domains/booking/domain.md` RN-05 |

---

## Objetivos

- Demonstrar, de ponta a ponta, uma chamada síncrona contratada (OpenAPI) entre dois domínios do
  laboratório, com Booking nunca lendo dados internos de Catalog.
- Garantir que toda Reservation criada tenha preço, moeda e valor total congelados de forma
  imutável a partir da resposta do Catalog no momento da solicitação (RN-05, RN-06).
- Garantir que nenhuma Reservation seja criada quando a solicitação viola uma regra de negócio
  (período inválido, capacidade excedida, acomodação inexistente/inativa/indisponível).
- Critério de conclusão desta feature: um Guest consegue solicitar uma reserva válida e recebê-la
  criada em estado `solicitada`, e cada caminho de rejeição definido abaixo é observável e não deixa
  Reservation nenhuma criada.

---

## Histórias de Usuário

- Como **Guest**, eu quero solicitar uma reserva informando a Accommodation, o período e o número
  de hóspedes, para que meu pedido seja registrado com o preço vigente garantido.
- Como **Guest**, eu quero ser informado imediatamente quando meu pedido não pode ser aceito (período
  inválido, capacidade excedida, acomodação indisponível), para que eu ajuste minha solicitação sem
  esperar por uma etapa posterior.
- Como **autor/arquiteto em estudo**, eu quero que a validação de disponibilidade aconteça via
  chamada síncrona contratada a Catalog, para que eu possa observar e documentar esse padrão de
  integração no laboratório.
- Como **frontend de teste**, eu quero enviar a solicitação de reserva e exibir o resultado
  (sucesso com dados congelados, ou rejeição com motivo), para que a jornada seja verificável
  manualmente.

---

## Funcionalidades Principais

### RF-01: Solicitar Reserva

**Descrição**: Um Guest solicita uma Reservation para uma Accommodation existente, informando
período (check-in, check-out) e número de hóspedes. Booking valida a solicitação de forma síncrona
com Catalog e, se válida, cria a Reservation em estado `solicitada` com preço, moeda e valor total
congelados; publica `booking.reservation_requested`. Se qualquer regra de negócio for violada,
nenhuma Reservation é criada e o Guest recebe o motivo da rejeição.

**Critérios de Aceitação**:

- **Given** uma Accommodation ativa, com capacidade suficiente para o número de hóspedes informado
  e disponível para todo o período solicitado, e um período com check-out posterior ao check-in
  **When** o Guest solicita a reserva
  **Then** Booking cria a Reservation em estado `solicitada`, registra preço por noite, moeda (BRL)
  e valor total (noites × preço por noite) devolvidos pelo Catalog no momento da solicitação,
  publica `booking.reservation_requested` e retorna ao solicitante a confirmação com identificador e
  estado da Reservation criada.

- **Given** um período em que o check-out não é posterior ao check-in (incluindo check-in igual a
  check-out)
  **When** o Guest solicita a reserva
  **Then** a solicitação é rejeitada por período inválido, nenhuma Reservation é criada e nenhuma
  chamada de negócio a Catalog ou Payment ocorre.

- **Given** um número de hóspedes menor ou igual a zero
  **When** o Guest solicita a reserva
  **Then** a solicitação é rejeitada por quantidade de hóspedes inválida e nenhuma Reservation é
  criada.

- **Given** uma Accommodation que não existe ou cuja Property/Accommodation não está ativa
  **When** o Guest solicita a reserva
  **Then** a solicitação é rejeitada por acomodação indisponível e nenhuma Reservation é criada.

- **Given** uma Accommodation ativa cujo número de hóspedes solicitado excede sua capacidade máxima
  informada pelo Catalog
  **When** o Guest solicita a reserva
  **Then** a solicitação é rejeitada por capacidade excedida e nenhuma Reservation é criada.

- **Given** uma Accommodation ativa e com capacidade suficiente, mas indisponível em algum trecho do
  período solicitado segundo o Catalog
  **When** o Guest solicita a reserva
  **Then** a solicitação é rejeitada por indisponibilidade no período e nenhuma Reservation é
  criada.

- **Given** a validação síncrona com Catalog não pode ser concluída (Catalog indisponível ou retorna
  erro inesperado)
  **When** o Guest solicita a reserva
  **Then** nenhuma Reservation é criada e o solicitante é informado de que o pedido não pôde ser
  processado no momento, sem que isso seja interpretado como rejeição de negócio (essa reserva pode
  ser tentada novamente pelo solicitante).

**Prioridade**: Must Have

**Rastreabilidade**: RN-01, RN-02, RN-03, RN-04, RN-05, RN-06, RN-10, RN-12; Catalog RN-02, RN-03,
RN-04, RN-06.

---

## Experiência do Usuário

O Guest (via frontend de teste) preenche Accommodation, datas de check-in/check-out e número de
hóspedes, e envia a solicitação. Em caso de sucesso, o frontend exibe a Reservation criada com seu
identificador, estado `solicitada` e os valores congelados (preço por noite, moeda, valor total),
deixando claro que o pagamento ainda não foi solicitado (isso é F03). Em caso de rejeição, o
frontend exibe o motivo de forma direta o suficiente para o Guest corrigir a solicitação (período,
capacidade, disponibilidade) ou entender que não foi possível validar no momento (falha ao consultar
Catalog).

Não há requisito de acessibilidade além do já aplicável ao frontend de teste como ferramenta de
laboratório (`context/architecture-baseline.md`); não há onboarding — o uso é direto por quem já
conhece o propósito de estudo do sistema.

---

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | O laboratório usa uma única moeda fixa (BRL) para todo valor monetário; não há campo de moeda variável por Accommodation. | Moeda por Accommodation retornada dinamicamente pelo Catalog — descartada por contradizer o non-goal explícito de múltiplas moedas da Vision. | RF-01 (registro de preço/moeda), Termos Canônicos | PD-001 |
| DP-02 | O Guest de referência é um identificador informado por quem solicita a reserva, sem autenticação nem validação de identidade nesta fase. | Modelar Identity/Guest já na Fase 0 — descartada por antecipar tecnologia sem problema atual (regra do roadmap); bloquear a feature até existir identidade — descartada por inviabilizar o escopo congelado. | RF-01 (dado de entrada "Guest de referência"), Termos Canônicos | PD-002 |
| DP-03 | A criação do Availability Block em Catalog não faz parte desta feature; ocorre apenas após a confirmação da Reservation (F04), fora do escopo de F01. | Bloquear a data já na solicitação — descartada nesta etapa porque a decisão já está registrada em `domains/booking/domain.md` RN-09 e `domains/catalog/domain.md` RN-05, não é uma escolha em aberto deste PRD. | Non-Goals, Riscos | — |

---

## Restrições Técnicas de Alto Nível

- A validação de existência/status/capacidade/disponibilidade/preço é obtida de Catalog por chamada
  síncrona contratada em OpenAPI, nunca por leitura do dataset publicado `available_accommodations_v1`
  nem por acesso a tabelas internas de Catalog (`context/architecture-baseline.md`).
- Todo dado de entrada (Accommodation, período, número de hóspedes, Guest de referência) é tratado
  como não confiável e validado antes de qualquer efeito (higiene básica de serviço).
- Nenhum dado real de pagamento ou PII real é processado; valores e identificadores são fictícios.

---

## Não-Objetivos (Fora de Escopo)

- Iniciar a solicitação de pagamento, avançar ou compensar a saga — pertence a F03 (Solicitação de
  Pagamento) e F04 (Conclusão da Saga).
- Consultar o estado de uma Reservation já criada — pertence a F02 (Consulta de Reserva).
- Criar ou remover Availability Block em Catalog — ocorre apenas após confirmação (F04), do lado de
  Catalog (F05 do domínio Catalog), nunca nesta feature.
- Cancelamento voluntário pelo Guest/Host, alteração de datas ou de hóspedes de uma Reservation já
  solicitada — fora do escopo funcional congelado da Fase 0 (RN-12).
- Retry, timeout, idempotência de rede e reconciliação de solicitações concorrentes sobre a mesma
  Accommodation — tratados na Fase 1 (F06 — Resiliência da Saga); nesta feature, duas solicitações
  concorrentes podem ambas passar na validação síncrona antes que qualquer confirmação bloqueie a
  data em Catalog (janela de corrida conhecida e aceita nesta fase, já registrada como risco em
  `domains/booking/domain.md` §8).
- Autenticação/autorização real de Guest, pagamento real, cupons, pricing dinâmico e múltiplas
  moedas — non-goals globais da Vision.

---

## Plano de Rollout Faseado

Feature única e indivisível para efeito de entrega — não há rollout incremental interno a F01: a
feature só demonstra o objetivo de estudo (integração síncrona contratada com congelamento de
preço) quando o caminho de sucesso e todos os caminhos de rejeição de RF-01 estão implementados
juntos.

### MVP (Fase 1)

- **Funcionalidades incluídas**: RF-01 (caminho de sucesso e todos os caminhos de rejeição
  descritos).
- **Critérios de sucesso para avançar à Fase 2**: não aplicável — não há Fase 2/3 para esta feature;
  a conclusão de RF-01 habilita diretamente F02 e F03 do domínio.

---

## Métricas de Sucesso

Como o Localize Stay v2 é um laboratório de estudo pessoal, sem usuários reais nem métricas
comerciais (`vision.md` §1), as métricas de sucesso aqui são de verificação de comportamento, não de
adoção ou engajamento:

- **Corretude do caminho de sucesso**: toda solicitação que satisfaz RN-01 a RN-06 resulta em uma
  Reservation criada em `solicitada` com preço/moeda/total congelados idênticos aos devolvidos por
  Catalog no momento da chamada — verificável a 100% pelos casos de teste de RF-01.
- **Corretude dos caminhos de rejeição**: toda solicitação que viola período, capacidade,
  existência/status ou disponibilidade não cria nenhuma Reservation — verificável a 100% pelos
  casos de teste de RF-01.
- **Isolamento de domínio**: nenhuma chamada de Booking a Catalog ocorre fora do contrato OpenAPI
  definido (sem leitura de schema/tabela interna) — verificável por revisão de código/TechSpec.

---

## Riscos e Mitigações

- **Risco de aprendizado incompleto**: se a validação síncrona for implementada de forma frouxa
  (ex.: Booking assumindo dados sem checar a resposta real de Catalog), o objetivo de estudo de
  contrato formal não é atingido — Mitigação: os critérios de aceitação de RF-01 exigem que cada
  rejeição venha de fato da resposta de Catalog, não de suposição local.
- **Risco de descontinuidade do projeto**: por ser esforço pessoal e paralelo, a feature pode ficar
  parcialmente implementada — Mitigação: já registrada em `vision.md` §7 (ausência de prazos
  rígidos); não é tratada novamente aqui.

Riscos técnicos de concorrência entre solicitações (condição de corrida sobre a mesma Accommodation)
já estão documentados em `domains/booking/domain.md` §8 e não são reabertos aqui; são aceitos como
limitação conhecida da Fase 0 (ver Não-Objetivos).

---

## Alternativas Consideradas

### Abordagem Escolhida: Validação síncrona pontual via OpenAPI, com preço congelado na criação

- **Descrição**: Booking chama Catalog de forma síncrona no momento da solicitação, usa a resposta
  para validar e para congelar preço/moeda/total na Reservation.
- **Por que foi escolhida**: já é a decisão registrada em `context/architecture-baseline.md`
  (distinção entre validação síncrona pontual e dataset publicado `available_accommodations_v1`) e
  em `domains/booking/domain.md` RN-04/RN-05; esta feature apenas a implementa, não a decide de
  novo.

### Alternativa Rejeitada: Validar via leitura do dataset publicado `available_accommodations_v1`

- **Descrição**: Booking consultaria o dataset publicado em vez de chamar Catalog sincronamente.
- **Trade-offs**: dataset publicado é adequado para consumidores amplos e independentes do ciclo de
  vida da reserva (ex.: futura Busca), mas pode estar defasado em relação ao estado real de
  disponibilidade no instante exato da solicitação.
- **Por que foi rejeitada**: já descartada no baseline arquitetural por not garantir a consistência
  pontual necessária para decidir se uma Reservation pode ser criada agora; mantida aqui apenas para
  registro de rastreabilidade, não como decisão reaberta nesta feature.

---

## Questões em Aberto

- Formato exato de identificação da Accommodation e do Guest de referência na chamada (ex.: tipo de
  identificador) fica para a TechSpec de F01, que já herda o contrato síncrono com Catalog — não
  bloqueia este PRD.
- PD-001 e PD-002 estão como `Proposed`; devem ser marcados `Accepted` após a aprovação deste PRD
  (sem impacto se a aprovação for parcial, pois nenhuma outra feature depende disso ainda).
