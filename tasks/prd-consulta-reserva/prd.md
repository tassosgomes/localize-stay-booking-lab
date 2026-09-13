# Consulta de Reserva

## Visão Geral

Depois que um Guest solicita uma Reservation (F01), ele recebe um identificador mas não tem, ainda,
nenhuma forma de saber o que acontece com essa reserva enquanto a saga de pagamento (F03/F04)
avança. Esta feature permite que quem conhece o identificador de uma Reservation consulte, a
qualquer momento, seus dados congelados, o estado atual do ciclo de vida (`solicitada`,
`confirmada`, `cancelada`) e a situação observável da saga (pagamento pendente, autorizado ou
rejeitado), incluindo o motivo quando a reserva foi cancelada.

Sem esta consulta, o resultado da saga fica invisível para o Guest e para quem estuda o laboratório:
uma Reservation poderia ser confirmada ou cancelada "nos bastidores" sem que ninguém, fora de acesso
direto ao banco, pudesse observar o desfecho. Afeta o Guest (que quer saber o que houve com seu
pedido), o autor/arquiteto em estudo (que observa o progresso da saga) e o frontend de teste (que
exibe esse estado).

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade selecionada:** não há `backlog/capabilities.md` formal nesta etapa; a feature vem
  diretamente de `domains/booking/domain.md` (F02).
- **Domínio no Domain Map:** Booking — mantém o ciclo de vida da Reservation e da Reservation Saga;
  esta feature apenas expõe leitura, sem criar nem alterar estado (`context/domain-map.md`).
- **Prioridade e dependências:** Must Have; segunda feature da ordem de implementação sugerida do
  domínio. Depende de F01 já existir (uma Reservation precisa ter sido criada para ser consultada);
  não depende de F03/F04 estarem implementadas para funcionar — antes delas, a consulta
  simplesmente mostra o estado inicial (`solicitada`, saga com pagamento pendente).
- **Restrições do baseline:** serviço próprio em .NET/C#, schema `booking.*`; Fase 0 não implementa
  autenticação/autorização (`context/architecture-baseline.md`); atores são identificadores de
  referência não autenticados (PD-002); toda entrada externa é tratada como não confiável e
  validada.

### Vision Doc

- **Objetivos de negócio atendidos**: dar visibilidade de negócio ao percurso da saga sem exigir
  acesso direto ao banco, complementando o logging correlacionado já previsto no baseline como forma
  de "reconstruir manualmente o percurso de uma reserva" (`context/architecture-baseline.md`
  §Observabilidade; `vision.md` §1).
- **Restrições globais aplicáveis**: escopo funcional congelado da Fase 0; nenhuma tecnologia
  antecipada de fases futuras (ex.: sem tracing distribuído nesta feature).
- **Non-Goals globais respeitados**: sem autenticação real, sem múltiplas moedas, sem cancelamento
  voluntário/alteração de reserva.

### Domain Doc

- **ID da feature**: F02 — Consulta de Reserva (`domains/booking/domain.md`).
- **Entidades envolvidas**: Reservation, Reservation Saga.
- **Regras de negócio referenciadas**: RN-05, RN-06 (os valores exibidos são os congelados na
  criação, nunca recalculados), RN-07, RN-08 (o desfecho exibido reflete a saga de pagamento), RN-10
  (todo desfecho final é um dos dois estados terminais observáveis), RN-11 (o estado exibido nunca
  regride nem alterna entre terminais).
- **Dependências upstream**: F01 (Solicitação de Reserva) — cria a Reservation e a Saga a serem
  consultadas.
- **Dependências downstream**: nenhuma feature de Booking depende tecnicamente desta para existir;
  é o canal de visibilidade usado pelo frontend de teste e pelo autor/arquiteto em estudo durante
  F03/F04.
- **Eventos consumidos**: nenhum — a consulta lê o estado já persistido pelas demais features, não
  reage a eventos.
- **Eventos produzidos**: nenhum.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| Situação da saga | Estado observável da Reservation Saga do ponto de vista de negócio: pagamento pendente (aguardando resultado), autorizado ou rejeitado. Não é o estado interno técnico da saga, apenas o resultado relevante para quem consulta. | `domains/booking/domain.md` §3, decisão desta etapa |
| Identificador de correlação | Identificador que liga a Reservation à sua Saga e aos eventos trocados com Payment; exposto na consulta para permitir correlacionar manualmente com os logs do baseline. | `domains/booking/domain.md` §3; `context/architecture-baseline.md` §Observabilidade |

