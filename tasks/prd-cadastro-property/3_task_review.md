# Task 3.0 — revisão focused (primeira)

- **Modo:** focused (não é revalidação)
- **Escopo:** habilitador EN-01 — tipos Catalog, `propertyApi`, env/`apiClient` e MSW. Formulários, navegação e backend real fora de escopo.
- **Diff base:** `c4b4061cee73f860f5b625a28fb1c4a5973a28bd` (checkpoint task 2.0; working tree + untracked da fatia)
- **HEAD durante a revisão:** `c4b4061cee73f860f5b625a28fb1c4a5973a28bd` (`feature/prd-cadastro-property`) — inalterado
- **Independência:** worker fresco desta runtime; não é segundo revisor humano. Gate reexecutado; aprovação do implementer não foi reutilizada.

## Gate

```bash
scripts/ai-flow/gate.sh --filter="propertyApi.test"
```

- **Resultado:** exit 0 — `GATE: APROVADO`
- **Testes:** `propertyApi.test=12`, 0 falhas; seletor não executou componentes de UI
- **Build/format:** `tsc` ok (`frontend/localize-stay-frontend`); lint pulado (sem eslint/prettier local)

Evidência extra (não substitui o gate): `npm run api:check` exit 0; `vite build` ok; `ServiceStatus.test.tsx` 2/2 após o MSW compartilhado.

## O que foi revisado

Contrato da task, diff desde o checkpoint, untracked da fatia (`propertyApi.ts`/`propertyApi.test.ts`, `catalog.ts` gerado, `apiClient.ts`, MSW `handlers`/`server`/`setup`, `check-catalog-drift.mjs`, `.env.example`) e skills `react-architecture`, `react-testing`, `test-guide`. Trechos de `api-contract.yaml` / frontend-techspec / ADR-003 só para conferir OpenAPI 1.0.0, `/v1`, `fetch` e EN-01.

Comportamento coberto: POST `/v1/properties` com JSON e `X-Host-Reference-Id`; PATCH omite `undefined`; `/v1` não duplica; 201/200 tipados; 400 com `details`/`traceId`; 403/404/500; fallback de rede e JSON inválido; abort sem retry; drift `openapi-typescript` 7.13.0 a partir de `api-contract.yaml`; lock só adiciona pacotes (Playwright/`user-event`/`openapi-typescript`), sem upgrade amplo; `fetch` via `apiClient` sem query library/store/retry; estrutura intermediária só em `features/property-registration`; aliases Vite/TS coincidem; `tsconfig.app.json`/`tsconfig.node.json` inexistentes no app da Fundação; nenhum formulário/página da 4.0/5.0.

## Bloqueantes

Nenhum.

## Recomendações (3)

1. **Asserção PATCH de rota/header** (`propertyApi.test.ts` ~L74–101): o caso de update prova o body parcial; método, `/v1/properties/{id}` e `X-Host-Reference-Id` ficam implícitos no handler MSW e no `send()` compartilhado com o POST. Incluir as mesmas asserções do create evita regressão se o update deixar de passar pelo helper comum.
2. **Aliases configurados e não usados** (`propertyApi.ts` L1–3; testes ~L6–8): Vite e `tsconfig.json` definem `@/` e `@features/`, mas o adapter importa com `../../../`. Trocar nos consumidores da feature quando a API pública da 4.0 nascer.
3. **Dois setup files** (`vitest.setup.ts` L1 vs `vite.config.ts` `setupFiles`): o config aponta só para `src/test/setup.ts`; `vitest.setup.ts` reexporta o mesmo módulo e permanece no `include` do tsconfig. Remover o shim na próxima limpeza de config para não divergir o bootstrap do Vitest.

## Veredito

VALIDAÇÃO APROVADA (3 recomendações)
