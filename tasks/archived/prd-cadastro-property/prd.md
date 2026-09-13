# Cadastro de Property

## Visão Geral

A feature F01 permite que um Host cadastre uma `Property` e altere seus dados cadastrais. Ela
estabelece a raiz do catálogo de hospedagens do Localize Stay v2, necessária para que unidades
reserváveis sejam vinculadas posteriormente pela F02 e consultadas pela F03.

O valor desta entrega é criar uma fonte única e confiável para a identidade básica das hospedagens,
mantendo o escopo funcional pequeno o suficiente para sustentar o objetivo do laboratório: estudar
fronteiras, ownership e contratos sem reproduzir a superfície de produto de uma plataforma comercial.

---

## Rastreabilidade

### Capacidade e fronteiras

- **Capacidade selecionada:** não há backlog formal de capacidades; `backlog/capabilities.md` não existe.
- **Domínio no Domain Map:** Catalog, dono de `Property`, `Accommodation` e `Availability`. Esta feature
  mantém apenas `Property` e não assume responsabilidades de Booking, Payment ou Notification.
- **Prioridade e dependências:** Must Have, Fase 0 — Fundação. É a primeira feature sugerida do domínio e
  não possui dependência funcional upstream. F02, F03 e F07 dependem de seu resultado.
- **Restrições do baseline:** Catalog preserva ownership exclusivo dos dados de `Property`; consumidores
  acessam seu estado somente por contratos explícitos. Na Fase 0 não há autenticação/autorização real e
  todos os dados são fictícios. A interface pública deve permanecer apta à catalogação no OpenMetadata.

### Vision Doc

- **Objetivos de negócio atendidos:** viabilizar o cadastro mínimo de hospedagens do escopo funcional
  congelado e estabelecer a base do Catalog para a Fase 0.
- **Restrições globais aplicáveis:** projeto pessoal e greenfield, sem usuários ou PII reais; Catalog é o
  dono lógico dos próprios dados; chamadas síncronas públicas devem possuir contrato versionado; ativos,
  ownership e relacionamentos devem ser catalogáveis.
- **Non-Goals globais respeitados:** produto comercial, imagens, reviews, mapas/geolocalização avançada,
  pricing dinâmico, cupons, chat, recommendation engine, multi-tenancy, busca da Fase 2 e infraestrutura
  de cloud/escala não fazem parte desta feature.

### Domain Doc

- **ID da feature:** F01 — Cadastro de Property.
- **Entidades envolvidas:** `Property` e `Host` (como responsável pela hospedagem, não como entidade sob
  gestão do Catalog).
- **Regras de negócio referenciadas:** RN-01 e RN-02 afetam features downstream, mas nenhuma regra numerada
  do Domain Doc define diretamente a criação ou a edição de `Property` em F01.
- **Dependências upstream:** nenhuma.
- **Dependências downstream:** F02 — Cadastro de Accommodation; F03 — Consulta de
  Property/Accommodation; F07 — Desativação de Property/Accommodation.
- **Eventos consumidos:** nenhum.
- **Eventos produzidos:** nenhum evento assíncrono nesta fase.

## Termos Canônicos

| Termo | Definição de negócio | Escopo/Fonte |
|---|---|---|
| `Property` | Hospedagem cadastrada por um Host; raiz à qual uma ou mais `Accommodation` podem pertencer. | Vision Doc e Domain Doc de Catalog |
| `Host` | Responsável informado para uma `Property`; a Fase 0 assume sua identidade, sem implementar cadastro ou autenticação do ator. | Domain Doc e baseline arquitetural |
| Localização | Descrição textual livre de onde a `Property` se encontra; não implica mapas nem geolocalização avançada. | Domain Doc e esclarecimento desta feature |

---

## Objetivos

- Permitir o cadastro válido de uma `Property` com nome, localização textual e Host responsável.
- Permitir a correção posterior dos dados cadastrais editáveis de uma `Property` existente.
- Tornar cada cadastro identificável de forma estável para uso pelas features F02, F03 e F07.
- Concluir todos os critérios Must Have e seus casos negativos antes de iniciar F02.

---

## Histórias de Usuário

- Como **Host**, eu quero cadastrar uma hospedagem com seus dados básicos para que ela possa receber
  unidades reserváveis no catálogo.
- Como **Host**, eu quero corrigir o nome ou a localização textual de uma hospedagem já cadastrada para que
  o catálogo reflita seus dados atuais.
- Como **autor do laboratório**, eu quero observar respostas inequívocas para cadastros válidos e inválidos
  para verificar o comportamento do primeiro fluxo do domínio Catalog.

---

## Funcionalidades Principais

### RF-01: Criar Property

**Descrição**: O Host cadastra uma `Property` informando nome, localização textual e sua identificação como
responsável. Um cadastro aceito recebe identificação estável e inicia com status `ativo`, permitindo que as
features downstream o utilizem sem exigir uma ativação adicional.

**Critérios de Aceitação**:

