# Baseline Arquitetural

> Herda `vision.md` (visão, roadmap, restrições) e `context/domain-map.md` (quatro domínios: Catalog, Booking,
> Payment, Notification). Define os princípios estruturais que toda TechSpec, PRD e task devem seguir. Não
> projeta funcionalidades específicas — isso é responsabilidade das etapas seguintes.
>
> Decisões novas e materiais tomadas nesta etapa estão registradas como ADR em `docs/adr/` (índice em
> `docs/adr/index.md`): [ADR-001](../docs/adr/adr-001-backend-stack-dotnet.md) (stack .NET),
> [ADR-002](../docs/adr/adr-002-broker-fase0-rabbitmq.md) (RabbitMQ na Fase 0) e
> [ADR-003](../docs/adr/adr-003-frontend-teste-react.md) (frontend de teste em React, sem gateway/BFF).

## Estilo Arquitetural

- **Service-Based Architecture** (não monólito, não microsserviços plenos): quatro serviços independentemente
  implantáveis — Catalog, Booking, Payment e Notification Worker —, um por domínio do `domain-map.md`, cada um
  com seu próprio código-fonte e processo, porém compartilhando uma única instância PostgreSQL com ownership
  lógico por schema. Esta é a decisão já registrada no brief/roadmap (Fase 0) e herdada aqui, não uma nova
  escolha desta etapa.
- Cada serviço mapeia 1:1 para um bounded context do domain-map; nenhum serviço acumula responsabilidade de
  outro domínio.
- Stack de implementação: **.NET / C# (ASP.NET Core)** para os quatro serviços — ver [ADR-001](../docs/adr/adr-001-backend-stack-dotnet.md).
- Razão para "service-based" em vez de microsserviços completos: praticar contratos formais e ownership lógico
  de dados sem pagar o custo operacional de um banco por serviço, service mesh ou orquestração de containers já
  na Fase 0 — coerente com o princípio do brief "reduzir o produto, não a engenharia" e com evitar otimização
  prematura. Infraestrutura de containers/cloud é escopo das Fases 3/4.
- Um **frontend de teste/visualização** em React + Vite + TypeScript é incluído desde a Fase 0, exclusivamente
  para facilitar a verificação manual do sistema (criar hospedagem, consultar disponibilidade, criar reserva,
  acompanhar o resultado da saga) — ver [ADR-003](../docs/adr/adr-003-frontend-teste-react.md). Ele **não** é um
  quinto domínio/bounded context: não tem ownership de dados, não implementa regra de negócio e não aparece no
  `domain-map.md`. É tratado como uma ferramenta de suporte ao estudo, não como parte do domínio do produto.

## Princípios de Interação entre Domínios

- Toda interação entre domínios é obrigatoriamente uma de três categorias, classificada **antes** de
  implementar: chamada síncrona (API/OpenAPI), evento assíncrono (AsyncAPI) ou dado compartilhado (Data
  Contract). Acesso "ad hoc" (import de código entre serviços, chamada direta a banco/API não contratada) é
  proibido.
- **Validação de disponibilidade na criação de reserva:** Booking chama Catalog **sincronamente** via API
  contratada em OpenAPI para validar acomodação/disponibilidade no momento da reserva. O dataset publicado
  `available_accommodations_v1` não é usado nesse caminho — ele existe para consumidores mais amplos e
  independentes do ciclo de vida da reserva (ex.: a futura Busca da Fase 2). Isto resolve o ponto em aberto
  registrado em `domain-map.md`.
- A saga Booking ↔ Payment é **coreografada** via eventos, não orquestrada por um componente externo: Booking
  publica `PaymentRequested` e reage a `PaymentAuthorized`/`PaymentRejected` para confirmar ou compensar a
  reserva. Booking é o dono lógico do fluxo porque é o dono do estado de `Reservation`.
- Notification é sempre um consumidor terminal: consome eventos finais de Booking e nunca publica eventos
  consumidos por outros domínios (fim da cadeia assíncrona).
- Nenhum serviço lê ou escreve em schema/tabela interna de outro domínio, nem mesmo para leitura. Todo acesso
  cross-domain passa por um contrato explícito (API do dono, evento do dono, ou dataset publicado do dono).
- **Frontend de teste:** chama diretamente as APIs OpenAPI de Catalog, Booking e Payment a partir do navegador,
  sem gateway/BFF intermediário na Fase 0 — ver [ADR-003](../docs/adr/adr-003-frontend-teste-react.md). O
  frontend é só mais um consumidor externo dos contratos já publicados; não ganha nenhum acesso que um
  consumidor externo comum não teria.