---

## Objetivos

- Permitir que quem conhece o identificador de uma Reservation veja seu estado atual sem depender
  de outro canal ou de acesso direto ao banco.
- Tornar observável a situação da saga (pendente, autorizado, rejeitado) e, quando a Reservation foi
  cancelada, o motivo do cancelamento.
- Garantir que os dados retornados são sempre os efetivamente persistidos (preço, moeda e total
  congelados por F01; estado atualizado por F04), nunca recalculados ou inferidos na consulta.
- Critério de conclusão desta feature: uma Reservation existente pode ser consultada por
  identificador em qualquer ponto do seu ciclo de vida e retorna dados fiéis ao estado persistido;
  um identificador que não corresponde a nenhuma Reservation é tratado como "não encontrada", nunca
  como erro genérico nem como dado inventado.

---

## Histórias de Usuário

- Como **Guest**, eu quero consultar minha Reservation pelo identificador recebido ao solicitá-la,
  para saber se ela foi confirmada, cancelada, ou ainda aguarda o resultado do pagamento.
- Como **autor/arquiteto em estudo**, eu quero consultar a situação da saga (pendente, autorizado,
  rejeitado) e o identificador de correlação de uma Reservation, para observar e documentar o
  progresso da jornada entre Booking e Payment.
- Como **frontend de teste**, eu quero buscar uma Reservation por identificador e exibir seus dados
  e o estado da saga, para permitir verificação manual do fluxo completo sem acesso direto ao banco.
- Como **Guest**, eu quero receber uma resposta clara quando o identificador informado não
  corresponde a nenhuma Reservation, para saber que devo conferir o identificador.

---

## Funcionalidades Principais

### RF-01: Consultar Reserva por Identificador

**Descrição**: Um solicitante informa o identificador de uma Reservation e recebe seus dados de
negócio atuais: Accommodation e Guest de referência, período (check-in/check-out), número de
hóspedes, preço por noite, moeda e valor total congelados, estado da Reservation
(`solicitada`/`confirmada`/`cancelada`), situação da saga (pagamento pendente/autorizado/rejeitado),
identificador de correlação e, quando a Reservation foi cancelada, o motivo do cancelamento. A
consulta é somente leitura e não altera nenhum estado. Como a Fase 0 não implementa
autenticação/autorização (PD-002), qualquer solicitante que conheça o identificador pode consultá-lo
— não há verificação de que o solicitante é o Guest de referência da Reservation.

**Critérios de Aceitação**:

- **Given** uma Reservation existente no estado `solicitada`, com a saga aguardando o resultado do
  pagamento (F03 ainda não concluiu)
  **When** o solicitante consulta pelo identificador
  **Then** recebe os dados congelados da Reservation, estado `solicitada` e situação da saga como
  pagamento pendente, sem motivo de cancelamento.

- **Given** uma Reservation existente que foi confirmada após autorização do pagamento (F04)
  **When** o solicitante consulta pelo identificador
  **Then** recebe estado `confirmada` e situação da saga como pagamento autorizado.

- **Given** uma Reservation existente que foi cancelada após rejeição do pagamento (F04)
  **When** o solicitante consulta pelo identificador
  **Then** recebe estado `cancelada`, situação da saga como pagamento rejeitado, e o motivo do
  cancelamento.

- **Given** um identificador que não corresponde a nenhuma Reservation existente
  **When** o solicitante consulta por ele
  **Then** a resposta indica que a Reservation não foi encontrada, sem expor detalhes internos e sem
  ser confundida com uma rejeição de regra de negócio.

- **Given** um identificador em formato inválido (ex.: vazio ou fora do formato esperado)
  **When** o solicitante consulta por ele
  **Then** a solicitação é rejeitada como entrada inválida, distinta conceitualmente de "Reservation
  não encontrada" (uma é erro de entrada, a outra é ausência de um registro válido).

**Prioridade**: Must Have

**Rastreabilidade**: RN-05, RN-06, RN-07, RN-08, RN-10, RN-11; PD-002.

---

## Experiência do Usuário

