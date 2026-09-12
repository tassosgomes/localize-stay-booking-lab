---
status: pending
slice_type: enabling
verification_type: behavioral
parallelizable: true
blocked_by: ["prd-fundacao-fase0/7.0"]
---

<task_context>
<domain>frontend/property-registration/integration</domain>
<type>integration</type>
<scope>configuration</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"4.0, 5.0"</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="propertyApi.test"` gera/valida tipos e executa testes MSW do adapter, provando headers, bodies, PATCH parcial e ProblemDetails sem rede real</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="propertyApi.test"</gate_command>
<gate_test_selector>Arquivo `frontend/localize-stay-frontend/src/features/property-registration/api/propertyApi.test.ts`</gate_test_selector>
<gate_expected_result>Filtro encontra testes; todos passam; POST/PATCH seguem o OpenAPI 1.0.0; erros têm fallback seguro; type-check e build permanecem verdes</gate_expected_result>
<static_evidence>N/A — enabling behavioral validado pelo adapter HTTP com MSW</static_evidence>
<vertical_slice>N/A — enabling</vertical_slice>
</task_context>

# Tarefa 3.0: Preparar integração frontend tipada com o contrato Catalog

## Relacionada às User Stories

- Host cadastra hospedagem (suporte contratual).
- Host corrige nome/localização (suporte contratual).
- Autor observa respostas inequívocas (suporte ao parsing uniforme de erros).

## Visão Geral

Estabelece o contrato compartilhado do frontend: tipos gerados do OpenAPI, adapter `fetch`,
configuração da URL e infraestrutura MSW. É habilitador porque tipos, parser e mocks são consumidos
por create e update; mantê-los juntos evita DTOs e handlers divergentes.

## Entrega Observável

- **Entrada ou gatilho:** chamadas `propertyApi.create` e `propertyApi.update` em testes MSW.
- **Resultado esperado:** requests usam `/v1`, JSON e `X-Host-Reference-Id`; PATCH omite campos não alterados; respostas e ProblemDetails são normalizados.
- **Checkpoint de feedback:** gate `propertyApi.test`, geração sem drift, type-check e build.
- **Seletor focalizado:** `propertyApi.test.ts`.
- **Fora deste checkpoint:** formulários, navegação visual e backend real.

## Requisitos

- Fixar `openapi-typescript` e ferramentas de teste/E2E no manifesto e lock, sem upgrades amplos.
- Gerar `catalog.ts` somente de `api-contract.yaml`; proibir edição manual.
- Reutilizar `fetch` e `apiClient.ts`; não adicionar query library, store ou retry automático.
- Ler a URL Catalog pelo mecanismo Vite existente, mantendo `/v1` uma única vez.
- Parsear ProblemDetails defensivamente, incluindo `details` e `traceId`, com fallback de rede/JSON.
- Usar MSW compartilhado, reset por teste e nenhuma rede externa no Vitest.

## Arquivos Envolvidos

- **Criar:**
  - `frontend/localize-stay-frontend/src/features/property-registration/api/propertyApi.ts`
  - `frontend/localize-stay-frontend/src/features/property-registration/api/propertyApi.test.ts`
  - `frontend/localize-stay-frontend/src/services/api/generated/catalog.ts`
  - `frontend/localize-stay-frontend/src/services/apiClient.ts`
  - `frontend/localize-stay-frontend/src/test/mocks/handlers.ts`
  - `frontend/localize-stay-frontend/src/test/mocks/server.ts`
  - `frontend/localize-stay-frontend/src/test/setup.ts`
  - `frontend/localize-stay-frontend/.env.example`
- **Modificar:**
  - `frontend/localize-stay-frontend/package.json` e `package-lock.json`
  - `frontend/localize-stay-frontend/src/config/env.ts`
  - `frontend/localize-stay-frontend/vite.config.ts`
  - `frontend/localize-stay-frontend/tsconfig.json`
  - `frontend/localize-stay-frontend/tsconfig.app.json`
  - `frontend/localize-stay-frontend/tsconfig.node.json`
- **Referência:**
  - `tasks/prd-cadastro-property/{api-contract.yaml,frontend-techspec.md,techspec.md}`
  - `tasks/prd-fundacao-fase0/7_task.md`
  - `docs/adr/adr-003-frontend-teste-react.md`
- **Skills para consultar durante implementação:**
  - `react-architecture` — aliases e fronteira da feature.
  - `react-testing`, `test-guide` — MSW, isolamento e cobertura sem duplicação.

## Subtarefas

- [ ] 3.1 Fixar scripts/dependências e configurar aliases/setup sobre o app da Fundação.
- [ ] 3.2 Gerar `catalog.ts` e adicionar verificação determinística de drift.
- [ ] 3.3 Estender env/apiClient e implementar `propertyApi` com cancelamento e parsing seguro.
- [ ] 3.4 Criar servidor/handlers MSW compartilhados e testes focados do adapter.
- [ ] 3.5 Executar gate e confirmar seleção de testes sem chamadas externas.

## Sequenciamento

- Bloqueado por: Fundação 7.0.
- Desbloqueia: 4.0 e 5.0.
- Paralelizável: Sim com 1.0/2.0; não compartilha arquivos backend.

## Rastreabilidade

- Cobre EN-01 da Frontend TechSpec e os artefatos de transporte/mocks de seu inventário.
- Evidência: requests create/update, erros e drift observados por testes.

## Detalhes de Implementação

`create(input, hostReferenceId, signal)` envia POST; `update(propertyId, changes,
hostReferenceId, signal)` envia PATCH somente com alterações. Ambos usam tipos derivados de
`paths`/`components` do arquivo gerado, sem wrappers manuais nele.

Preservar itens genéricos na estrutura Base e iniciar a intermediária somente em
`src/features/property-registration`. Aliases devem coincidir no Vite e TypeScript.

**Convenções da stack:** public API será criada na 4.0; MSW em APIs; AAA; no máximo três mocks por
teste e sem mock direto de `fetch`.

## Prontidão para Implementação

- **Decisões fechadas:** `fetch`, `openapi-typescript`, MSW, tipos sem edição, sem retry e URL Vite da Fundação.
- **Limites de decisão do implementer:** adaptar o nome da variável e script de type-check ao app real, sem configuração paralela.
- **Dependências disponíveis:** contrato aprovado e Fundação 7.0.
- **Artefatos exigidos pelo gate:** teste, tipos e handlers desta task; app/config da Fundação 7.0.
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Gate passa: `scripts/ai-flow/gate.sh --filter="propertyApi.test"`.
- [ ] O seletor encontra teste e não executa componentes.
- [ ] `npm run api:generate` seguido do check de diff não altera `catalog.ts`.
- [ ] Type-check e build passam.
- [ ] POST/PATCH enviam rota, header e body exatos; PATCH omite campos inalterados.
- [ ] 201/200 são tipados e 400/403/404/500/rede produzem resultado seguro.
- [ ] Vitest não acessa rede real e handlers são resetados.
- [ ] Nenhum artefato vem de task futura.
- [ ] Validator focused aprova no perfil standard.
