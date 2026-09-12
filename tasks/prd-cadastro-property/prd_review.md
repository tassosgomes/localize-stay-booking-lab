# Full validation — prd-cadastro-property

- **Modo:** full
- **PRD dir:** `tasks/prd-cadastro-property`
- **Specs selecionadas (não revisadas em profundidade):** `prd.md`, `techspec.md`, `frontend-techspec.md`, `api-contract.yaml`
- **Branch:** `feature/prd-cadastro-property` (já rebaseada; nenhum rebase nesta revisão)
- **base_ref:** `08daf5e32cca43d0713600d0c495afeaebf211de`
- **validated_commit (HEAD revisado):** `6c7088b81d78aec4b66aee3208b592138d8c8df3`
- **validated_tree:** `1495bba787883c29211aee27b5caa348820e30b3`
- **Estabilidade:** HEAD e árvore Git idênticos antes e depois. Código de produto inalterado. Única sujidade pré-existente: `tasks/prd-cadastro-property/flow-state.json` (estado operacional; não tocado).
- **Independência:** worker fresco; aprovação focused das tasks não foi reutilizada.

## Gate

```bash
scripts/ai-flow/gate.sh --base=08daf5e32cca43d0713600d0c495afeaebf211de --all-tests
```

- **Resultado:** exit 1 — `GATE: REPROVADO` (~190s)
- **Etapa:** testes
- **Comando:** `vitest run (frontend/localize-stay-frontend)`
- **Portas 5111/5175:** livres antes e depois. Nenhum processo/container E2E desta execução ficou de pé (`docker ps` vazio no worktree).
- **Revisão semântica do diff/specs:** encerrada após a falha do gate, conforme o contrato do validator.

### Falha (últimas linhas relevantes)

`FAIL frontend/localize-stay-frontend/e2e/PropertyUpdate.spec.ts`

```
Error: Playwright Test did not expect test.describe() to be called here.
 ❯ frontend/localize-stay-frontend/e2e/PropertyUpdate.spec.ts:22:6
     22| test.describe('PropertyUpdate', () => {
 Test Files  1 failed | 5 passed (6)
      Tests  41 passed (41)
```

Os 41 casos Vitest/RTL passaram; a suíte agregada caiu na coleta do spec Playwright.

## Bloqueantes

1. **Task 5.0** — `frontend/localize-stay-frontend/e2e/PropertyUpdate.spec.ts:22` e invocação `--all-tests` em `scripts/ai-flow/gate.sh` (~L298–305).
   A 5.0 introduziu o único `*.spec.ts` do frontend (`test.describe` Playwright). O filtro focused usa `env --chdir="$fdir" ./node_modules/.bin/vitest` e respeita `vite.config.ts` (`test.include` só `src/**/*.test.ts(x)`, `exclude` `**/e2e/**`, L24–25). `--all-tests` chama `npm --prefix "$fdir" exec vitest run` a partir da raiz do repo, sem carregar essa config; o include padrão do Vitest (`**/*.{test,spec}.*`) coleta o spec E2E e a suíte completa falha. Sem gate agregado verde não há aprovação full.

## Recomendações

Nenhuma além do bloqueante. Não converter o alinhamento `chdir`/config do Vitest em sugestão: é condição para o `--all-tests` passar.

## Veredito

FULL VALIDATION REPROVADA (task 5.0; 1 bloqueante)
