# Domain Document — Catalog

> **Nível 1 da hierarquia de documentação.** Este documento detalha o bounded context do domínio Catalog. Sempre forneça o `vision.md` junto com este arquivo ao iniciar sessões de PRD ou Tech Spec dentro deste domínio.

**Domínio:** Catalog
**Domain Map:** `context/domain-map.md` — domínio "Catalog"
**Capacidades relacionadas:** nenhum backlog de capacidades formal ainda (`backlog/capabilities.md` não existe nesta etapa)
**Restrições arquiteturais pertinentes:** `context/architecture-baseline.md` não existe ainda; restrições globais herdadas diretamente de `vision.md` §5 (schema `catalog.*` em PostgreSQL único, contratos OpenAPI/AsyncAPI, Data Contract para dataset publicado)
**Responsável:** a definir
**Status:** `planned`
**Fase do Roadmap:** Fase 0 — Fundação
**Última revisão:** 2026-09-12

---

## 1. Propósito do Domínio (Domain Purpose)

### Responsabilidade Principal
Manter o catálogo de hospedagens (Property) e suas unidades reserváveis (Accommodation), e expor consulta de disponibilidade por período e capacidade de hóspedes.

### Problema que Resolve
Sem uma fonte única e confiável do que existe para reservar e do que está livre em um período, Booking não teria como validar uma solicitação de reserva nem o sistema teria uma base estável para features futuras de busca (Fase 2). Catalog concentra essa verdade, isolando o ciclo de vida do inventário do ciclo de vida da reserva.

### Fora do Escopo deste Domínio (Out of Scope)
- Criar, confirmar ou cancelar reservas → pertence a **Booking**.
- Processar autorização, rejeição ou estorno de pagamento → pertence a **Payment**.
- Notificar hóspede/anfitrião sobre resultado de reserva → pertence a **Notification**.
- Upload de imagens, reviews, mapas/geolocalização avançada, pricing dinâmico, cupons, chat ou recommendation engine → non-goals explícitos do sistema (`vision.md` §5), não apenas deste domínio.
- Autenticação/autorização real de Host e Guest → decisão de arquitetura adiada (`vision.md` §5); este documento assume identidade de Host já resolvida por outro mecanismo.
- Múltiplas unidades idênticas por Accommodation (ex.: "5 quartos Standard") → decisão validada nesta etapa: cada Accommodation é uma unidade única reservável, sem contagem de estoque.

---

## 2. Usuários do Domínio (Domain Users)

| Perfil (Role) | O que faz neste domínio | Frequência de uso |
|---|---|---|
| Host | Cadastra e edita Property e Accommodation sob sua propriedade | Ocasional |
| Guest | Consulta Property/Accommodation e disponibilidade por período e nº de hóspedes | Frequente |
| Booking (domínio consumidor) | Consulta existência e disponibilidade de uma Accommodation ao criar uma reserva | Frequente (síncrono, por reserva) |

---

## 3. Entidades Principais (Core Entities)

> Entidades são os objetos de negócio centrais deste domínio. Não é um schema de banco de dados — é o vocabulário do domínio.

| Entidade | Descrição | Atributos Principais | Relacionamentos |
|---|---|---|---|
| Property | Hospedagem cadastrada por um Host | nome, localização (texto livre), Host responsável, status (ativo/inativo) | possui: uma ou mais Accommodation |
| Accommodation | Unidade reservável dentro de uma Property | nome/tipo, capacidade máxima de hóspedes, preço por noite (fixo), status (ativo/inativo) | pertence a: uma Property · possui: zero ou mais Availability Block |
| Availability Block | Registro de que uma Accommodation está indisponível em um intervalo de datas | data de início, data de fim, motivo (origem: reserva em Booking) | pertence a: uma Accommodation |

---

## 4. Features Previstas (Planned Features)

> Lista de features deste domínio. Cada feature marcada como `prd-ready` tem (ou terá) um PRD dedicado.

| # | Feature | Descrição | Prioridade | Status | PRD |
|---|---|---|---|---|---|
| F01 | Cadastro de Property | Host cria e edita uma hospedagem | Must Have | `planned` | — |
| F02 | Cadastro de Accommodation | Host cria e edita uma unidade reservável vinculada a uma Property | Must Have | `planned` | — |
| F03 | Consulta de Property/Accommodation | Guest e Host consultam hospedagens e unidades cadastradas | Must Have | `planned` | — |
| F04 | Consulta de Disponibilidade | Guest e Booking consultam se uma Accommodation está livre em um período para um nº de hóspedes | Must Have | `planned` | — |
| F05 | Atualização de Availability Block por evento | Sistema cria/remove Availability Block reagindo a eventos de ciclo de vida de reserva publicados por Booking | Must Have | `planned` | — |
| F06 | Publicação do dataset `available_accommodations_v1` | Expõe disponibilidade agregada como dataset contratado (Data Contract) para consumidores externos ao Catalog | Should Have | `planned` | — |
| F07 | Desativação de Property/Accommodation | Host desativa hospedagem/unidade sem excluir histórico | Could Have | `planned` | — |

**Prioridades (MoSCoW):** `Must Have` · `Should Have` · `Could Have` · `Won't Have`
**Status possíveis:** `planned` · `prd-ready` · `in-progress` · `done` · `out-of-scope`

---

