---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: [3.0]
---

<task_context>
<domain>frontend/reservation-lookup/journey</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>http_server</dependencies>
<unblocks>"5.0"</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="ReservationLookupPage.test" --filter="reservationIdValidation.test"` prova, via RTL + MSW, os 5 cenários do AC de RF-01 (200×3 + 400 + 404) mais 500 no mesmo formulário de um campo, com validação local, loading, `ReservationDetailView` completo e foco acessível</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="ReservationLookupPage.test" --filter="reservationIdValidation.test"</gate_command>
<gate_test_selector>Arquivo `frontend/localize-stay-frontend/src/features/reservation-lookup/pages/ReservationLookupPage.test.tsx` (+ `reservationIdValidation.test.ts`, colocalizado na mesma feature)</gate_test_selector>
<gate_expected_result>Filtro encontra testes; todos passam; `/reservations/consultar` aceita um identificador válido e mostra `ReservationDetailView` com os campos corretos para os 3 pares estado/`sagaStatus`; 400 mostra erro de formato distinto de "não encontrada"; 404 mostra "não encontrada" em tom neutro; 500/rede mostram mensagem genérica com `traceId` quando presente; loading impede busca concorrente; build e type-check permanecem verdes</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Solicitante abre `/reservations/consultar`, informa um identificador e recebe, no mesmo incremento, os 5 cenários do AC de RF-01 (200 com os 3 pares estado/`sagaStatus`; 400 formato inválido; 404 não encontrada) mais 500/falha de rede, de forma acessível — feature "única e indivisível" conforme o PRD, não fatiada por tipo de resposta</vertical_slice>
</task_context>

# Tarefa 4.0: Consultar reserva pela interface acessível — 5 cenários + 500 (V-01 frontend)

## Relacionada às User Stories

- "Como Guest, eu quero consultar minha Reservation pelo identificador recebido ao solicitá-la, para
  saber se ela foi confirmada, cancelada, ou ainda aguarda o resultado do pagamento." (cobertura
  direta — UI completa)
- "Como autor/arquiteto em estudo, eu quero consultar a situação da saga (pendente, autorizado,
  rejeitado) e o identificador de correlação de uma Reservation..." (cobertura direta —
  `ReservationDetailView` exibe `sagaStatus`/`correlationId`)
- "Como frontend de teste, eu quero buscar uma Reservation por identificador e exibir seus dados e o
  estado da saga, para permitir verificação manual do fluxo completo sem acesso direto ao banco."
  (cobertura direta)
- "Como Guest, eu quero receber uma resposta clara quando o identificador informado não corresponde a
  nenhuma Reservation, para saber que devo conferir o identificador." (cobertura direta — tom neutro,
  distinto de erro)

## Visão Geral

Adiciona a segunda jornada de Booking ao frontend de teste: um solicitante informa o identificador de
uma Reservation recebido em F01 e vê, na mesma página, seus dados congelados, o estado atual
(`solicitada`/`confirmada`/`cancelada`), a situação da saga de pagamento
(`pendente`/`autorizado`/`rejeitado`) e, quando cancelada, o motivo. É uma consulta pontual somente
leitura — sem listagem, sem atualização automática. Mesma disciplina de F01: o PRD trata a feature
como única e indivisível (Plano de Rollout Faseado — "não há Fase 2/3... a conclusão de RF-01 dá
visibilidade completa"), então esta task entrega os 5 cenários do AC de RF-01 mais o caminho de erro
genérico (500/rede) num único incremento, não fatiado por tipo de resposta.

## Entrega Observável

- **Entrada ou gatilho:** Guest abre `/reservations/consultar`, digita um identificador e submete o
  formulário.
- **Resultado esperado:** para um id bem formado e existente, `ReservationDetailView` substitui o
  feedback com todos os campos (incluindo `cancellationReason` só quando `cancelada`) e o foco move
  para o título do resultado; para 400 (formato inválido, seja local ou do backend), erro associado ao
  campo distinto textualmente de "não encontrada"; para 404, mensagem neutra de "não encontrada"; para
  500/rede, mensagem genérica com `traceId` quando presente e permite nova tentativa.
- **Checkpoint de feedback:** `scripts/ai-flow/gate.sh --filter="ReservationLookupPage.test"
  --filter="reservationIdValidation.test"` verde.
- **Seletor focalizado:** `ReservationLookupPage.test.tsx`, `reservationIdValidation.test.ts`
- **Fora deste checkpoint:** nenhuma chamada contra o backend real (isso é a task 5.0, E2E); estados
  `confirmada`/`cancelada` só são exercitados aqui via RTL+MSW, não por uma jornada real de usuário
  ainda (dependem da mesma técnica de seed que o backend documenta como dívida técnica).

## Requisitos

- **Estrutura de pastas** (`react-architecture`, mesma convenção de `reservation-request`):
  `src/features/reservation-lookup/{api,components,pages,validation}` + `index.ts` exportando somente
  `ReservationLookupPage`.
- **`reservationIdValidation.ts`:** função pura — `reservationId` obrigatório, formato UUID
  (`Guid`); antecipa o 400 do backend sem se tornar autoridade (o caminho 400 do backend continua
  testado, para cobrir uma resposta malformada que escape da validação local).
- **`ReservationLookupPage`:** máquina de estados `idle → searching → found (200) | notFound (404) |
  invalid (400) | failed (500/rede)`; cada nova busca substitui o resultado anterior (sem preservar
  um resultado antigo); `AbortController` cancela a busca anterior ao iniciar uma nova e ao
  desmontar; botão de busca desabilitado durante `searching`.
- **`ReservationDetailView`:** exibe dados congelados (período, hóspedes, preço, moeda, total),
  estado, `sagaStatus`, `correlationId` (sempre visível) e `cancellationReason` (só quando presente,
  sem `dt`/`dd` vazio para os demais estados); ao exibir o resultado, move foco para o próprio título.
- **Tratamento de erro (por `(status, code)`, mesma tabela da TechSpec frontend):**
  - `400 VALIDATION_ERROR` → `FormErrorSummary` associado ao campo do identificador.
  - `404 RESERVATION_NOT_FOUND` → `OperationFeedback` tom `neutral` (`role="status"`, nunca
    `role="alert"`) — é uma resposta esperada e válida do fluxo, não um erro técnico.
  - `500 INTERNAL_ERROR` / resposta não contratual / falha de rede → `OperationFeedback` tom `error`
    (`role="alert"`) + `traceId` visível quando presente; permite nova tentativa.
- **Roteamento:** `/reservations/consultar` (rota irmã de `/reservations`, não `/reservations/:id`)
  registrada em `App.tsx` com uma nova entrada de navegação ("Consultar reserva").
- **Acessibilidade (WCAG 2.1 AA, mesma disciplina de F01):** campo com `label`; erro de formato usa
  `aria-invalid`/`aria-describedby`; `OperationFeedback` com `role="status"` para loading e
  "não encontrada", `role="alert"` só para erro real.

## Arquivos Envolvidos

- **Criar:**
  - `frontend/localize-stay-frontend/src/features/reservation-lookup/index.ts`
  - `frontend/localize-stay-frontend/src/features/reservation-lookup/pages/ReservationLookupPage.tsx`
  - `frontend/localize-stay-frontend/src/features/reservation-lookup/pages/ReservationLookupPage.test.tsx`
  - `frontend/localize-stay-frontend/src/features/reservation-lookup/components/ReservationDetailView.tsx`
  - `frontend/localize-stay-frontend/src/features/reservation-lookup/validation/reservationIdValidation.ts`
  - `frontend/localize-stay-frontend/src/features/reservation-lookup/validation/reservationIdValidation.test.ts`
- **Modificar:**
  - `frontend/localize-stay-frontend/src/App.tsx` (registra rota/nav `/reservations/consultar` pela
    API pública da feature)
- **Referência:**
  - `frontend/localize-stay-frontend/src/features/reservation-lookup/api/reservationDetailApi.ts`
    (task 3.0) — único ponto de contato HTTP, não reimplementar parsing aqui
  - `frontend/localize-stay-frontend/src/components/{FormErrorSummary,OperationFeedback}.tsx` —
    reaproveitados sem alteração
  - `frontend/localize-stay-frontend/src/features/reservation-request/pages/ReservationRequestPage.tsx`
    — padrão de máquina de estados/foco acessível a seguir
  - `frontend-techspec.md` (§Hierarquia de Componentes e Estados, §Tratamento Centralizado de Erros,
    §Acessibilidade) — comportamento normativo
- **Skills para consultar durante implementação:**
  - `react-architecture` — feature com `index.ts` público, aliases, sem imports relativos profundos
  - `react-testing` — Vitest/RTL, AAA, `userEvent`, queries semânticas, MSW

## Subtarefas

- [ ] 4.1 Implementar `reservationIdValidation.ts` (obrigatório + formato UUID) e
      `reservationIdValidation.test.ts` (vazio, whitespace, UUID inválido, UUID válido)
- [ ] 4.2 Implementar a máquina de estados de `ReservationLookupPage` (idle→searching→found/notFound/
      invalid/failed), `AbortController` por busca e botão desabilitado durante `searching`
- [ ] 4.3 Implementar `ReservationDetailView` (todos os campos, `cancellationReason` condicional, foco
      no título ao exibir resultado)
- [ ] 4.4 Ligar o tratamento de erro por `(status, code)` via `FormErrorSummary`/`OperationFeedback`
      (400 campo, 404 neutro, 500/rede erro real com `traceId`)
- [ ] 4.5 Registrar rota `/reservations/consultar` e entrada de navegação em `App.tsx`
- [ ] 4.6 Escrever `ReservationLookupPage.test.tsx`: render inicial; vazio/malformado não dispara
      request; os 3 pares 200 estado/`sagaStatus` (com/sem `cancellationReason`); 400 distinto de 404;
      404 tom neutro; 500/rede com `traceId` e nova tentativa; segunda busca substitui resultado
      anterior; loading bloqueia busca concorrente

## Sequenciamento

- Bloqueado por: 3.0 (consome `reservationDetailApi`, tipos gerados e handlers MSW)
- Desbloqueia: 5.0 (E2E consome esta UI)
- Paralelizável: Não com 3.0 (dependência direta); pode avançar em paralelo às tasks 1.0/2.0 do
  backend (nenhum arquivo compartilhado)

## Rastreabilidade

- Esta tarefa cobre: as 4 User Stories do PRD (Guest, autor/arquiteto, frontend de teste), RF-01
  (todas as ACs, lado UI), Experiência do Usuário, acessibilidade WCAG 2.1 AA da fatia entregue.
- Evidência esperada: `ReservationLookupPage.test.tsx` verde cobrindo os 5 cenários do AC de RF-01 +
  500; `reservationIdValidation.test.ts` verde.

## Detalhes de Implementação

Máquina de estados (`frontend-techspec.md` §Hierarquia de Componentes e Estados):

```text
ReservationLookupPage
├── ReservationLookupForm             (campo único: reservationId)
│   └── FormErrorSummary              (reaproveitado — formato inválido)
├── OperationFeedback                 (loading / notFound / erro genérico)
└── ReservationDetailView             (exibido só após 200)
```

`idle → searching → found (200) | notFound (404) | invalid (400) | failed (500/rede)`. Diferente da
jornada de F01, aqui **não há preservação de "resultado anterior"** durante uma nova busca: trocar o
identificador e buscar de novo substitui o resultado exibido (ou limpa, se a nova busca falhar).

Tabela de tratamento de erro (`frontend-techspec.md` §Tratamento Centralizado de Erros):

| HTTP / `code` | Comportamento na UI |
|---|---|
| `200` | `ReservationDetailView` substitui o feedback; foco move para o título do resultado |
| `400 / VALIDATION_ERROR` | `FormErrorSummary` associado ao campo do identificador; distinto textualmente de "não encontrada" |
| `404 / RESERVATION_NOT_FOUND` | `OperationFeedback` tom `neutral` ("Nenhuma reserva encontrada com esse identificador. Confira o identificador recebido.") — não é erro de formulário nem falha técnica |
| `500 / INTERNAL_ERROR` | `OperationFeedback` tom `error` + `traceId` visível para diagnóstico |
| resposta não contratual, parse inválido ou falha de rede | `OperationFeedback` tom `error`, mensagem genérica, permite nova tentativa |

`OperationFeedback` já existente (`src/components/OperationFeedback.tsx`) mapeia
`tone === 'error' ? 'alert' : 'status'` — passar `tone="neutral"` para 404 garante `role="status"`
sem nenhuma alteração no componente compartilhado.

`reservationDetailApi` (task 3.0) já devolve um resultado tipado
(`found`/`notFound`/`malformed`/`failed`) — esta página só faz o `switch` sobre esse resultado, sem
reimplementar parsing de `ProblemDetails`.

**Convenções da stack:**
- Mesmo padrão de `ReservationRequestPage`: `useRef` para foco programático (título do resultado /
  `FormErrorSummary`), `abortRef` para cancelar busca anterior.
- Testes seguem AAA, `userEvent` e queries semânticas (`getByLabel`, `getByRole`); MSW reseta handlers
  após cada teste (`resetHandlers()` já configurado em `src/test/setup.ts`).

## Prontidão para Implementação

- **Decisões fechadas:** rota `/reservations/consultar` (não `/reservations/:id` — decisão já
  confirmada na TechSpec frontend); sem preservação de resultado anterior; 404 sempre `role="status"`,
  nunca `role="alert"`; sem cache/revalidação/polling.
- **Limites de decisão do implementer:** textos exatos de mensagem (dentro do tom definido pela
  tabela acima); organização interna de `ReservationLookupForm` (componente próprio ou inline em
  `ReservationLookupPage`, como já variou entre features anteriores).
- **Dependências disponíveis:** `reservationDetailApi`, tipos gerados, handlers MSW (task 3.0, já
  prontos); `FormErrorSummary`, `OperationFeedback` (F01, já em `main`).
- **Artefatos exigidos pelo gate:** `ReservationLookupPage.test.tsx` e
  `reservationIdValidation.test.ts` são criados nesta própria task.
- **Dependências futuras:** Nenhuma — a task 5.0 (E2E) consome a UI já pronta e testada por esta
  task, sem precisar reabri-la.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `scripts/ai-flow/gate.sh --filter="ReservationLookupPage.test"`
- [ ] Teste focalizado passa: `scripts/ai-flow/gate.sh --filter="reservationIdValidation.test"`
- [ ] Os dois seletores encontram pelo menos um teste cada e não executam casos sem relação com esta
      task
- [ ] Build compila sem erros: `npm run build`
- [ ] `/reservations/consultar` aceita um identificador vazio/malformado sem disparar request e move
      foco ao erro local
- [ ] Os 3 pares 200 estado/`sagaStatus` exibem todos os campos corretos, com `cancellationReason`
      presente só em `cancelada`/`rejeitado`
- [ ] 400 (backend) e 404 mostram mensagens distintas, com tons corretos (`alert`/`status`)
- [ ] 500/falha de rede mostram mensagem genérica com `traceId` quando presente e permitem nova busca
- [ ] Uma segunda busca substitui o resultado anterior exibido; loading impede busca concorrente
      (botão desabilitado)
- [ ] Lint/type-check passam sem violações
- [ ] Checkpoint de feedback executado conforme descrito acima
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente a UI via MSW, não a jornada full-stack real (task 5.0)
