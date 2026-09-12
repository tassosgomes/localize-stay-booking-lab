# Review — Task 5.0: Solicitar reserva pela interface acessível (V-FE-01)

- **Modo:** focused (primeira revisão; complexidade high — fatia indivisível por design)
- **Base (checkpoint 4.0):** `1b612a3de70fdffa0a06f10515e616acbb8197ff` (HEAD, estável durante toda a revisão)
- **Resultado:** `VALIDATION APPROVED` — 0 bloqueantes, 3 recomendações não bloqueantes

## Gate (executado pelo validator, antes da leitura semântica)

```
scripts/ai-flow/gate.sh --filter="ReservationRequestPage.test" --filter="reservationFormValidation.test" --filter="reservationErrorMapping.test"
→ GATE: APROVADO
   build: tsc ok (frontend/localize-stay-frontend)
   testes: ok (ReservationRequestPage.test=17 reservationFormValidation.test=20 reservationErrorMapping.test=10)
```

O `gate_command` do `5_task.md` (corrigido pelo orquestrador para incluir `.test` nos filtros) é o
comando efetivamente executado; saída APROVADO, exit 0.

## Evidências adicionais executadas pelo validator

- Suíte completa com cobertura (`npm run test:coverage`): **63/63 testes verdes em 5 arquivos**
  (inclui `reservationApi.test.ts` da 4.0, que exercita `signal` através do `apiClient` modificado).
- Cobertura da feature (piso configurado 70% em statements/branches/functions/lines,
  `vite.config.ts`): **97,35% stmts / 94,21% branches / 95,23% funcs / 97,35% lines** — thresholds
  aprovados pelo próprio vitest.
- `git status`/HEAD idênticos no início e no fim da revisão (nenhuma mudança durante o processo).
- Busca por `playwright` em `src/`: nenhum resultado — nenhum artefato da task 6.0 no diff.

## Escopo revisado

- **Criados (untracked):** `src/features/reservation-request/{index.ts, pages/ReservationRequestPage.tsx
  (+test 17), components/{RequestReservationForm,ReservationResultSummary}.tsx, types/reservationForm.ts,
  validation/reservationFormValidation.ts (+test 20), errors/reservationErrorMapping.ts (+test 10)}`;
  `src/components/{FormErrorSummary,OperationFeedback}.tsx` (não existiam; criação justificada).
  `api/reservationApi.ts` é artefato da 4.0 (commitado), apenas consumido.
- **Modificados:** `App.tsx` (rota `/reservations`), `apiClient.ts` (probe cross-realm AbortSignal),
  `package.json`/`package-lock.json` (+`@testing-library/user-event`, +`@vitest/coverage-v8`,
  +script `test:coverage`), `vite.config.ts` (coverage 70% na feature), `.gitignore` (`coverage/`),
  `5_task.md`/`flow-state.json` (estado/gate, orquestrador).

## Verificação dos pontos de atenção

### (a) 7 desfechos com tom/campo corretos por `code` — OK