## 5. Dependências (Domain Dependencies)

### Depende de (Upstream)
| Domínio | O que consome | Tipo | Criticidade |
|---|---|---|---|
| Booking | Eventos de ciclo de vida da reserva (`booking.reservation_confirmed`, `booking.reservation_cancelled`), para manter Availability Block atualizado | Evento | Alta |

### Fornece para (Downstream)
| Domínio | O que fornece | Tipo | Criticidade |
|---|---|---|---|
| Booking | Existência de Accommodation e disponibilidade no período solicitado, na criação de uma reserva | Chamada síncrona (leitura) | Alta |
| Futuros consumidores (ex.: Busca — Fase 2) | Dataset `available_accommodations_v1` | Dados (leitura, contratado) | Baixa nesta fase |

### Integrações Externas (External Integrations)
| Sistema Externo | Finalidade | Direção | Status |
|---|---|---|---|
| — | Nenhuma integração externa prevista nesta fase | — | — |

---

## 6. Regras de Negócio (Business Rules)

> Regras que governam o comportamento deste domínio. Serão referenciadas nos PRDs como critérios de aceitação.

| ID | Regra | Origem |
|---|---|---|
| RN-01 | Uma Accommodation pertence a exatamente uma Property. | Domain Map — fronteira Catalog |
| RN-02 | Apenas Property e Accommodation com status "ativo" podem ser retornadas em consultas de disponibilidade. | Decisão desta etapa |
| RN-03 | O número de hóspedes solicitado em uma consulta de disponibilidade não pode exceder a capacidade máxima da Accommodation. | Decisão desta etapa |
| RN-04 | Uma Accommodation está disponível em um período se não houver Availability Block com sobreposição de datas para esse período. | Decisão desta etapa (bloqueio simples, sem contagem de unidades) |
| RN-05 | Catalog cria um Availability Block ao receber `booking.reservation_confirmed` e o remove ao receber `booking.reservation_cancelled` para a mesma reserva. | Decisão desta etapa |
| RN-06 | O preço por noite de uma Accommodation é fixo e não varia por data, temporada ou demanda. | `vision.md` §5 — non-goal de pricing dinâmico |
| RN-07 | Apenas o Host proprietário de uma Property pode criar ou editar suas Accommodations. | Decisão desta etapa (regra de ownership de negócio; mecanismo de autorização real é decisão de arquitetura adiada) |

---

## 7. Eventos do Domínio (Domain Events)

> Fatos relevantes de negócio que este domínio produz ou consome. Útil para identificar integrações assíncronas.

### Produz (Publishes)
- Nenhum evento assíncrono de domínio nesta fase. Catalog expõe estado via consulta síncrona (F03, F04) e via dataset contratado `available_accommodations_v1` (F06 — Data Contract, não evento).

### Consome (Subscribes)
- `booking.reservation_confirmed` (de: Booking) — cria Availability Block para o período reservado (RN-05).
- `booking.reservation_cancelled` (de: Booking) — remove o Availability Block correspondente (RN-05).

---

## 8. Estratégia de Desenvolvimento (Development Strategy)

### Ordem de Implementação Sugerida
1. [F01] Cadastro de Property — base do domínio, sem dependências.
2. [F02] Cadastro de Accommodation — depende de F01.
3. [F03] Consulta de Property/Accommodation — depende de F01, F02.
4. [F04] Consulta de Disponibilidade — depende de F02; funciona com Availability Block vazio até F05 existir.
5. [F05] Atualização de Availability Block por evento — depende de F02 e do contrato de eventos de Booking já estar definido.
6. [F06] Publicação do dataset `available_accommodations_v1` — depende de F04 estar estável.
7. [F07] Desativação de Property/Accommodation — depende de F01, F02.

### Riscos do Domínio
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Condição de corrida entre a consulta síncrona de disponibilidade (F04) e a criação assíncrona de Availability Block (F05) — uma Accommodation pode ser oferecida como livre e ser reservada duas vezes antes do evento chegar | Média | Alto | Registrado como questão em aberto para o baseline arquitetural (§9); pode exigir hold temporário no momento da consulta ou aceitar reconciliação via compensação em Booking/Payment |
| Contrato de evento de Booking (nome, payload, gatilho exato) ainda não formalizado | Alta | Médio | F05 só entra em `prd-ready` depois que Booking definir o evento de confirmação/cancelamento em seu próprio Domain Doc |

---

## 9. Questões em Aberto (Open Questions)

- [ ] O Availability Block deve ser criado na reserva "solicitada" (mais cedo, reduz corrida, mas pode bloquear período por reservas nunca confirmadas) ou apenas na "confirmada" (mais tarde, mas expõe janela de corrida)? Depende do desenho da saga em Booking.
- [ ] Existe necessidade de um mecanismo de expiração/liberação automática de Availability Block para reservas "solicitadas" que nunca avançam? Fica para o Domain Doc de Booking e para o baseline arquitetural.
- [ ] O mecanismo real de autorização de Host (RN-07) será resolvido em qual etapa — baseline arquitetural ou um PRD específico de identidade?

---

*Domain Doc gerado com a skill `tsg-flow-domain-creator`, a partir de `vision.md` e `context/domain-map.md`. Para criar PRDs das features deste domínio, use a skill `tsg-flow-prd-creator` fornecendo este arquivo e o `vision.md` como contexto.*
