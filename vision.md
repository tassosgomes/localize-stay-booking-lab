# Vision Document — Localize Stay v2

> **Nível 0 da hierarquia de documentação.** Este documento é a âncora de contexto para todos os Domain Docs, PRDs, Tech Specs e Tasks do projeto. Sempre que iniciar uma nova sessão com a IA, forneça este arquivo como contexto.
>
> **Natureza do projeto:** este é um projeto de **estudo/laboratório pessoal** de arquitetura distribuída, sem fins comerciais, sem usuários reais e sem dados reais de pagamento. Não é (e não pretende se tornar) um clone funcional de Airbnb/Booking. Onde este documento menciona "negócio", "objetivo" ou "público-alvo", trata-se do objetivo de aprendizado do autor, não de um objetivo comercial.

---

## 1. Visão Geral do Sistema (System Overview)

### Problema de Negócio
Quem estuda arquitetura distribuída na teoria tem dificuldade em praticar decisões reais — contratos entre serviços, sagas com compensação, ownership de dados, catalogação de metadados e evolução de plataforma — porque a maioria dos projetos de estudo ou é simples demais (CRUD/tutorial) ou é complexa demais em escopo de produto (tentando clonar um produto real), o que desvia o esforço da profundidade técnica para a superfície de produto.

O Localize Stay v2 resolve isso reduzindo deliberadamente o domínio de negócio a um recorte mínimo (hospedagens, reserva, pagamento simulado) para concentrar o esforço de aprendizado na engenharia: contratos formais, sagas, idempotência, versionamento de eventos e catalogação de ativos — evoluindo por fases ao longo do tempo.

### Solução Proposta
Um sistema fictício de hospedagens, dividido em serviços com ownership de dados próprio, que permite cadastrar/consultar acomodações, consultar disponibilidade, criar reservas e executar uma saga de pagamento simulado (autorização, confirmação ou cancelamento/compensação). A integração entre serviços é sempre feita through contratos explícitos e versionados (chamadas síncronas via OpenAPI, eventos via AsyncAPI, dados compartilhados via Data Contracts), e os ativos, ownership e relacionamentos são catalogados no OpenMetadata.

O sistema evolui por fases (fundação → resiliência → busca → plataforma cloud → escala), cada uma introduzindo uma nova tecnologia apenas quando ela resolve um problema já existente no projeto — nunca por antecipação.

### Público-Alvo (Target Audience)
| Perfil (Role) | Descrição | Necessidade Principal |
|---|---|---|
| Autor / arquiteto em estudo | Pessoa conduzindo o laboratório para praticar arquitetura distribuída na prática | Um ambiente controlado, pequeno e realista o suficiente para tomar e documentar decisões arquiteturais reais |
| Leitores da trilha de artigos (Medium) | Profissionais de tecnologia interessados em arquitetura de software | Exemplos concretos, incrementais e verificáveis de decisões arquiteturais aplicadas a um caso real (ainda que de escopo reduzido) |

### Contexto de Entrada
- [x] Ideia nova (greenfield), com fins de estudo
- [ ] Discovery com cliente
- [ ] Modernização de sistema legado

Não há sistema legado, cliente real ou usuários finais reais. O projeto não tem finalidade comercial; qualquer decisão de escopo prioriza valor de aprendizado sobre completude de produto.

---

## 2. Domínios Identificados (Domain Map)

**Não aplicável nesta etapa** — a decomposição formal de domínios (bounded contexts) é responsabilidade da etapa seguinte, `tsg-flow-domain-decomposer`, e não deve ser antecipada na Vision.

Para contexto, o brief de arquitetura já aponta serviços candidatos que deverão ser validados/formalizados naquela etapa: **Catalog** (hospedagens/disponibilidade), **Booking** (reserva e saga), **Payment** (autorização/estorno simulados) e **Notification Worker** (consumidor assíncrono de eventos finais, sem regra central de negócio).

---

## 3. Mapa de Interdependências (Dependency Map)

**Não aplicável nesta etapa** — depende da decomposição formal de domínios (seção 2). Registrar apenas a regra estrutural já definida no brief: um serviço não acessa tabelas internas de outro domínio; todo compartilhamento de dados entre domínios ocorre via view/materialized view publicada e contratada (Data Contract).

---

## 4. Roadmap Macro (High-Level Roadmap)

> Fases retiradas do brief de arquitetura. Domínios formais (D01, D02...) serão atribuídos após o Domain Decomposer; aqui ficam apenas os temas/tecnologias candidatas por fase, como restrição estratégica já registrada no brief — não como decisão de arquitetura desta Vision.

### Fase 0 — Fundação
**Objetivo:** implementar o escopo funcional congelado e os três estilos de contrato (OpenAPI, AsyncAPI, Data Contract).
**Temas/tecnologias candidatas:** Service-Based Architecture, PostgreSQL, OpenAPI, AsyncAPI, OpenMetadata.
**Critério de conclusão:** uma reserva percorre o fluxo completo de sucesso e de falha; OpenAPI e AsyncAPI descrevem as APIs e eventos públicos; ao menos um Data Contract protege uma view publicada entre domínios; OpenMetadata apresenta os principais ativos, ownership e relacionamentos; o README explica por que cada integração usa API, evento ou contrato de dados; nenhuma funcionalidade fora do escopo congelado está presente.

### Fase 1 — Resiliência
**Objetivo:** tornar a saga robusta e observável.
**Temas/tecnologias candidatas:** Outbox, retries, DLQ, tracing, metrics, contract tests.

### Fase 2 — Busca
**Objetivo:** criar busca desacoplada sem alterar o domínio principal.
**Temas/tecnologias candidatas:** Elasticsearch/OpenSearch, CDC, Debezium, Kafka, indexação eventual.