## Regras de Propriedade dos Dados

- Uma única instância PostgreSQL na Fase 0, com ownership lógico por schema: `catalog.*`, `booking.*`,
  `payment.*` (Notification só ganha schema próprio, ex. `notification.*`, se precisar reter estado — do
  contrário permanece stateless, apenas processando eventos).
- Um schema de integração (ex. `integration.*`) concentra exclusivamente as views/materialized views
  publicadas e contratadas (`available_accommodations_v1`, `reservation_calendar_v1`); é o único ponto de
  leitura cross-domain de dados.
- A tabela é sempre implementação interna do domínio dono; a view publicada no schema de integração é a
  interface de dados — mudança na tabela interna nunca pode quebrar a view sem uma migração deliberada do
  Data Contract.
- Cada domínio controla suas próprias migrations; nenhum serviço aplica migration em schema alheio.
- Acesso a banco é restrito por role: cada serviço conecta com uma role própria (`catalog_role`,
  `booking_role`, `payment_role`), com permissão de escrita apenas no seu schema e leitura apenas no schema de
  integração — nunca escrita no schema de integração por um consumidor, nunca leitura de schema alheio.

## Padrões de Comunicação

- **Síncrona:** HTTP/REST contratado em OpenAPI 3.x. Versionamento por prefixo de rota (`/v1/...`), consistente
  com a convenção de nome já usada nos datasets publicados (sufixo `_v1`).
- Como o frontend de teste chama Catalog, Booking e Payment diretamente do navegador (sem gateway), cada um
  desses três serviços habilita CORS explicitamente para a origem do frontend, em vez de abrir esse acesso de
  forma permissiva/global.
- **Assíncrona:** RabbitMQ como broker de eventos na Fase 0 — ver [ADR-002](../docs/adr/adr-002-broker-fase0-rabbitmq.md).
  Eventos contratados em AsyncAPI, com a versão embutida no tipo do evento (ex.: `PaymentAuthorized.v1`) para
  evoluir payloads sem quebrar consumidores existentes.
- Todo evento da saga carrega `correlationId` (identifica a reserva/saga) e `causationId` (identifica o
  evento/comando que o originou) — pré-requisito para rastrear manualmente o percurso de uma reserva e base
  para os estudos de resiliência da Fase 1.
- **Dado compartilhado:** view/materialized view publicada no schema de integração, formalizada por um Data
  Contract que descreve schema, cadência de atualização e política de compatibilidade — o Data Contract é, para
  dados, o equivalente ao OpenAPI/AsyncAPI.
- Todo contrato (OpenAPI, AsyncAPI, Data Contract) é versionado desde a Fase 0. Mudança incompatível gera uma
  nova versão publicada ao lado da anterior; nunca uma alteração in-place que quebre consumidores existentes.

## Princípios de Segurança

- A Fase 0 **não implementa autenticação/autorização**: é um laboratório de estudo, sem usuários reais e sem
  múltiplos atores a proteger (non-goal explícito da Vision). Introduzir um mecanismo de identidade agora
  antecipa uma tecnologia sem um problema atual que a justifique, contrariando a regra do roadmap. Quando um
  cenário real de múltiplos atores existir, tratar como decisão de arquitetura nova (ADR própria), não como
  extensão silenciosa deste baseline.
- Toda entrada externa (payloads de API, mensagens consumidas) é tratada como não confiável e validada
  (tipos, formato, limites) — higiene básica de serviço, independente de haver autenticação.
- Todos os dados do sistema são fictícios; nenhuma PII real nem dado real de pagamento é processado (non-goal
  da Vision) — não há requisito de criptografia ou compliance de dados sensíveis na Fase 0.
- Segredos de infraestrutura (connection string do Postgres, credenciais do RabbitMQ) nunca ficam em código
  versionado, mesmo em ambiente local de laboratório — prática que se carrega sem retrabalho para as Fases 3/4
  (cloud, secrets managers).

## Padrões de Observabilidade

- Fase 0: logging estruturado básico em cada serviço, cobrindo os pontos-chave da saga (requisição recebida,
  evento publicado, evento consumido, decisão de confirmação/cancelamento). Tracing distribuído e métricas
  formais são temas explícitos da Fase 1 (Resiliência) — não antecipar instrumentação (ex.: OpenTelemetry) além
  deste logging básico.
- Todo log relacionado à saga inclui `correlationId`/`causationId` (os mesmos identificadores dos contratos de
  evento), permitindo reconstruir manualmente o percurso de uma reserva entre serviços sem tracing distribuído.