O Guest (via frontend de teste) informa o identificador da Reservation recebido no momento da
solicitação (F01) e visualiza uma tela de detalhes com: dados do período e hóspedes, preço por
noite, moeda e valor total, estado atual da Reservation e um indicador claro da situação da saga
("Pagamento pendente", "Pagamento autorizado" ou "Pagamento rejeitado"). Quando a Reservation está
cancelada, o motivo é exibido junto ao estado. Quando o identificador não corresponde a nenhuma
Reservation, o frontend exibe uma mensagem direta de "não encontrada" em vez de uma tela vazia ou
um erro técnico.

Não há atualização automática/tempo real nesta feature: para ver uma mudança de estado (ex.: saga
concluída após F04), o solicitante consulta novamente. Não há requisito de acessibilidade além do já
aplicável ao frontend de teste como ferramenta de laboratório
(`context/architecture-baseline.md`); não há onboarding.

---

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | Esta feature cobre apenas a consulta de uma Reservation específica pelo seu identificador; não inclui listagem ou busca de reservas por Guest de referência, período ou estado. | Listagem por Guest de referência — descartada nesta etapa por ampliar o escopo além do descrito em `domains/booking/domain.md` F02 ("situação de uma Reservation") e por, sem autenticação (PD-002), permitir que qualquer solicitante varra todas as reservas de um Guest e não apenas consulte um registro específico que já conhece. | Objetivos, RF-01, Não-Objetivos | — |
| DP-02 | A consulta expõe o identificador de correlação da saga como dado de negócio observável, para permitir correlação manual com os logs previstos no baseline. | Omitir o identificador de correlação por ser "detalhe técnico" — descartada porque `domains/booking/domain.md` já lista esse identificador como atributo da entidade Reservation Saga, e o baseline depende dele para reconstrução manual do percurso; escondê-lo prejudicaria o objetivo de estudo sem ganho de escopo. | RF-01, Termos Canônicos | — |

---

## Restrições Técnicas de Alto Nível

- Sem autenticação/autorização na Fase 0 (`context/architecture-baseline.md`, PD-002): a consulta
  não verifica identidade nem vínculo entre solicitante e Guest de referência da Reservation.
- Todo dado de entrada (identificador) é tratado como não confiável e validado antes de qualquer
  busca (higiene básica de serviço).
- Consulta é somente leitura: nenhuma chamada desta feature cria, altera ou cancela uma Reservation
  ou sua Saga.
- Nenhum dado real de pagamento ou PII real é exposto; identificadores e valores são fictícios.

---

## Não-Objetivos (Fora de Escopo)

- Listar, buscar ou filtrar reservas por Guest de referência, período, estado ou qualquer outro
  critério — apenas consulta pontual por identificador (DP-01).
- Iniciar, avançar ou compensar a saga de pagamento — pertence a F01, F03 e F04.
- Criar, alterar ou cancelar uma Reservation — esta feature é somente leitura.
- Cancelamento voluntário pelo Guest/Host, alteração de datas ou hóspedes de uma Reservation já
  solicitada — fora do escopo funcional congelado da Fase 0 (RN-12).
- Exposição de histórico completo de transições de estado (auditoria) — apenas o estado atual é
  retornado; reconstrução de percurso completo é atendida pelo logging correlacionado do baseline,
  não por esta API de negócio.
- Atualização em tempo real (streaming, polling automático, notificações push) do estado consultado
  — o solicitante consulta novamente quando quiser um estado atualizado.
- Autenticação/autorização real de Guest, restrição de acesso por identidade — non-goal global e de
  PD-002.

---

## Plano de Rollout Faseado

Feature única e indivisível para efeito de entrega — não há rollout incremental interno a F02: a
feature só cumpre seu objetivo de visibilidade quando o caminho de sucesso (todos os estados e
situações de saga) e o caminho de "não encontrada" estão implementados juntos.

### MVP (Fase 1)

- **Funcionalidades incluídas**: RF-01 (todos os estados de Reservation e situações de saga, mais o
  caminho de "não encontrada" e o de entrada inválida).
- **Critérios de sucesso para avançar à Fase 2**: não aplicável — não há Fase 2/3 para esta feature;
  a conclusão de RF-01 dá visibilidade completa ao progresso de F01/F03/F04.

---

## Métricas de Sucesso

