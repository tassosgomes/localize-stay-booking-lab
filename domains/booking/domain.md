# Domain Document — Booking

> **Nível 1 da hierarquia de documentação.** Este documento detalha o bounded context do domínio Booking. Sempre forneça o `vision.md` junto com este arquivo ao iniciar sessões de PRD ou Tech Spec dentro deste domínio.

**Domínio:** Booking  
**Domain Map:** `context/domain-map.md` — domínio "Booking"  
**Capacidades relacionadas:** nenhum backlog de capacidades formal (`backlog/capabilities.md` não existe nesta etapa)  
**Restrições arquiteturais pertinentes:** `context/architecture-baseline.md` — serviço próprio em .NET/C#, schema `booking.*`, API OpenAPI, eventos AsyncAPI via RabbitMQ, dataset contratado e proibição de acesso direto a dados de outros domínios  
**Responsável:** a definir  
**Status:** `planned`  
**Fase do Roadmap:** Fase 0 — Fundação; evolução de resiliência na Fase 1  
**Última revisão:** 2026-09-12

---

## 1. Propósito do Domínio (Domain Purpose)

### Responsabilidade Principal
Criar reservas, conduzir seu ciclo de vida e coordenar a saga que transforma uma solicitação válida em reserva confirmada ou cancelada conforme o resultado do pagamento simulado.

### Problema que Resolve
Uma solicitação de hospedagem atravessa verdades pertencentes a domínios distintos: Catalog sabe se a Accommodation existe e está disponível, Payment decide se o pagamento pode ser autorizado e Booking decide o estado final da Reservation. Booking mantém essa jornada coerente e rastreável sem assumir regras internas dos demais domínios.

Na Fase 0, o fluxo cobre autorização e rejeição de pagamento. Timeout, retries, idempotência robusta e compensações para falhas tardias aprofundam a mesma saga na Fase 1, sem alterar a fronteira do domínio.

### Fora do Escopo deste Domínio (Out of Scope)
- Manter Property, Accommodation, preço corrente ou Availability Block → pertence a **Catalog**.
- Autorizar, rejeitar ou estornar pagamento → pertence a **Payment**; Booking apenas solicita e reage ao resultado.
- Entregar mensagens ao Guest ou Host → pertence a **Notification**; Booking publica somente o resultado final.
- Bloquear datas enquanto a reserva está solicitada → não ocorre na Fase 0; Catalog cria o bloqueio apenas após a confirmação.
- Cancelamento voluntário pelo Guest ou Host, alteração de datas e reembolso por política comercial → fora do escopo funcional congelado.
- Timeout, retries, DLQ e tratamento de resultado tardio → Fase 1 — Resiliência.
- Autenticação/autorização, pagamento real, cupons, pricing dinâmico e múltiplas moedas → non-goals ou decisões adiadas pela Vision.

---

## 2. Usuários do Domínio (Domain Users)

| Perfil (Role) | O que faz neste domínio | Frequência de uso |
|---|---|---|
| Guest | Solicita uma reserva e consulta seu estado até a confirmação ou o cancelamento | Ocasional |
| Autor / arquiteto em estudo | Executa e observa os caminhos de sucesso e rejeição da saga | Frequente durante o laboratório |
| Frontend de teste | Envia solicitações e apresenta o estado da Reservation e da saga | Frequente durante desenvolvimento e demonstrações |

---

## 3. Entidades Principais (Core Entities)

> Entidades são objetos de negócio deste domínio, não estruturas de persistência.

| Entidade | Descrição | Atributos Principais | Relacionamentos |
|---|---|---|---|
| Reservation | Compromisso solicitado por um Guest para ocupar uma Accommodation durante um período | identificador, Guest de referência, Accommodation de referência, check-in, check-out, quantidade de hóspedes, preço por noite congelado, valor total, moeda, estado | possui: uma Reservation Saga; referencia: uma Accommodation do Catalog |
| Reservation Saga | Acompanhamento da jornada entre solicitação de reserva e resultado do pagamento | identificador de correlação, estado da jornada, solicitação de pagamento, resultado recebido, motivo de cancelamento | pertence a: uma Reservation; relaciona: resultado de Payment |

Os estados de negócio da Reservation na Fase 0 são `solicitada`, `confirmada` e `cancelada`. A Reservation Saga distingue ao menos pagamento pendente, autorizado e rejeitado para explicar como o estado final foi alcançado.

---

## 4. Features Previstas (Planned Features)

