---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: true
blocked_by: [4.0]
---

<task_context>
<domain>frontend/reservation-request/journey</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>http_server</dependencies>
<unblocks>"6.0"</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="ReservationRequestPage"` prova, via RTL + MSW, os 7 desfechos do contrato (201 + 400 + 5×422 + 503) no mesmo formulário, com validação local, prevenção de duplo submit, preservação de valores em erro e foco acessível; `reservationFormValidation.test.ts` e `reservationErrorMapping.test.ts` (funções puras) passam no mesmo gate</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="ReservationRequestPage"</gate_command>
<gate_test_selector>Arquivo `frontend/localize-stay-frontend/src/features/reservation-request/pages/ReservationRequestPage.test.tsx` (mais `reservationFormValidation.test.ts` e `reservationErrorMapping.test.ts`, colocalizados na mesma feature)</gate_test_selector>
<gate_expected_result>Filtro encontra testes; todos passam; `/reservations` aceita um request válido e mostra o resumo congelado; cada um dos 6 desfechos de erro (400, 5×422, 503) mostra mensagem/tom/campo corretos e preserva os valores digitados; loading impede duplo submit; build e type-check permanecem verdes</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Guest abre `/reservations`, preenche Accommodation/Guest de referência/período/hóspedes e submete; recebe, no mesmo incremento, os 7 desfechos do contrato (sucesso com resumo congelado; período inválido; hóspedes inválidos; acomodação indisponível; capacidade excedida; período indisponível; Catalog indisponível) de forma acessível — feature "única e indivisível" conforme o PRD, não fatiada por tipo de rejeição</vertical_slice>
</task_context>

# Tarefa 5.0: Solicitar reserva pela interface acessível: 7 desfechos (V-FE-01)

## Relacionada às User Stories

- "Como Guest, eu quero solicitar uma reserva informando a Accommodation, o período e o número de
  hóspedes..." (cobertura direta na UI)
- "Como Guest, eu quero ser informado imediatamente quando meu pedido não pode ser aceito (período
  inválido, capacidade excedida, acomodação indisponível)..." (cobertura direta na UI)
- "Como frontend de teste, eu quero enviar a solicitação de reserva e exibir o resultado (sucesso com
  dados congelados, ou rejeição com motivo)..." (cobertura direta — é exatamente esta task)

## Visão Geral

Entrega RF-01 completo no frontend sobre a integração tipada da 4.0: a rota `/reservations` renderiza
`ReservationRequestPage`, que orquestra `RequestReservationForm` (Accommodation, Guest de referência,
período, hóspedes), valida localmente período/hóspedes antes de qualquer request, envia
`reservationApi.request` e transiciona entre `idle → submitting → requested (201) | rejected (422) |
unavailable (503) | failed (500/rede)`. Em sucesso, exibe `ReservationResultSummary` com ID, status
`solicitada` e valores congelados; em qualquer falha, retorna ao formulário editável com os valores
preservados. É a única fatia de UI porque o PRD marca RF-01 como "feature única e indivisível para
efeito de entrega" (Plano de Rollout Faseado) — fatiar por tipo de rejeição na UI recriaria, no
frontend, o rollout interno que o PRD já proíbe no backend (`frontend-techspec.md`
§Sequenciamento de Desenvolvimento).

## Entrega Observável

- **Entrada ou gatilho:** abrir `/reservations`, preencher os 4 campos e submeter, para cada um dos 7
  cenários de resposta do contrato (mockados via MSW nesta task; o backend real só é exercitado em
  6.0).
- **Resultado esperado:**
  - Válido + `201` → formulário é substituído por `ReservationResultSummary` (ID, `solicitada`,
    preço/moeda/total, nota de que o pagamento ainda não foi solicitado); botão "Nova solicitação"
    reseta para `idle` com formulário vazio.
  - `checkOut <= checkIn` (validado localmente, sem request) ou `422 PERIODO_INVALIDO` (vindo do
    backend) → erro associado a `checkIn`/`checkOut` no `FormErrorSummary`, foco move para o resumo.
  - `guestsCount <= 0` (local) ou `422 QUANTIDADE_HOSPEDES_INVALIDA` → erro associado a `guestsCount`.
  - `422 ACOMODACAO_INDISPONIVEL` → erro associado a `accommodationId`.
  - `422 CAPACIDADE_EXCEDIDA` → erro associado a `guestsCount` (com referência a `accommodationId`).
  - `422 PERIODO_INDISPONIVEL` → erro associado a `checkIn`/`checkOut`.
  - `400 VALIDATION_ERROR` → mensagem genérica de requisição malformada, sem campo específico
    (contrato não expõe `details[].field`).
  - `503 CATALOG_INDISPONIVEL` → `OperationFeedback` com `role="status"` e tom de falha temporária
    ("não foi possível validar agora, tente novamente"), **nunca** como erro de formulário.
  - `500`/falha de rede → mensagem genérica + `traceId` (quando presente) via `role="alert"`.
  - Em toda falha/rejeição, os valores digitados são preservados e o Guest pode reenviar.
- **Checkpoint de feedback:** `gate.sh --filter="ReservationRequestPage"` (RTL + MSW + `userEvent`).
- **Seletor focalizado:** `ReservationRequestPage.test.tsx`
  (+ `reservationFormValidation.test.ts`, `reservationErrorMapping.test.ts`).
- **Fora deste checkpoint:** backend/Catalog reais (isso é 6.0, via Playwright); consulta de uma
  Reservation já criada (F02); avanço da saga de pagamento (F03).

## Requisitos

- Evoluir/consolidar a estrutura intermediária (`src/features/reservation-request/{api,components,
  pages,types,validation,errors}/`, `index.ts` exportando só `ReservationRequestPage`) — mesma
  convenção de `prd-cadastro-property`, sem introduzir uma segunda organização.
- Registrar `/reservations` no mecanismo de navegação existente (`App.tsx` ou router já
  materializado pela Fundação/`prd-cadastro-property`); não criar `/reservations/:id` (fora de
  escopo — F02).
- `reservationFormValidation.ts` (funções puras, sem I/O): `accommodationId` obrigatório e formato
  UUID; `guestReference` obrigatório, ≥1 caractere não branco, máx. 255, sem `trim()` silencioso;
  `checkIn`/`checkOut` obrigatórios, `checkOut` posterior a `checkIn`; `guestsCount` obrigatório,
  inteiro, mínimo 1. Validação local cobre **apenas** período/hóspedes/shape — capacidade,
  disponibilidade e existência/status da Accommodation não são simuladas no cliente (dependem da
  resposta do backend).
- `reservationErrorMapping.ts` (função pura): mapeia cada um dos 5 `code` de 422 a
  campo(s)/mensagem; mapeia `503` a tom "falha temporária" (não erro de campo); mapeia `400`/`500`/
  desconhecido a mensagem genérica. Não interpreta `title`/`detail` do `ProblemDetails` para decidir
  comportamento — só `code`.
- `<input type="date">` para check-in/check-out (produz `yyyy-MM-dd` nativamente, mesmo formato do
  contrato); sem biblioteca de formulário.
- `AbortController` por submit; desmontagem da página cancela a requisição pendente; botão de submit
  desabilitado durante `submitting` para impedir duplo envio; leitura do formulário não é bloqueada
  durante loading.
- Acessibilidade (WCAG 2.1 AA da fatia): todo input com `label`; erro usa `aria-invalid` +
  `aria-describedby`; `FormErrorSummary` recebe foco após submit inválido, 400 ou 422; ao exibir
  `201`, mover foco para o título do resumo sem retirar controle do teclado; `OperationFeedback` usa
  `role="status"` para sucesso/loading/503 e `role="alert"` para erro real (400/500/rede).
- Criar `FormErrorSummary.tsx`/`OperationFeedback.tsx` em `src/components/` **somente se ainda não
  existirem** (podem já ter sido criados por `prd-cadastro-property`); reaproveitar sem duplicar.
- Textos em português; sem biblioteca de i18n.

## Arquivos Envolvidos

- **Criar:**
  - `frontend/localize-stay-frontend/src/features/reservation-request/index.ts`
  - `frontend/localize-stay-frontend/src/features/reservation-request/pages/ReservationRequestPage.tsx`
  - `frontend/localize-stay-frontend/src/features/reservation-request/pages/ReservationRequestPage.test.tsx`
  - `frontend/localize-stay-frontend/src/features/reservation-request/components/RequestReservationForm.tsx`
  - `frontend/localize-stay-frontend/src/features/reservation-request/components/ReservationResultSummary.tsx`
  - `frontend/localize-stay-frontend/src/features/reservation-request/types/reservationForm.ts`
  - `frontend/localize-stay-frontend/src/features/reservation-request/validation/reservationFormValidation.ts`
  - `frontend/localize-stay-frontend/src/features/reservation-request/validation/reservationFormValidation.test.ts`
  - `frontend/localize-stay-frontend/src/features/reservation-request/errors/reservationErrorMapping.ts`
  - `frontend/localize-stay-frontend/src/features/reservation-request/errors/reservationErrorMapping.test.ts`
  - `frontend/localize-stay-frontend/src/components/FormErrorSummary.tsx` (só se ainda não existir)
  - `frontend/localize-stay-frontend/src/components/OperationFeedback.tsx` (só se ainda não existir)
- **Modificar:**
  - `frontend/localize-stay-frontend/src/App.tsx` (ou router existente) — registrar `/reservations`
    pela API pública da feature
  - `frontend/localize-stay-frontend/src/test/mocks/handlers.ts` (se algum cenário adicional de UI
    precisar de handler não coberto pela 4.0)
- **Referência:**
  - `tasks/prd-solicitacao-reserva/frontend-techspec.md` — mapeamento completo de HTTP/`code` → UI
    (§Tratamento Centralizado de Erros), hierarquia de componentes e estados (§Arquitetura de
    Frontend)
  - `tasks/prd-solicitacao-reserva/api-contract.yaml` — schemas/exemplos de request/response
  - `frontend/localize-stay-frontend/src/features/reservation-request/api/reservationApi.ts` (4.0) —
    adapter tipado e resultado normalizado a consumir
- **Skills para consultar durante implementação:**
  - `react-architecture` — estrutura de feature, API pública, fronteira de imports
  - `react-testing` — RTL, `userEvent`, queries semânticas, AAA, MSW, foco/acessibilidade em teste

## Subtarefas

- [ ] 5.1 Implementar `reservationFormValidation.ts` (período, hóspedes, UUID, whitespace) com testes
      unitários cobrindo vazio/whitespace/UUID inválido/`checkOut<=checkIn`/`guestsCount<=0`
- [ ] 5.2 Implementar `reservationErrorMapping.ts` com testes cobrindo os 5 `code` de 422 (campo
      correto), 503 (tom "falha temporária"), 400/500/desconhecido (mensagem genérica)
- [ ] 5.3 Implementar `RequestReservationForm` + `ReservationResultSummary` (reaproveitando/criando
      `FormErrorSummary`/`OperationFeedback`) com a máquina de estados
      `idle→submitting→requested|rejected|unavailable|failed`
- [ ] 5.4 Implementar `ReservationRequestPage` orquestrando submit → resultado/erro, `AbortController`
      por submit e desabilitação de duplo envio; expor via `index.ts`
- [ ] 5.5 Registrar rota `/reservations` na navegação existente
- [ ] 5.6 Escrever `ReservationRequestPage.test.tsx` (RTL + MSW) cobrindo os 7 desfechos, preservação
      de valores, foco após erro/sucesso e prevenção de duplo submit; executar o gate

## Sequenciamento

- Bloqueado por: 4.0 (`reservationApi`, tipos gerados, handlers MSW)
- Desbloqueia: 6.0
- Paralelizável: Sim, com 1.0/2.0/3.0 (backend) — nenhum arquivo compartilhado; não paralelizável com
  4.0 (dependência direta)

## Rastreabilidade

- Esta tarefa cobre: V-FE-01 da `frontend-techspec.md`; RF-01 completo do ponto de vista da UI (os 7
  desfechos); Experiência do Usuário e Acessibilidade da TechSpec frontend.
- Evidência esperada: `ReservationRequestPage.test.tsx` verde para os 7 cenários;
  `reservationFormValidation.test.ts` e `reservationErrorMapping.test.ts` verdes; cobertura mínima de
  70% statements/branches/functions/lines na feature, com 100% dos 7 ramos contratuais exercitados.

## Detalhes de Implementação

Máquina de estados da página (`frontend-techspec.md` §Hierarquia de Componentes e Estados):

```
idle → submitting → requested (201)
                   → rejected (422, qualquer um dos 5 code)
                   → unavailable (503 CATALOG_INDISPONIVEL)
                   → failed (500 / rede / parse inválido)