### Fase 3 — Plataforma cloud
**Objetivo:** explorar implantação e trade-offs entre provedores de nuvem.
**Temas/tecnologias candidatas:** AWS, GCP ou Azure; managed DB, broker, containers, secrets, IAM.

### Fase 4 — Escala e entrega
**Objetivo:** estudar distribuição, performance e operação.
**Temas/tecnologias candidatas:** CDN, cache, autoscaling, load tests, SLOs, DR.

> Regra do roadmap (do brief): cada nova tecnologia entra somente quando resolve um problema que já existe no projeto. O roadmap é uma fila de estudos, não um checklist obrigatório.

---

## 5. Restrições Globais (Global Constraints)

### Restrições Técnicas (Technical Constraints)
- **Persistência:** uma única instância PostgreSQL, com ownership lógico por schema (`catalog.*`, `booking.*`, `payment.*`) e um schema de integração para dados publicados. Nenhum serviço acessa tabelas internas de outro domínio.
- **Contratos obrigatórios:** chamadas síncronas descritas em OpenAPI; comunicação assíncrona descrita em AsyncAPI; compartilhamento de dados entre domínios feito somente via view/materialized view publicada e protegida por Data Contract.
- **Catalogação:** ativos, ownership e relacionamentos (APIs, tópicos/datasets) devem ser registrados no OpenMetadata.
- **Linguagem/framework de aplicação:** não definidos nesta etapa — é decisão de arquitetura (baseline arquitetural), não desta Vision.
- **Infraestrutura de execução (containers, cloud, Kubernetes):** fora de escopo na Fase 0; exploração de cloud e escala fica reservada às Fases 3 e 4.
- **Autenticação/autorização:** não especificada no brief; decisão adiada para etapas de arquitetura/PRD posteriores.

### Restrições de Negócio (Business Constraints)
- **Prazo:** sem prazo comercial. Horizonte de estudo aproximado de um ano, evoluindo por fases, sem obrigatoriedade de concluir o roadmap inteiro.
- **Orçamento:** projeto pessoal de estudo, sem orçamento formal alocado.
- **Regulatório:** não aplicável — dados fictícios, sem PII real, sem transações financeiras reais.
- **Dados legados:** não aplicável — projeto greenfield.

### Non-Goals do Sistema
- Não é um produto comercial nem substituto de plataformas reais de hospedagem (Airbnb/Booking).
- Não processará pagamentos reais nem integrará gateways reais (ex.: Stripe) — pagamento é sempre simulado.
- Não incluirá upload de imagens, reviews, mapas/geolocalização avançada, pricing dinâmico, cupons, chat ou recommendation engine.
- Não incluirá busca via Elasticsearch, Kubernetes, multi-tenancy, observabilidade avançada, CDN ou multi-cloud na etapa inicial (ficam reservados a fases futuras do roadmap, se e quando fizerem sentido).

---

## 6. Glossário de Negócio (Business Glossary)

| Termo | Definição | Domínio(s) candidato(s) |
|---|---|---|
| Property | Hospedagem cadastrada (o imóvel/estabelecimento). | Catalog |
| Accommodation / Room | Unidade reservável dentro de uma Property. | Catalog |
| Reservation | Registro de reserva feita por um hóspede, com estados de ciclo de vida (solicitada, confirmada, cancelada). | Booking |
| Payment | Registro de autorização, rejeição ou estorno de pagamento simulado. | Payment |
| Saga | Sequência de passos e compensações que coordena reserva e pagamento entre serviços. | Booking, Payment |
| Data Contract | Contrato que protege um dataset publicado (view/materialized view) consumido por outros domínios. | Todos (integração) |
| `available_accommodations_v1` | Dataset publicado de disponibilidade de acomodações. | Catalog |
| `reservation_calendar_v1` | Dataset publicado de calendário de reservas. | Booking |
| Ownership lógico | Regra de que cada schema/domínio é dono exclusivo de suas tabelas internas; acesso externo só via contrato publicado. | Todos |

---

## 7. Premissas e Riscos Globais (Assumptions & Risks)

### Premissas (Assumptions)
- O autor conduz o projeto individualmente, sem dependência de aprovação de terceiros para decisões de escopo.
- O ambiente de desenvolvimento local é suficiente para as Fases 0 e 1; infraestrutura de nuvem só é necessária a partir da Fase 3.
- As ferramentas de contrato (OpenAPI/AsyncAPI) e catalogação (OpenMetadata) são adotáveis sem custo relevante para uso de estudo.

### Riscos Globais (Global Risks)
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Escopo crescer além do congelado por curiosidade técnica (feature creep de produto) | Média | Alto | Tratar o escopo funcional congelado do brief como contrato; nova tecnologia só entra quando resolve problema já existente (regra do roadmap) |
| Avançar para tópicos de fases futuras (busca, cloud, escala) antes de consolidar a fundação | Média | Médio | Seguir a ordem das fases do roadmap; cada fase só começa após a anterior atender seu critério de conclusão |
| Descontinuidade do projeto por ser um esforço pessoal/paralelo | Média | Médio | Ausência de prazos rígidos; roadmap tratado como fila de estudos, não checklist obrigatório |

---

## 8. Histórico de Revisões (Revision History)

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 0.1 | 2026-09-11 | Tasso Gomes | Versão inicial, gerada a partir de `docs/brief.md` |

---

*Vision Doc gerado com a skill `tsg-flow-vision-creator`, a partir de `docs/brief.md`. Próximo passo sugerido: `tsg-flow-domain-decomposer`, para gerar `context/domain-map.md`.*
