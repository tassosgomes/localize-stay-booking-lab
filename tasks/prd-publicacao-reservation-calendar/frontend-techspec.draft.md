# Especificação Técnica Frontend — Publicação do dataset reservation_calendar_v1

> **PRD de origem:** tasks/prd-publicacao-reservation-calendar/prd.md
> **API Contract:** N/A — F05 não possui integração HTTP ou endpoint; o YAML existente é um envelope OpenAPI schema-only com paths vazio.
> **Data Contract de referência:** tasks/prd-publicacao-reservation-calendar/api-contract.yaml e contracts/data-contracts/reservation_calendar_v1.md
> **Baseline e ADRs:** context/architecture-baseline.md (§frontend de teste, §Data Contract, §propriedade dos dados), ADR-003 e ADR-004
> **TechSpec backend relacionada:** tasks/prd-publicacao-reservation-calendar/techspec.md (Aprovado)
> **Data:** 2026-09-13
> **Status:** Em Revisão
> **Handoff:** draft — não há implementação nem task de frontend para F05

## Resumo Executivo

F05 não adiciona uma jornada visual. O PRD declara que a publicação do
reservation_calendar_v1 não cria tela, botão ou ação para o Guest, e a TechSpec backend confirma
que a única interface é a view PostgreSQL
integration.reservation_calendar_v1. Portanto, esta TechSpec registra a ausência deliberada de
escopo frontend: nenhuma rota, componente, chamada HTTP, estado de tela, tipo gerado, mock ou teste
React deve ser criado para a entrega.

O Data Contract é referência para o consumidor futuro, não um contrato de transporte para o
navegador. O frontend existente continua limitado às jornadas já contratadas de Catalog e Booking
por HTTP; não recebe acesso ao PostgreSQL, às roles de banco ou às tabelas booking.*. Se uma UI
futura precisar exibir esse dataset, deverá existir um PRD próprio para o consumidor e, quando
necessário, uma API HTTP contratada própria.

**Trade-off primário:** deixar a ausência de UI explícita reduz a observabilidade visual imediata do
dataset, mas preserva a fronteira do PRD e evita inventar um dashboard, uma API ou uma conexão de
banco que acoplaria o frontend à implementação de Booking. O comportamento de F05 será validado
pelos testes de contrato, grants e catalogação definidos na TechSpec backend.

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|---|---|---|
| tsg-flow-frontend-techspec-creator | .agents/skills/tsg-flow-frontend-techspec-creator/SKILL.md | Confirmação do escopo UI/API, rastreabilidade, fatias verticais e registro desta especificação como draft |

Nenhuma skill react-* é aplicada nesta feature: não há mudança estrutural, de componente, fetching,
estado, acessibilidade ou teste no frontend. A stack existente foi apenas verificada em
frontend/localize-stay-frontend/package.json, vite.config.ts, App.tsx e nos testes já materializados.

## Fronteira de Escopo Frontend

| Aspecto | Decisão para F05 |
|---|---|
| UI | Nenhuma tela, componente, botão, formulário ou visualização nova |
| Transporte | Nenhum endpoint HTTP; nenhum operationId |
| Fonte de dados | Data Contract SQL-only em integration.reservation_calendar_v1, consumido fora do navegador |
| Acesso | O frontend não recebe credenciais/roles PostgreSQL e não lê booking.* |
| Estado | Nenhum estado server/client/URL novo |
| Testes frontend | Nenhum cenário novo; a evidência pertence à suíte de contrato do backend |
| Navegação | Nenhuma rota ou item de menu novo |

## Mapeamento User Story → Tela → Operação → Teste

As histórias do PRD descrevem consumidores de dados e governança, não uma jornada do frontend de
teste. O mapeamento abaixo torna explícita a ausência de operação visual sem criar um endpoint
implícito.