Como o Localize Stay v2 é um laboratório de estudo pessoal, sem usuários reais nem métricas
comerciais (`vision.md` §1), as métricas de sucesso aqui são de verificação de comportamento, não de
adoção ou engajamento:

- **Fidelidade do retorno**: para qualquer Reservation existente, os dados retornados (preço,
  moeda, total, estado, situação da saga, motivo de cancelamento quando aplicável) são idênticos ao
  que está persistido no momento da consulta — verificável a 100% pelos casos de teste de RF-01.
- **Cobertura de estados**: os três estados de negócio da Reservation e as três situações de saga
  aplicáveis a cada um são exercitados nos casos de teste de RF-01.
- **Tratamento de ausência e entrada inválida**: todo identificador inexistente resulta em "não
  encontrada" e todo identificador malformado resulta em "entrada inválida", sem sobreposição entre
  os dois casos — verificável a 100% pelos casos de teste de RF-01.

---

## Riscos e Mitigações

- **Risco de exposição sem controle de acesso**: sem autenticação (Fase 0), qualquer solicitante que
  descubra ou adivinhe um identificador pode consultar dados de uma Reservation de outro Guest —
  Mitigação: aceito como limitação conhecida da Fase 0, já registrada em PD-002 e nos non-goals de
  PII real da Vision; a introdução de autenticação real e de controle de acesso por identidade é uma
  decisão de arquitetura futura (ADR), fora desta feature.
- **Risco de leitura inconsistente durante a corrida da saga**: uma consulta feita exatamente entre a
  publicação de `booking.payment_requested` e o processamento do resultado de Payment mostra
  "pagamento pendente" mesmo que o resultado já tenha ocorrido no instante seguinte — Mitigação:
  aceito como comportamento esperado de uma consulta que reflete o último estado persistido, sem
  requisito de tempo real nesta fase; o solicitante pode consultar novamente.

---

## Alternativas Consideradas

### Abordagem Escolhida: Consulta pontual por identificador, somente leitura, com estado consolidado de Reservation e Saga

- **Descrição**: o solicitante informa o identificador da Reservation e recebe, em uma única
  resposta, os dados congelados, o estado da Reservation e a situação observável da saga.
- **Por que foi escolhida**: atende exatamente o que `domains/booking/domain.md` F02 descreve
  ("acompanhar dados, estado final e situação da saga de uma Reservation") sem introduzir escopo
  adicional (listagem, filtros, histórico) que não foi pedido nem justificado por um problema atual
  do laboratório.

### Alternativa Rejeitada: Listagem/busca de reservas por Guest de referência

- **Descrição**: além da consulta por identificador, o solicitante poderia listar todas as reservas
  associadas a um Guest de referência informado.
- **Trade-offs**: útil para um cenário de "minhas reservas" mais completo, mas amplia o escopo desta
  feature e, sem autenticação (PD-002), permite que qualquer solicitante varra o histórico completo
  de qualquer Guest a partir de um único identificador de referência, não apenas de uma Reservation
  específica que já conhece.
- **Por que foi rejeitada**: decisão desta etapa (DP-01); pode ser reconsiderada em fase futura, se e
  quando houver identidade real de Guest.

### Alternativa Rejeitada: Exposição de histórico completo de transições de estado

- **Descrição**: a consulta retornaria não só o estado atual, mas todas as transições anteriores da
  Reservation e da Saga (auditoria).
- **Trade-offs**: dá visibilidade adicional ao progresso da saga, mas duplica o propósito do logging
  correlacionado já previsto no baseline arquitetural (`correlationId`/`causationId`) e adiciona
  complexidade de persistência sem um problema atual que a justifique.
- **Por que foi rejeitada**: o objetivo desta feature é "acompanhar o estado atual", não auditar o
  histórico; a regra do roadmap (nova tecnologia/escopo só quando resolve um problema já existente)
  se aplica aqui.

---

## Questões em Aberto

- Se uma fase futura introduzir identidade real de Guest, a decisão de não listar reservas por Guest
  (DP-01) deve ser revisitada; não bloqueia esta entrega. Responsável: autor do laboratório, sem
  prazo definido.
- Formato exato do identificador de Reservation na consulta (ex.: tipo, encoding) fica para a
  TechSpec de F02, que herda o mesmo identificador definido para a Reservation em F01 — não bloqueia
  este PRD.
