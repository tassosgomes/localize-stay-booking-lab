# Full validation — prd-cadastro-property

- **Run:** `full-2` / retry após flake Docker / validator fresco / `2026-09-12T20:18:28Z`
- **Tentativa:** full 2/3 (substitui o `prd_review.md` do full-2 anterior; aquele veredito Ryuk/ResourceReaper **não** é evidência desta execução)
- **Modo:** full
- **PRD dir:** `tasks/prd-cadastro-property`
- **Specs selecionadas:** `prd.md`, `techspec.md`, `frontend-techspec.md`, `api-contract.yaml`
- **Branch:** `feature/prd-cadastro-property` (já preparada; nenhum rebase nesta revisão)
- **base_ref:** `08daf5e32cca43d0713600d0c495afeaebf211de`
- **validated_commit (HEAD revisado):** `0c993f505d505f47b7c16341d6eed4cf805f378d`
- **validated_tree:** `15c0149a42b0799528e40005748f140d4598f195`
- **Estabilidade:** HEAD e árvore Git idênticos antes e depois. Código de produto inalterado. Sujidade pré-existente: `tasks/prd-cadastro-property/flow-state.json` (estado operacional; não tocado). Este arquivo é a única alteração desta revisão.
- **Independência:** worker fresco; aprovações focused/revalidation e o full-2 com Ryuk não foram reutilizados como revisão semântica.

## Gate

```bash
scripts/ai-flow/gate.sh --base=08daf5e32cca43d0713600d0c495afeaebf211de --all-tests
```

- **Resultado:** exit 0 — `GATE: APROVADO` (~183s)
- **Ambiente:** Docker 29.7.2 disponível. Sem leftover Testcontainers/Ryuk antes nem depois. `TESTCONTAINERS_RYUK_DISABLED` **não** foi usado. Retry limpo: Catalog IntegrationTests subiu Postgres (`postgres:16-alpine`) sem `DockerContainerNotFoundException`.
- **Escopo do gate:** 102 arquivos; format .NET ok (43 `.cs` Catalog); lint frontend pulado (sem eslint/prettier local); build das 5 solutions + `tsc` ok; `dotnet test` de todas as solutions + `vitest run` no frontend; `git diff --check` ok.
- **Nota de suíte:** `--all-tests` não executa Playwright; o E2E `e2e/PropertyUpdate.spec.ts` existe e foi o checkpoint da task 5.0, mas não foi reexecutado neste agregado.

## Rastreabilidade

| Requisito | Evidência no diff | Resultado |
|---|---|---|
| RF-01 criar, validar, duplicata, sem persistência parcial | `Property.Create`, validators, `POST`, `CreatePropertyTests` | Atende |
| RF-02 PATCH parcial, 403/404, imutáveis ID/Host/status | `UpdateDetails`/`EnsureOwnedBy`, `UpdatePropertyTests` | Atende |
| Contrato OpenAPI 3.1 (`createProperty`/`updateProperty`, ProblemDetails) | `api-contract.yaml`, export `contracts/openapi/catalog.json`, `PropertyOpenApiContractTests` | Atende |
| Frontend create→edit na sessão, sem GET/F03 | `PropertyWorkspacePage`, `propertyApi`, testes RTL/MSW + spec Playwright | Atende |
| Ownership Catalog, sem evento/auth real, dados fictícios | header `X-Host-Reference-Id`, logs sem nome/localização, sem GET | Atende |
| Fronteiras F02/F03/F07 | sem Accommodation, listagem, desativação, transferência | Atende |

Contratos entre tasks: 1.0 entrega aggregate/POST/erros; 2.0 reutiliza os mesmos e fecha PATCH/export; 3.0 tipos/MSW; 4.0 UI create; 5.0 UI edit + E2E. Nenhuma task consome artefato futuro.

## Revisão de design (não bloqueante)

Pressão observada: dois comandos CRUD num aggregate e mapeamento pequeno de exceções. Alternativas: manter Service Pattern + `IExceptionHandler`; extrair CQRS/handlers; Strategy table para erros. **Manter o atual.** Benefício de extração só aparece com consultas/projeções (F03) ou mais aggregates. Custo agora seria indireção sem requisito. Gatilho: segundo estilo de leitura ou terceira operação de escrita no mesmo serviço.

## Bloqueantes

Nenhum.

## Recomendações (4)

1. **OpenMetadata** — checkpoint manual da 2.0; sem PAT/`OPENMETADATA_URL` nesta revisão. Reingerir `contracts/openapi/catalog.json` quando o catálogo estiver acessível.
2. **`--all-tests` não roda Playwright** — jornada create→edit/403 não é re-provada no gate full; o spec existe. Incluir E2E no agregado se o laboratório quiser essa prova em toda validação full.
3. **`App.tsx` importa o barrel por caminho relativo** — aliases `@features/*` existem e não são usados no composition root.
4. **`alertdialog` de nova criação** — `aria-modal` sem trap de foco/Escape; dívida a11y já registrada na 5.0, fora do caminho crítico de cadastro/edição.

## Veredito

FULL VALIDATION APROVADA (gate `--all-tests`; 0 bloqueantes; 4 recomendações)