| User Story | Tela / Componente | OperationId ou ação | Evidência |
|---|---|---|---|
| Consumidor futuro consulta períodos e estados de Reservations por Accommodation | N/A — consumidor futuro, fora de F05 | Leitura autorizada de integration.reservation_calendar_v1; não é operação HTTP | V-01 da TechSpec backend: snapshot, elegibilidade, cardinalidade e fidelidade |
| Consumidor distingue confirmada de cancelada para calcular ocupação ativa | N/A — nenhuma UI nesta feature | Interpretar status do Data Contract; somente confirmada representa ocupação ativa | V-01 da TechSpec backend e testes de contrato dos estados permitidos |
| Autor/arquiteto localiza o dataset e verifica owner/lineage | N/A — catálogo/governança, não frontend | Consulta do ativo no OpenMetadata | V-02 da TechSpec backend: owner Booking, descrição, contrato e lineage |
| Booking compartilha somente o mínimo necessário | N/A — responsabilidade do produtor | Allow-list das sete colunas do Data Contract | V-01/V-02 da TechSpec backend: ausência de campos proibidos, grants e proteção |

Não há endpoint, tela ou teste RTL/Playwright correspondente às linhas acima. Uma futura UI de busca
ou calendário deve ser tratada como consumidor independente, com PRD, contrato de transporte e
Frontend TechSpec próprios.

## Arquitetura de Frontend

### Estrutura de Pastas

Nenhuma pasta ou módulo novo é proposto. A estrutura existente de
frontend/localize-stay-frontend permanece inalterada, incluindo as features
property-registration, reservation-request e reservation-lookup, que não consomem
reservation_calendar_v1.

Não criar src/features/reservation-calendar, adapter para PostgreSQL, service de consulta,
generated/reservationCalendar.ts ou fixture MSW para F05.

### Roteamento

| Rota | Componente | Layout | Auth |
|---|---|---|:---:|
| N/A | Nenhuma página nova | Layout existente sem alteração | N/A |

As rotas atuais /, /properties, /reservations e /reservations/consultar não são alteradas. Nenhuma
entrada nova deve ser adicionada ao roteamento manual de App.tsx.

### Hierarquia de Componentes

Não aplicável. Não existe hierarquia de componentes para F05 e não há estado de tela a modelar.

## Geração de Tipos do API Contract

### Ferramenta Escolhida

- **Ferramenta:** N/A para F05.
- **Comando:** nenhum.
- **Saída:** nenhum arquivo gerado.
- **Regeneração:** não aplicável.

O package.json atual mantém npm run api:generate para os contratos HTTP já consumidos pelas
features existentes. Este draft não adiciona o YAML schema-only de F05 ao script e não cria
reservationCalendar.ts. Gerar tipos para um dataset sem um consumidor frontend seria um artefato
sem uso e poderia sugerir incorretamente que existe uma API de leitura.

Se uma futura feature frontend consumir dados de calendário por uma API, sua TechSpec deverá
referenciar o contrato HTTP dessa feature. Se o consumidor for um serviço de backend, ele deve usar
contracts/data-contracts/reservation_calendar_v1.md e o YAML como fonte do schema, sem expor
credenciais de banco ao browser.

## Estratégia de Fetching

### Biblioteca e Operações

Não há fetching frontend. Nenhum hook, api client, base URL, cache, polling, retry ou atualização
otimista é criado para F05. O frontend não executa SELECT nem abre conexão PostgreSQL.

O fato de a view regular refletir a transição terminal na próxima leitura bem-sucedida é uma
semântica do consumidor do dataset e não uma instrução para adicionar polling ou refresh ao
frontend. A decisão de view ao vivo está registrada na ADR-004 e é implementada pelo backend.

### Tratamento de Erros

Não há resposta HTTP, code, Problem Details ou erro de UI definido por F05. Falhas de DDL,
permissão, qualidade, migration ou ingestão são tratadas nos gates e checkpoints da TechSpec
backend; não devem ser convertidas em mensagens de uma tela inexistente.

## Gerenciamento de Estado

### Server State