| # | Feature | Descrição | Prioridade | Status | PRD |
|---|---|---|---|---|---|
| F01 | Solicitação de Reserva | Guest informa Accommodation, período e hóspedes; Booking valida a disponibilidade no Catalog e congela o preço da estadia | Must Have | `prd-done` | [`tasks/prd-solicitacao-reserva/prd.md`](../../tasks/prd-solicitacao-reserva/prd.md) |
| F02 | Consulta de Reserva | Permite acompanhar dados, estado final e situação da saga de uma Reservation | Must Have | `prd-done` | [`tasks/prd-consulta-reserva/prd.md`](../../tasks/prd-consulta-reserva/prd.md) |
| F03 | Solicitação de Pagamento | Inicia a etapa assíncrona da saga para uma Reservation válida e solicitada | Must Have | `prd-done` | [`tasks/prd-solicitacao-pagamento/prd.md`](../../tasks/prd-solicitacao-pagamento/prd.md) |
| F04 | Conclusão da Saga | Confirma a Reservation após autorização ou a cancela após rejeição, publicando o resultado final | Must Have | `prd-ready` | [`tasks/prd-conclusao-saga/prd.md`](../../tasks/prd-conclusao-saga/prd.md) |
| F05 | Publicação do dataset `reservation_calendar_v1` | Expõe o calendário de reservas como dataset contratado sem revelar dados internos de Booking | Should Have | `prd-ready` | [`tasks/prd-publicacao-reservation-calendar/prd.md`](../../tasks/prd-publicacao-reservation-calendar/prd.md) |
| F06 | Resiliência da Saga | Trata timeout, retries, idempotência, resultado tardio e compensação de pagamento autorizado quando a confirmação não puder ser concluída | Should Have | `planned` | — |

**Prioridades (MoSCoW):** `Must Have` · `Should Have` · `Could Have` · `Won't Have`  
**Status possíveis:** `planned` · `prd-ready` · `in-progress` · `done` · `out-of-scope`

---

## 5. Dependências (Domain Dependencies)

### Depende de (Upstream)
| Domínio | O que consome | Tipo | Criticidade |
|---|---|---|---|
| Catalog | Existência, status, capacidade, disponibilidade, preço por noite e moeda da Accommodation para o período solicitado | Chamada síncrona (leitura) | Alta |
| Payment | Resultado da autorização (`payment.payment_authorized` ou `payment.payment_rejected`) | Evento | Alta |

### Fornece para (Downstream)
| Domínio | O que fornece | Tipo | Criticidade |
|---|---|---|---|
| Payment | Solicitação de pagamento vinculada à Reservation | Evento | Alta |
| Catalog | Confirmação ou cancelamento para criação/remoção idempotente do Availability Block | Evento | Alta |
| Notification | Resultado final da Reservation para notificação das partes interessadas | Evento | Média |
| Futuros consumidores | Dataset `reservation_calendar_v1` | Dados (leitura, contratado) | Baixa nesta fase |

### Integrações Externas (External Integrations)
| Sistema Externo | Finalidade | Direção | Status |
|---|---|---|---|
| Frontend de teste/visualização | Solicitar e acompanhar reservas pela API pública de Booking | Entrada | `planned` |
| — | Nenhum sistema comercial ou gateway real previsto | — | — |

---

## 6. Regras de Negócio (Business Rules)

| ID | Regra | Origem |
|---|---|---|
| RN-01 | Uma Reservation referencia exatamente uma Accommodation e um Guest de referência. | Domain Map — fronteira Booking |
| RN-02 | Check-out deve ser posterior ao check-in; o período é interpretado como `[check-in, check-out)`, resultando em ao menos uma noite. | Decisão desta etapa |
| RN-03 | A quantidade de hóspedes deve ser positiva e não pode exceder a capacidade informada pelo Catalog. | `domains/catalog/domain.md` RN-03 |
| RN-04 | Booking só cria uma Reservation solicitada após o Catalog confirmar, de forma síncrona, que Property e Accommodation estão ativas e que a Accommodation está disponível no período. | Baseline arquitetural; Catalog RN-02 e RN-04 |
| RN-05 | Booking registra na Reservation o preço por noite e a moeda devolvidos pelo Catalog no momento da solicitação; esse valor não muda se o catálogo for alterado depois. | Decisão aprovada nesta etapa |
| RN-06 | O valor total solicitado a Payment é o número de noites multiplicado pelo preço por noite congelado na Reservation. | Decisão aprovada nesta etapa; Catalog RN-06 |
| RN-07 | Uma Reservation solicitada só pode ser confirmada após `payment.payment_authorized` correspondente à mesma saga. | Vision e Domain Map |
| RN-08 | Uma Reservation solicitada é cancelada após `payment.payment_rejected`; a rejeição não autoriza Booking a alterar o Payment. | Vision e Domain Map |
| RN-09 | Na Fase 0, Catalog recebe `booking.reservation_confirmed` para criar o Availability Block; nenhuma data é bloqueada enquanto a Reservation está apenas solicitada. | Decisão aprovada nesta etapa; Catalog §9 |
| RN-10 | Cada resultado final da saga produz exatamente um estado terminal observável da Reservation: confirmada ou cancelada. | Critério de conclusão da Fase 0 |
| RN-11 | Uma Reservation em estado terminal não pode regressar a solicitada nem trocar diretamente para o outro estado terminal. | Consistência do ciclo de vida |
| RN-12 | Cancelamento voluntário e alteração de uma Reservation não são permitidos na Fase 0. | Escopo funcional congelado |