- **Given** nome, localização e identificação do Host válidos
  **When** o Host solicita o cadastro da `Property`
  **Then** a `Property` é registrada uma única vez com identificação estável, os dados informados, o Host
  responsável e status `ativo`.

- **Given** uma solicitação sem nome, sem localização ou sem identificação do Host
  **When** o cadastro é submetido
  **Then** a solicitação é rejeitada com indicação dos campos inválidos e nenhuma `Property` é criada.

- **Given** nome ou localização contendo apenas espaços em branco
  **When** o cadastro é submetido
  **Then** a solicitação é rejeitada como dado obrigatório ausente e nenhuma `Property` é criada.

- **Given** dados de cadastro que excedem os limites aceitos pelo produto
  **When** o cadastro é submetido
  **Then** a solicitação é rejeitada com indicação do campo inválido e sem persistência parcial.

- **Given** uma `Property` já cadastrada com o mesmo nome e a mesma localização
  **When** outro cadastro válido é submetido
  **Then** a nova `Property` é aceita com identificação própria, pois a Fase 0 não aplica unicidade de
  negócio nem detecção automática de duplicidade.

**Prioridade**: Must Have

**Rastreabilidade**: F01; base para RN-01 e RN-02 nas features downstream.

---

### RF-02: Editar dados cadastrais da Property

**Descrição**: O Host responsável altera o nome e/ou a localização textual de uma `Property` existente. A
identificação estável, o status e o Host responsável pela `Property` não são alterados por este fluxo. A
transferência para outro Host não faz parte da F01.

**Critérios de Aceitação**:

- **Given** uma `Property` existente e dados cadastrais válidos
  **When** seu Host responsável solicita a alteração do nome e/ou da localização
  **Then** os campos informados são atualizados e a identificação, o Host responsável e o status permanecem
  inalterados.

- **Given** uma `Property` inexistente
  **When** uma edição é solicitada
  **Then** a operação informa que a `Property` não foi encontrada e nenhum cadastro é criado ou alterado.

- **Given** uma `Property` existente
  **When** a edição tenta deixar nome ou localização ausente, em branco ou fora dos limites aceitos
  **Then** toda a alteração é rejeitada com indicação dos campos inválidos e os dados anteriores são
  preservados.

- **Given** uma `Property` existente vinculada a um Host
  **When** outro Host solicita sua edição
  **Then** a operação é rejeitada e os dados permanecem inalterados, considerando a identidade de Host
  fornecida pelo mecanismo fictício da Fase 0, sem exigir autenticação real.

- **Given** uma `Property` existente
  **When** a edição tenta alterar sua identificação estável ou seu status
  **Then** esses dados não são alterados por F01; a desativação pertence à F07.

**Prioridade**: Must Have

**Rastreabilidade**: F01; fronteira de F07; regra de ownership coerente com o Domain Doc.

---

## Experiência do Usuário

O Host acessa o cadastro de hospedagem no frontend de teste, informa nome e localização textual e confirma a
operação. Em caso de sucesso, recebe confirmação clara e a identificação da `Property`. Em caso de erro, os
campos problemáticos são indicados sem perder os demais valores já informados.

Para edição, o Host parte de uma `Property` previamente identificada, vê os dados atuais, altera nome e/ou
localização e confirma. O resultado deve distinguir sucesso, dados inválidos, cadastro inexistente e falta de
ownership. A experiência deve permitir uso por teclado, associar rótulos aos campos, expor erros de maneira
textual e perceptível por tecnologia assistiva e não depender apenas de cor.

A descoberta e consulta geral de propriedades pertencem à F03. Nesta entrega, o frontend de teste pode
acessar uma `Property` pelo identificador retornado no cadastro para exercitar a edição, sem transformar esse
apoio em uma feature de consulta pública.

---

## Decisões de Produto

| ID | Decisão confirmada | Alternativas descartadas e motivo | Impacto no PRD | Registro |
|---|---|---|---|---|
| DP-01 | A Fase 0 aceita `Property` com nome e localização iguais aos de outro cadastro. | Chave de unicidade ou detecção heurística de duplicidade adicionariam uma política não necessária ao laboratório. | RF-01 inclui aceitação explícita de duplicidade. | Aprovação deste PRD |
| DP-02 | F01 não permite transferir uma `Property` para outro Host. | Transferência ampliaria o fluxo de edição e exigiria regras de ownership fora do escopo mínimo. | RF-02 preserva o Host responsável; transferência é non-goal. | Aprovação deste PRD |

---

## Restrições Técnicas de Alto Nível

- Os dados são fictícios e não devem conter PII real.
- A ausência de autenticação/autorização na Fase 0 não elimina o vínculo de negócio entre `Property` e Host;
  a identificação fictícia do ator deve ser suficiente para observar os cenários de ownership.
- Catalog mantém ownership exclusivo dos dados da `Property`; não há acesso direto de outro domínio a seus
  dados internos.
- A interação pública de cadastro e edição deve possuir contrato explícito e versionado, conforme a Vision e
  o baseline, sem prescrever neste PRD endpoints, bibliotecas ou persistência.