- Catalogação no **OpenMetadata** (APIs, tópicos/filas, datasets publicados, ownership e lineage) é obrigatória
  desde a Fase 0 — é observabilidade "de catálogo/governança", distinta e independente da observabilidade
  operacional/runtime tratada na Fase 1.

## Premissas de Escalabilidade

- Fase 0/1 operam em escala de laboratório: uma instância de PostgreSQL, uma instância de RabbitMQ e o
  OpenMetadata — sem requisito de alta disponibilidade, autoscaling ou multi-região.
- **Dependências stateful em tempo de desenvolvimento:** PostgreSQL, RabbitMQ e OpenMetadata rodam como
  serviços persistentes no servidor `infra` do homelab do autor, provisionados via **Coolify**, em vez de um
  `docker compose` local com esses serviços — a máquina de desenvolvimento (ThinkPad) tem espaço em disco
  limitado para hospedá-los de forma persistente. Os quatro serviços de aplicação (Catalog, Booking, Payment,
  Notification Worker) e o frontend de teste continuam rodando/sendo desenvolvidos localmente (ou no `desenv`,
  conforme o fluxo já documentado em `infra/AGENTS.md`), conectando-se a essas dependências pela rede em vez de
  localhost. Isto é uma decisão de **ambiente de desenvolvimento** por restrição prática de disco, não a
  decisão de plataforma/cloud de produção da Vision (Fase 3) — os detalhes de provisionamento (stacks Coolify,
  redes, credenciais) ficam para quando essa etapa for tratada, não para este baseline.
- O ownership lógico por schema (em vez de acesso direto a tabelas) é o que permite, no futuro, separar
  fisicamente um domínio para seu próprio banco sem reescrever contratos: consumidores já dependem apenas de
  APIs, eventos e datasets publicados, nunca de tabelas internas.
- A independência de deploy dos quatro serviços (Service-Based Architecture) permite escalar/implantar cada um
  isoladamente já nas Fases 3/4, sem exigir refatoração das fronteiras de domínio definidas no domain-map.
- Nenhuma decisão de escala (cache, CDN, autoscaling, particionamento de broker) é tomada na Fase 0 — permanecem
  reservadas às Fases 3/4, seguindo a regra do roadmap de só introduzir tecnologia quando resolve um problema
  já existente.

## Guardrails Arquiteturais

- **Propriedade dos domínios:** cada bounded context do domain-map mapeia 1:1 para um serviço; nenhuma regra de
  negócio de um domínio é implementada dentro do serviço de outro (ex.: Booking nunca decide autorização de
  pagamento; Payment nunca decide o estado final da reserva; Notification nunca decide regra de negócio).
- **Propriedade dos dados:** proibido, em qualquer circunstância, acessar schema/tabela interna de outro
  domínio — mesmo para leitura. Toda leitura cross-domain passa por API contratada do dono ou por dataset
  publicado com Data Contract.
- **Regras de integração:** toda nova integração entre domínios é classificada como síncrona, assíncrona ou
  dado compartilhado antes de ser implementada. Integrações não classificadas (chamada direta a banco alheio,
  import de código entre serviços) não são permitidas.
- **Acoplamento permitido:** acoplamento é sempre a um contrato versionado (API, evento ou dataset), nunca a um
  detalhe de implementação interno de outro serviço. Mudança incompatível em um contrato exige nova versão,
  mantendo a anterior disponível até todos os consumidores migrarem.
- **Camada anticorrupção:** futuros consumidores externos ao domain-map original (ex.: a Busca da Fase 2) leem
  exclusivamente datasets publicados — nunca chamam serviços internos diretamente —, preservando a
  independência de evolução dos quatro domínios centrais.
- **Disciplina de introdução de tecnologia:** nova tecnologia (broker adicional, cache, ferramenta de
  observabilidade) só entra quando resolve um problema já existente no projeto (regra do roadmap); introdução
  antecipada exige justificativa explícita em uma nova ADR.
- **Registro de decisões:** toda decisão arquitetural nova e material (troca de stack, de estilo arquitetural,
  de broker, de estratégia de dados) é registrada como ADR em `docs/adr/` com contexto autocontido — nunca
  apenas discutida em conversa.
- **Frontend não é domínio:** o frontend de teste/visualização nunca implementa regra de negócio (validação de
  disponibilidade, decisão de confirmação/cancelamento, cálculo de saga) — essas regras vivem exclusivamente em
  Catalog/Booking/Payment. O frontend só chama os contratos já publicados e exibe o resultado; se alguma lógica
  de negócio aparecer no frontend, é sinal de deriva a corrigir, não um atalho de conveniência.