---

## 7. Eventos do Domínio (Domain Events)

### Produz (Publishes)
- `booking.reservation_requested` — registra que uma solicitação válida foi criada; não possui consumidor obrigatório na Fase 0.
- `booking.payment_requested` — solicita a Payment autorização do valor congelado para a Reservation.
- `booking.reservation_confirmed` — informa Catalog e Notification que a Reservation chegou ao estado confirmado.
- `booking.reservation_cancelled` — informa Catalog e Notification que a Reservation chegou ao estado cancelado após rejeição.

### Consome (Subscribes)
- `payment.payment_authorized` (de: Payment) — conclui com confirmação a saga correspondente.
- `payment.payment_rejected` (de: Payment) — conclui com cancelamento a saga correspondente.

Os contratos técnicos versionados podem representar esses nomes com versão no tipo (por exemplo, `PaymentAuthorized.v1`), conforme o baseline. Todo evento da saga carrega `correlationId` e `causationId`; a forma do payload será definida no contrato AsyncAPI.

---

## 8. Estratégia de Desenvolvimento (Development Strategy)

### Ordem de Implementação Sugerida
1. [F01] Solicitação de Reserva — estabelece Reservation, regras de período e integração síncrona com Catalog.
2. [F02] Consulta de Reserva — torna o estado da jornada observável no laboratório.
3. [F03] Solicitação de Pagamento — inicia a integração assíncrona com Payment.
4. [F04] Conclusão da Saga — fecha os caminhos de autorização e rejeição e habilita Catalog/Notification.
5. [F05] Publicação de `reservation_calendar_v1` — expõe o estado estável por Data Contract.
6. [F06] Resiliência da Saga — evolui o fluxo já funcional na Fase 1.

### Riscos do Domínio
| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Duas solicitações podem validar a mesma Accommodation antes que a primeira seja confirmada e bloqueada no Catalog | Média | Alto | Aceitar e demonstrar a janela na Fase 0; definir reconciliação/compensação no PRD da F06 |
| Falha depois da autorização e antes da confirmação pode deixar Payment e Reservation divergentes | Média | Alto | Cobrir o caminho nominal na Fase 0 e especificar resultado tardio, retry e estorno compensatório na F06 |
| Eventos duplicados ou fora de ordem podem tentar concluir novamente uma saga | Média | Alto | Tornar estados terminais monotônicos na Fase 0 (RN-11) e aprofundar idempotência na F06 |
| Mudança no preço do Catalog durante a saga pode alterar o valor esperado | Baixa | Médio | Congelar preço, moeda e total na criação da Reservation (RN-05 e RN-06) |

---

## 9. Questões em Aberto (Open Questions)

- [x] Moeda única definida no PRD de F01: BRL, fixa em todo o laboratório (`docs/product-decisions/PD-001-moeda-unica-laboratorio.md`).
- [x] Definido no PRD de F05 quais estados entram em `reservation_calendar_v1` e a política de atualização do dataset: `confirmada` e `cancelada` entram; `solicitada` fica fora; o dataset mantém um snapshot atual, atualizado após a transição terminal persistida.
- [ ] Na Fase 1, definir prazo de timeout, política de retry e comportamento para autorização recebida depois do cancelamento.
- [ ] Na Fase 1, definir quando Booking solicita estorno a Payment se uma autorização não puder resultar em confirmação.

---

*Domain Doc gerado com a skill `tsg-flow-domain-creator`, a partir de `vision.md`, `context/domain-map.md`, `context/architecture-baseline.md` e `domains/catalog/domain.md`. Para criar PRDs das features deste domínio, use a skill `tsg-flow-prd-creator` fornecendo este arquivo e o `vision.md` como contexto.*
