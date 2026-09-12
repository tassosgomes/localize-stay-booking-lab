# Task 4.0 — revisão focused (primeira)

- **Modo:** focused (não é revalidação)
- **Escopo:** V-01 create UI — `/properties`, formulário, validação, 201/400/500/rede, resumo e MSW. Edição, Playwright/E2E e backend real fora de escopo.
- **Diff base:** `cb37c5263d36f79fb84919bb77de7b65eace3f06` (checkpoint task 3.0; working tree + untracked da fatia)
- **HEAD durante a revisão:** `cb37c5263d36f79fb84919bb77de7b65eace3f06` (`feature/prd-cadastro-property`) — inalterado
- **Independência:** worker fresco desta runtime; não é segundo revisor humano. Gate reexecutado; aprovação do implementer não foi reutilizada.

## Gate

```bash
scripts/ai-flow/gate.sh --filter="PropertyCreate.test"
```

- **Resultado:** exit 0 — `GATE: APROVADO`
- **Testes:** `PropertyCreate.test=9`, 0 falhas; seletor não executou edição/E2E (`PropertyUpdate` / Playwright ausentes)
- **Build/format:** `tsc` ok (`frontend/localize-stay-frontend`); lint pulado (sem eslint/prettier local)

## O que foi revisado

Contrato da task, diff desde o checkpoint (`App.tsx`, `propertyApi.ts` abort, `apiClient.ts`) e untracked da fatia (`index.ts`, `PropertyWorkspacePage.tsx`, `CreatePropertyForm.tsx`, `CreatedPropertySummary.tsx`, `FormErrorSummary.tsx`, `OperationFeedback.tsx`, `propertyForm.ts`, `propertyFormValidation.ts` + unitário, `PropertyCreate.test.tsx`, `App.css`). Skills `react-architecture`, `react-code-quality` e `react-testing`. Trechos de frontend-techspec (rota, fetch/erros, validação, WCAG da jornada) e RF-01 só para cruzar critérios desta fatia.

Comportamento coberto: `/` permanece Service Status; `/properties` via `index.ts` sem React Router; UUID/nome 1..120/localização 1..500 sem `trim`; whitespace rejeitado; 201 mostra confirmação, ID copiável, Host, dados e `active`; duplicata aceita após confirmação explícita; inválido local não chama API e foca o resumo; 400 associa campos e preserva valores; 500/rede mostram mensagem/`traceId` e retry manual; submit desabilitado + `creatingRef` no loading; abort na desmontagem sem retry de 500; labels, `aria-invalid`/`aria-describedby`, `status`/`alert` e `:focus-visible`; nenhum `EditPropertyForm`/Playwright.

## Bloqueantes

Nenhum.

## Recomendações (3)

1. **Retry de `fetch` sem `signal`** (`apiClient.ts` L37–47): se `fetch` lança `TypeError` de AbortSignal incompatível, o cliente dispara um segundo `fetch` sem abort. Isso enfraquece o cancelamento na desmontagem e é um retry automático de POST no cliente compartilhado. Corrigir a instância do signal (ou restringir o fallback a teste) em vez de reenviar a mutation.
2. **400 com `details` não mapeáveis fica mudo** (`PropertyWorkspacePage.tsx` L117–123): `status === 400` com `details.length > 0` só preenche erros de campo; se nenhum alias bater, não há resumo nem `generalProblem`/`traceId`. O contrato atual cobre `name`/`location`/`X-Host-Reference-Id`; um fallback para alerta geral evita silêncio se o payload divergir.
3. **`alertdialog` sem trap de foco** (`PropertyWorkspacePage.tsx` L192–215): a confirmação de nova criação declara `aria-modal="true"`, mas Tab ainda alcança o resumo e a navegação, e Escape não fecha. A checklist da task (label, foco no resumo, ARIA de campo, status/alert, foco visível) está atendida; fechar o diálogo na 5.0 evita regressão de teclado.

## Veredito

VALIDAÇÃO APROVADA (3 recomendações)
