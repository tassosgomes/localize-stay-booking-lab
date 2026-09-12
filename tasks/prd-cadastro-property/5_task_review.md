# Task 5.0 — revisão focused (primeira)

- **Modo:** focused (não é revalidação)
- **Escopo:** V-02/V-03 edit UI + E2E — prefill, PATCH parcial, 200/400/403/404/500/rede, imutabilidade ID/Host/status, Playwright create→edit e ownership. Create UI da 4.0 só como regressão no diff compartilhado.
- **Diff base:** `8785df484fcd2c42d0948d08abbc006f2ff40df8` (checkpoint task 4.0; working tree + untracked da fatia)
- **HEAD durante a revisão:** `8785df484fcd2c42d0948d08abbc006f2ff40df8` (`feature/prd-cadastro-property`) — inalterado
- **Independência:** worker fresco desta runtime; não é segundo revisor humano. Gate reexecutado; aprovação do implementer não foi reutilizada.

## Gate

```bash
scripts/ai-flow/gate.sh --filter="PropertyUpdate"
```

- **Resultado:** exit 0 — `GATE: APROVADO` (~168s)
- **Testes:** `PropertyUpdate=14 rtl=12 e2e=2`; seletor pegou `PropertyUpdate.test.tsx` e `e2e/PropertyUpdate.spec.ts`, sem casos alheios
- **Build/format:** `tsc` ok (`frontend/localize-stay-frontend`); lint pulado (sem eslint/prettier local)
- **Portas:** 5111/5175 livres antes e depois; Playwright sobe Catalog via `e2e/start-stack.mjs` e derrubou Kestrel/Vite. Container `localize-stay-e2e-pg` ficou de pé e foi removido ao final desta revisão.
- **Evidência extra (não está no `gate.sh`):** `npm run api:check` exit 0 (sem drift em `catalog.ts`); `npm run test:coverage` 41/41, statements/lines 82.89%, branches 90.74%, functions 83.54% (limiar 70%). `PropertyCreate` (9) passou no mesmo run.

## O que foi revisado

Contrato da task, diff desde o checkpoint, untracked da fatia (`EditPropertyForm.tsx`, `PropertyUpdate.test.tsx`, `e2e/PropertyUpdate.spec.ts`, `e2e/start-stack.mjs`, `playwright.config.ts`) e skills `react-architecture`, `react-code-quality`, `react-testing`, `test-guide`. Trechos de frontend-techspec (V-02/V-03, PATCH parcial, erros, E2E, sem GET) e RF-02 só para cruzar critérios desta fatia. `propertyApi.ts` não mudou (já na 3.0).

Comportamento coberto: `created → editing → updating → created`; prefill a partir da resposta 201; ID/Host/status só no resumo (Host do form é o header fictício, com texto de que não transfere); sem mudança não envia PATCH; body só com `location` ou `name`+`location`; 200 atualiza resumo e preserva identidade; inválido local, 400, 403, 404, 500 e rede distinguíveis, com `traceId` quando o problema traz, draft preservado e retry manual; submit desabilitado no `updating`; abort na desmontagem sem retry automático; E2E real create→edit só de localização e 403 com Host B; `index.ts` público inalterado; sem GET de Property, storage, transferência, desativação ou retry automático.

## Bloqueantes

Nenhum.

## Recomendações (3)

1. **Abort do PATCH não é observado** (`PropertyUpdate.test.tsx` ~L412–432): o handler MSW nunca resolve; o caso só espera que `attempts` continue 1 após `unmount`. Isso não distingue cancelamento do `AbortController` de um request ainda pendente. Espelhar o teste do adapter (`propertyApi.test.ts`) e asserir `AbortError`/signal, ou falhar se o fetch completar depois do unmount.
2. **Postgres E2E sobrevive ao Playwright** (`e2e/start-stack.mjs` ~L72–78, L95–122): `shutdown` mata Catalog/Vite, mas não para `localize-stay-e2e-pg` (porta 54332). Encerrar o container no `SIGTERM` evita lixo entre gates (o validator removeu o que esta execução deixou).
3. **`alertdialog` sem trap de foco** (`PropertyWorkspacePage.tsx` ~L366–389): a confirmação de nova criação, ainda visível na edição, declara `aria-modal="true"`, mas Tab alcança resumo/form/navegação e Escape não fecha. Já era recomendação da 4.0; a 5.0 não fechou.

## Veredito

VALIDAÇÃO APROVADA (3 recomendações)