```

Em `rejected`/`unavailable`/`failed`, a página retorna ao formulário editável com os valores
preservados (nenhum destes estados limpa `RequestReservationForm`). Em `requested`, o formulário é
substituído por `ReservationResultSummary`; "Nova solicitação" volta para `idle` com formulário vazio
— não há continuação para pagamento (F03, fora de escopo).

```text
ReservationRequestPage
├── RequestReservationForm
│   └── FormErrorSummary        (400/422 — role="alert", recebe foco)
├── OperationFeedback           (role="status" para loading/sucesso/503; role="alert" para 500/rede)
└── ReservationResultSummary    (exibido só após 201)
```

Tabela HTTP/`code` → UI (autoridade: `frontend-techspec.md` §Tratamento Centralizado de Erros — não
reinventar durante a implementação):

| HTTP / `code` | Comportamento |
|---|---|
| `400 / VALIDATION_ERROR` | mensagem genérica no `FormErrorSummary`; preserva valores; foca o resumo |
| `422 / PERIODO_INVALIDO` | erro em `checkIn`/`checkOut`; pede correção do período |
| `422 / QUANTIDADE_HOSPEDES_INVALIDA` | erro em `guestsCount`; pede valor positivo |
| `422 / ACOMODACAO_INDISPONIVEL` | erro em `accommodationId`; indica acomodação inexistente/inativa |
| `422 / CAPACIDADE_EXCEDIDA` | erro em `guestsCount` (+ referência a `accommodationId`) |
| `422 / PERIODO_INDISPONIVEL` | erro em `checkIn`/`checkOut`; indica indisponibilidade no período |
| `503 / CATALOG_INDISPONIVEL` | **não** é erro de formulário: `OperationFeedback` `role="status"`, tom de falha temporária, permite reenvio imediato |
| `500 / INTERNAL_ERROR` | mensagem genérica + `traceId` visível |
| resposta não contratual / rede | mensagem genérica de indisponibilidade; preserva formulário |

**Convenções da stack:**
- Feature em `features/reservation-request` com `index.ts` exportando só `ReservationRequestPage`
  (`react-architecture`); módulos compartilhados (`components`, `services`, `config`) não importam a
  feature.
- Testes AAA, `userEvent`, queries semânticas (`getByRole`, `getByLabelText`), MSW resetado por teste,
  sem mock direto de `fetch` (`react-testing`).
- Sem `localStorage`/cache — refresh limpa o contexto deliberadamente (mesma disciplina do draft de
  Catalog, já registrada na TechSpec como decisão fechada).

## Prontidão para Implementação

- **Decisões fechadas:** jornada única sem edição/listagem/consulta por ID; `guestReference` no corpo
  do request; 503 nunca tratado como erro de formulário; validação local só cobre
  período/hóspedes/shape; sem `trim()` silencioso em `guestReference`; sem `localStorage` para a
  Reservation criada; feature não fatiada por tipo de rejeição.
- **Limites de decisão do implementer:** textos exatos das mensagens de erro (em português,
  coerentes com o PRD/contrato); organização interna de `components/` vs. `pages/` além do que já
  está fixado na Estrutura de Pastas da TechSpec.
- **Dependências disponíveis:** `reservationApi`, tipos gerados e handlers MSW (4.0); estrutura
  intermediária do frontend (Fundação V-04, possivelmente já promovida por `prd-cadastro-property`).
- **Artefatos exigidos pelo gate:** `ReservationRequestPage.test.tsx`,
  `reservationFormValidation.test.ts`, `reservationErrorMapping.test.ts` são criados nesta própria
  task; MSW/handlers vêm de 4.0.
- **Dependências futuras:** Nenhuma — a jornada de UI fica completa e testável nesta task (o backend
  real só é exercitado em 6.0, via Playwright).
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `scripts/ai-flow/gate.sh --filter="ReservationRequestPage"`
- [ ] Testes focalizados passam: `reservationFormValidation.test.ts`, `reservationErrorMapping.test.ts`
- [ ] Os seletores encontram pelo menos um teste cada e não executam casos sem relação com esta task
- [ ] Build compila sem erros; type-check passa
- [ ] Os 7 desfechos do contrato (201, 400, 5×422, 503) são observáveis na UI com o texto/campo/tom
      corretos, cada um via cenário de teste próprio
- [ ] Período/hóspedes inválidos são rejeitados localmente sem disparar request (verificado por mock
      do adapter não chamado)
- [ ] Toda rejeição/falha preserva os valores digitados no formulário
- [ ] Loading desabilita o botão de submit e impede duplo envio
- [ ] Foco move corretamente: resumo de erro após 400/422 inválido; título do resumo após 201
- [ ] "Nova solicitação" após 201 limpa o formulário para uma nova jornada
- [ ] Checkpoint de feedback executado: `gate.sh --filter="ReservationRequestPage"` → verde
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova a jornada completa de UI, sem depender de backend real (isso é 6.0)