Mapeamento em `reservationErrorMapping.ts` confere 1:1 com a tabela autoritativa da techspec
(§Tratamento Centralizado de Erros, linhas 207-217) e com o contrato (`api-contract.yaml` codes):
201 → resumo congelado (ID, `solicitada`, diária/total/moeda, nota de pagamento, botão "Nova
solicitação" resetando para vazio); 400 → resumo genérico sem campo; 5×422 → campo(s) exatos
(`PERIODO_INVALIDO`→checkIn/checkOut, `QUANTIDADE_HOSPEDES_INVALIDA`→guestsCount,
`ACOMODACAO_INDISPONIVEL`→accommodationId, `CAPACIDADE_EXCEDIDA`→guestsCount com referência textual
à acomodação, `PERIODO_INDISPONIVEL`→checkIn/checkOut); 503 → `OperationFeedback` `role="status"`
com tom de falha temporária, nunca erro de formulário, com teste de reenvio imediato →201;
500/rede → `role="alert"` genérico + `traceId` quando presente. Decisão exclusivamente por `code`,
nunca por `title`/`detail`. Cada desfecho tem cenário próprio no teste da página (17 testes).

### (b) Acessibilidade WCAG 2.1 AA da fatia — OK

Todo input com `<label htmlFor>`; erros com `aria-invalid` + `aria-describedby` apontando para o
`<p id="{field}-error">` com label em texto (nunca só cor); `FormErrorSummary` com `role="alert"` +
`tabIndex={-1}` recebendo foco após validação local/400/422 (assertado com `toHaveFocus` em todos os
cenários); após 201 o foco vai ao título do resumo (`tabIndex={-1}`, sem retirar controle do
teclado); `OperationFeedback` usa `role="status"` para loading/503 e `role="alert"` para 500/rede;
formulário com `noValidate` (validação custom anunciável substitui a nativa); leitura/edição dos
campos permanece habilitada durante loading (assertado).

### (c) Validação local antecipa RN-02/RN-03 sem chamar o adapter — OK

`validateReservationForm` é pura (sem I/O): UUID, guestReference ≥1 não-branco ≤255 sem `trim()`
silencioso, período com `checkOut > checkIn`, guestsCount inteiro ≥1. Não simula
capacidade/disponibilidade/existência (conforme techspec §Validação). Testes espiam
`reservationApi.request` e assertam `not.toHaveBeenCalled()` nos 3 cenários locais (período,
hóspedes, formulário vazio).

### (d) Duplo submit e abort na desmontagem — OK

Botão desabilitado durante `submitting`; teste com `delay(150)` + segundo clique asserta `calls === 1`.
`AbortController` por submit (novo submit e "Nova solicitação" abortam o anterior);
desmontagem aborta via cleanup do `useEffect`; teste captura o signal e asserta `aborted === true`.

### (e) Fronteira de imports da feature — OK

`App.tsx` importa exclusivamente `features/reservation-request/index.ts`, que exporta só
`ReservationRequestPage`. Componentes compartilhados (`FormErrorSummary`, `OperationFeedback`) não
importam a feature; a feature importa shared (`components`, `services`, `config`) na direção
permitida. Rota registrada sem `/reservations/:id` (F02 preservada).

### (f) `apiClient.ts` cross-realm — OK

Probe `new Request(url, { signal })` (sem rede) detecta incompatibilidade uma única vez: no browser
(mesma realm) o signal sempre flui — cancelamento real preservado; em jsdom cross-realm o signal é
omitido e o cancelamento permanece lógico (caller ignora resultado pós-abort). Chamadores sem
`signal` não mudam de comportamento (`undefined → undefined`). Suíte completa 63/63 verde,
incluindo os testes da 4.0 que passam `AbortController().signal` pelo caminho modificado.

### (g) Cobertura ≥70% — OK

Piso 70% configurado em `vite.config.ts` (thresholds vitest, falham o comando se violados);
medido: 97,35%/94,21%/95,23%/97,35%. Os 7 ramos contratuais são exigidos por cenário de teste,
não por limiar (comentário no config registra a decisão).

### (h) Sem escopo da 6.0 — OK

Nenhum arquivo E2E/Playwright no diff (busca por `playwright` em `src/` sem resultados); nenhuma
dependência de E2E adicionada; backend real não é tocado (MSW only).

## Bloqueantes

Nenhum.

## Recomendações (não bloqueantes)

1. **Ramos defensivos do ciclo de vida sem cobertura na página**
   (`ReservationRequestPage.tsx:46-48,121-123`): o `isAbortError` no nível da página e o `catch`
   pós-adapter só seriam atingidos com rejeição real de fetch por abort (jsdom dropa o signal no
   `apiClient`, então o AbortError nunca chega à página nos testes). A proteção correta existe e o
   comportamento é irrelevante para o usuário (React 18 ignora setState pós-unmount), mas se a 6.0
   (Playwright/browser real) cobrir cancelamento, estes ramos fecharão naturalmente.
2. **Barrel `index.ts` com 0% statements**: re-export puro entra no relatório de cobertura da
   feature sem risco (agregado 97,35%). Se desejado, excluir barrels (`**/index.ts`) do
   `coverage.include`/`exclude` em tarefa futura de configuração — cosmético.
3. **`CAPACIDADE_EXCEDIDA` referencia `accommodationId` apenas no texto da mensagem**
   ("capacidade máxima da acomodação informada"), sem marcar o campo com `aria-invalid`.
   Interpretação aderente à techspec ("erro associado a `guestsCount` (e referência a
   `accommodationId`); mensagem indica capacidade máxima excedida") e assertada nos testes puros e
   de página; registrar apenas para alinhamento futuro caso a techspec evolua para marcar ambos os
   campos.

## Conclusão

`VALIDATION APPROVED` — os 7 desfechos contratuais, a acessibilidade da fatia, a validação local, o
duplo submit/abort, a fronteira de imports, a mudança do `apiClient` (sem regressão na 4.0), o piso
de cobertura e a ausência de escopo 6.0 estão todos verificados com evidência executada pelo
validator. Aprovação com 3 recomendações não bloqueantes.