N/A. O frontend não consulta reservation_calendar_v1.

### Client State

N/A. Nenhum filtro, seleção de Accommodation, status de carregamento, snapshot ou preferência é
persistido em estado local, URL, localStorage ou sessionStorage.

## Validação de Formulários

N/A. F05 não possui formulário ou entrada do usuário no frontend. As regras de qualidade do
Data Contract — estados elegíveis, intervalo [check_in, check_out), unicidade, allow-list e
updated_at — são verificadas no backend/SQL conforme a TechSpec backend, não duplicadas como
validação client-side.

## Mocks e Ambiente de Desenvolvimento

### Estratégia

- **Durante dev:** não há mock frontend.
- **Durante testes frontend:** não há handler MSW novo.
- **Durante os testes de F05:** usar a fixture PostgreSQL/contrato descrita na TechSpec backend.
- **Prism:** não se aplica, pois o YAML não declara endpoints HTTP.

Não executar ou documentar um servidor Prism para F05 e não incluir handlers que simulem uma API
inexistente. Os exemplos de ReservationCalendarSnapshot no Data Contract servem aos testes de
contrato e a consumidores futuros, não à UI atual.

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Tipo | Skills Aplicáveis | Descrição |
|---|---|---|---|
| tasks/prd-publicacao-reservation-calendar/frontend-techspec.draft.md | Documentação | tsg-flow-frontend-techspec-creator | Registra o resultado da análise: F05 não possui escopo frontend |

Nenhum artefato de aplicação, tipo, rota, componente, mock ou teste frontend é criado.

### Arquivos a Modificar

Nenhum. Em particular, não modificar:

- frontend/localize-stay-frontend/src/App.tsx;
- frontend/localize-stay-frontend/package.json ou package-lock.json;
- frontend/localize-stay-frontend/src/services/api/generated/*;
- frontend/localize-stay-frontend/src/test/mocks/*;
- frontend/localize-stay-frontend/e2e/*.

### Arquivos de Referência (não alterar)

| Caminho | Motivo da Consulta |
|---|---|
| tasks/prd-publicacao-reservation-calendar/prd.md | Escopo, histórias, critérios de aceitação e declaração explícita de ausência de UI |
| tasks/prd-publicacao-reservation-calendar/api-contract.yaml | Schema-only, paths vazio e semântica do Data Contract |
| contracts/data-contracts/reservation_calendar_v1.md | Contrato durável, allow-list e regras de qualidade |
| tasks/prd-publicacao-reservation-calendar/techspec.md | View, grants, testes, fatias V-01/V-02 e governança do backend |
| context/architecture-baseline.md | Frontend sem ownership de dados e regra de integração por contrato |
| docs/adr/adr-003-frontend-teste-react.md | Papel do frontend como ferramenta de teste/visualização |
| docs/adr/adr-004-reservation-calendar-live-view.md | View PostgreSQL ao vivo, sem endpoint HTTP ou componente runtime |
| frontend/localize-stay-frontend/src/App.tsx | Confirmação das rotas e do roteamento atual, sem alteração necessária |
| frontend/localize-stay-frontend/package.json | Confirmação das ferramentas e scripts existentes, sem nova geração |

## Acessibilidade

N/A para F05. Nenhuma interface visual nova é criada, portanto não há novos labels, foco, ARIA,
contraste, teclado ou região de feedback para validar. As interfaces existentes permanecem sujeitas
às suas próprias TechSpecs e testes.

## Internacionalização

N/A. Nenhum texto, mensagem ou formato apresentado ao usuário é adicionado.

## Análise de Impacto

| Componente afetado | Tipo de impacto | Descrição e risco | Ação requerida |
|---|---|---|---|
| Frontend React/Vite | Sem impacto funcional | F05 não cria jornada nem consumidor de dados | Nenhuma alteração |
| App.tsx e navegação | Sem impacto | Não há rota de calendário ou catálogo visual | Não modificar |
| apiClient.ts e env.ts | Sem impacto | Não há URL/endpoint de F05 | Não modificar |
| Tipos gerados | Sem impacto | O YAML não é um contrato HTTP consumido pelo frontend | Não gerar novo arquivo |
| MSW/Vitest/Playwright | Sem impacto | Não há comportamento de UI a provar | Não adicionar handlers ou testes |
| Booking/Data Contract | Fora do frontend | View, migration, grants e OpenMetadata são responsabilidade do backend/governança | Seguir tasks geradas a partir da TechSpec backend |

## Abordagem de Testes

### Testes Unitários

Não há testes unitários frontend para F05. Não existem componentes, hooks, transformações ou
validações client-side desta feature.

### Testes de Integração

Não há teste React Testing Library + MSW para F05. Os cenários de elegibilidade, período,
cardinalidade, monotonicidade, timestamp, allow-list e permissões devem permanecer nos testes de
contrato PostgreSQL definidos como ReservationCalendarContractTests na TechSpec backend.

### Testes E2E

Não há cenário Playwright para F05. Nenhuma jornada de navegador deve ser inventada para substituir
o checkpoint de governança no OpenMetadata ou a leitura por role PostgreSQL.

### Testes de Contrato

A validação é de backend/dados, não de frontend. A TechSpec backend define V-01 para o snapshot e
V-02 para grants, proteção e catalogação, com PostgreSQL descartável e verificação manual do
OpenMetadata. O lint do YAML já está documentado no contrato; esta TechSpec não duplica nem altera
esse gate.

Os comandos npm run test, npm run build, npm run api:generate e npm run api:check continuam sendo
gates do frontend existente, mas não fornecem evidência de F05 e não devem ser alterados para esta
feature.

## Sequenciamento de Desenvolvimento

### Build Order

| Fatia | Jornada e artefatos, incluindo testes | Dependências | Checkpoint |
|---|---|---|---|
| N/A — frontend | Nenhuma fatia frontend; não criar rota, UI, adapter, mock ou teste | Nenhuma dependência frontend | Nenhum checkpoint frontend |
| V-01 — backend/dados | Snapshot elegível, sete colunas, transição persistida e qualidade do contrato | EN-01/F04, migration e PostgreSQL | ReservationCalendarContractTests conforme TechSpec backend |
| V-02 — backend/governança | Grants somente leitura, proteção de booking.* e owner/lineage | V-01, operador de DDL e OpenMetadata | Verificações SQL e catalogação conforme TechSpec backend |

V-01 e V-02 são listadas apenas para deixar explícito onde a evidência de F05 vive; não são tasks
frontend. O Task Creator deve gerar o plano da feature a partir do PRD e da TechSpec backend sem
adicionar uma camada visual artificial.

### Dependências Técnicas Bloqueantes

Não há dependência técnica frontend. A implementação completa de F05 continua sujeita às
dependências do backend: alinhamento de F04 com terminal_transition_at, migrations de Booking,
PostgreSQL, privilégio de DDL e acesso ao OpenMetadata, conforme
tasks/prd-publicacao-reservation-calendar/techspec.md.

## Performance

Sem impacto de performance no frontend: não há código, bundle, request, renderização, cache ou
imagem nova. A decisão de não adicionar polling também evita criar carga de rede sobre um dataset
que não tem transporte HTTP. Questões de custo de leitura da view pertencem à ADR-004 e ao
monitoramento do backend.

## Considerações Técnicas

### Decisões Principais

| Decisão | Racional | Trade-off |
|---|---|---|
| Não criar UI para F05 | O PRD exclui tela e o objetivo é publicar um contrato de dados, não entregar uma experiência Guest | O dataset não terá observação visual nesta entrega; a inspeção ocorre por contrato, SQL e catálogo |
| Tratar o YAML como Data Contract, não como API | paths está vazio, não há status/erro HTTP e o acesso é por role PostgreSQL | Consumidores frontend não podem reutilizar diretamente o apiClient existente |
| Não gerar tipos nem mocks frontend | Não existe consumidor React e gerar/simular uma API sem transporte criaria contrato implícito | Uma futura UI terá custo próprio de contrato, adapter e testes |
| Preservar as rotas e a navegação atuais | F05 não altera as jornadas já materializadas | O frontend não oferece atalho para o dataset nesta fase |
| Delegar a evidência à TechSpec backend | V-01/V-02 já cobrem snapshot, segurança e governança com banco real | A aceitação de F05 exige consultar mais de um gate, não uma suíte React |

### Riscos e Mitigações

- **Uma implementação futura presume que existe GET para o dataset:** manter API Contract como N/A
  e registrar paths vazio; qualquer endpoint novo exige contrato e PRD próprios.
- **Alguém tenta conectar o browser ao PostgreSQL:** proibir credenciais/roles no frontend e manter
  a leitura do dataset restrita a consumidores de backend autorizados.
- **Uma tela futura interpreta cancelada como ocupação:** reutilizar a semântica do Data Contract,
  em que somente confirmada representa ocupação ativa, em uma especificação do consumidor.
- **A ausência de UI é confundida com ausência de entrega:** usar os checkpoints V-01/V-02,
  incluindo qualidade, grants e OpenMetadata, como evidência de conclusão.

### Conformidade com Skills

| Decisão | Skill de Referência | Conforme? |
|---|---|:---:|
| Registrar formalmente uma TechSpec frontend sem escopo visual | tsg-flow-frontend-techspec-creator | ✅ |
| Não inventar endpoint quando o contrato tem paths vazio | tsg-flow-frontend-techspec-creator | ✅ |
| Não criar estrutura React sem jornada | tsg-flow-frontend-techspec-creator | ✅ |
| Usar MSW/Playwright somente quando houver comportamento de UI | react-testing | N/A — skill não aplicada |

## Questões em Aberto

Não há questão aberta de frontend que bloqueie a entrega desta especificação.

As pendências operacionais do Data Contract — como owner nominal no OpenMetadata, retenção futura e
grants para consumidores externos — permanecem no escopo de backend/governança e estão registradas
em tasks/prd-publicacao-reservation-calendar/techspec.md e no contrato durável. Elas não autorizam
criar uma UI ou uma API neste PRD.

## Architecture Decision Records

### Herdadas

- [ADR-003 — Frontend de teste/visualização: React, sem gateway/BFF na Fase 0](../../docs/adr/adr-003-frontend-teste-react.md) — define o frontend como cliente fino de APIs públicas, sem ownership de dados; não amplia o frontend para datasets sem transporte.
- [ADR-004 — Publicação do calendário como view PostgreSQL ao vivo](../../docs/adr/adr-004-reservation-calendar-live-view.md) — confirma que reservation_calendar_v1 é uma view de dados compartilhados, sem endpoint HTTP ou mecanismo de runtime no frontend.

### Criadas nesta sessão

Nenhuma. A decisão de não criar uma UI é delimitação de escopo e reutiliza o PRD, o baseline e as
ADRs existentes; não introduz decisão arquitetural nova.

## Próximos Passos

1. Após aprovação deste draft, promovê-lo para
   tasks/prd-publicacao-reservation-calendar/frontend-techspec.md sem alterar o conteúdo de escopo
   N/A.
2. Encaminhar PRD, Data Contract e a TechSpec backend ao tsg-flow-task-creator; gerar somente as
   tasks V-01/V-02 e o habilitador de backend já descritos na TechSpec backend.
3. Executar os gates de dados e a verificação de OpenMetadata definidos na TechSpec backend. Não há
   comando de geração de tipos ou servidor mock frontend para F05.
4. Se surgir uma necessidade de calendário, busca ou dashboard, abrir PRD próprio e especificar o
   consumidor; se houver transporte HTTP, usar tsg-flow-contract-creator antes da nova Frontend
   TechSpec.