- Os limites máximos dos campos devem ser definidos na TechSpec/contrato e produzir o comportamento observável
  descrito nos critérios de aceitação.

---

## Não-Objetivos (Fora de Escopo)

- Cadastrar, editar ou autenticar Hosts e Guests.
- Consultar ou listar `Property` para Host ou Guest; isso pertence à F03.
- Desativar ou reativar `Property`; isso pertence à F07.
- Excluir `Property`.
- Cadastrar ou editar `Accommodation`; isso pertence à F02.
- Gerenciar disponibilidade, preço, reserva, pagamento ou notificação.
- Transferir uma `Property` entre Hosts até que a política correspondente seja decidida.
- Detectar ou impedir automaticamente propriedades duplicadas.
- Incluir imagens, reviews, mapas/geolocalização avançada, pricing dinâmico, cupons, chat ou recomendações.
- Produzir eventos assíncronos de criação ou alteração de `Property` na Fase 0.

---

## Plano de Rollout Faseado

### MVP (Fase 1 desta feature)

- **Funcionalidades incluídas:** RF-01 e RF-02.
- **Critérios de sucesso:** todos os critérios de aceitação Must Have, incluindo casos negativos, são
  demonstráveis no frontend de teste e a `Property` criada pode servir de referência para iniciar F02.

Fases adicionais não se aplicam a F01: consulta, `Accommodation` e desativação possuem features próprias, e
ampliar esta entrega duplicaria fronteiras já definidas no Domain Doc.

---

## Métricas de Sucesso

| Métrica | Definição | Valor-alvo | Prazo |
|---|---|---|---|
| Cobertura dos critérios Must Have | Percentual dos critérios de aceitação de RF-01 e RF-02 demonstrados com resultado esperado | 100% | Antes de iniciar F02 |
| Integridade em rejeições | Solicitações inválidas ou não autorizadas que terminam sem criação indevida ou alteração parcial | 100% dos cenários especificados | Antes de concluir F01 |
| Continuidade para F02 | Cadastro criado por RF-01 que pode ser referenciado de forma estável no cadastro de `Accommodation` | 100% no cenário de integração da F02 | Na validação inicial de F02 |

Não há métrica comercial ou de adoção: o sistema é um laboratório pessoal sem usuários reais.

---

## Riscos e Mitigações

- **Cadastros semanticamente duplicados:** a ausência deliberada de unicidade permite registros repetidos.
  — **Mitigação:** tornar esse comportamento explícito no cadastro e usar identificações estáveis para
  distinguir as `Property`.
- **Demanda futura de transferência:** um caso posterior pode exigir mudança do Host responsável.
  — **Mitigação:** tratar essa necessidade como evolução de produto com regras próprias, sem ampliar
  silenciosamente o fluxo de edição da F01.
- **Escopo crescer para gestão completa de hospedagens:** consulta, desativação e unidades podem ser atraídas
  para o mesmo fluxo. — **Mitigação:** conservar as fronteiras F02, F03 e F07 e seus PRDs separados.
- **Expectativa de segurança real:** o cenário de ownership pode ser confundido com autenticação de produção.
  — **Mitigação:** rotular identidades e dados como fictícios e manter autenticação real fora da Fase 0.

---

## Alternativas Consideradas

### Abordagem Escolhida: Cadastro mínimo sem unicidade nem transferência

- **Descrição:** cada cadastro válido recebe identificação própria; a edição preserva o Host responsável.
- **Por que foi escolhida:** atende ao escopo mínimo da Fase 0 e evita antecipar políticas sem valor para o
  objetivo atual do laboratório.

### Alternativa Rejeitada 1: Impedir duplicidade

- **Descrição:** rejeitar uma criação quando nome e localização coincidirem com outra `Property`.
- **Trade-offs:** reduziria repetições, mas nome e localização textual não formam necessariamente uma identidade
  inequívoca e exigiriam política adicional.
- **Por que foi rejeitada:** complexidade de produto sem necessidade demonstrada na Fase 0.

### Alternativa Rejeitada 2: Permitir transferência entre Hosts

- **Descrição:** tornar o Host responsável um dado editável da `Property`.
- **Trade-offs:** cobriria mudança de ownership, mas introduziria regras de autorização e efeitos downstream.
- **Por que foi rejeitada:** ultrapassa o cadastro e a edição cadastral mínimos definidos para F01.

A separação entre F01 (cadastro/edição), F03 (consulta) e F07 (desativação) permanece herdada do Domain Doc.

---

## Questões em Aberto

Nenhuma questão de produto permanece aberta para a F01. Os limites concretos dos campos são detalhamento do
contrato/TechSpec e devem preservar o comportamento observável deste PRD.

---

*PRD aprovado em 2026-09-12 a partir de `vision.md`, `context/domain-map.md`,
`domains/catalog/domain.md`, das restrições vigentes em `context/architecture-baseline.md` e das decisões de
produto confirmadas nesta entrega.*
