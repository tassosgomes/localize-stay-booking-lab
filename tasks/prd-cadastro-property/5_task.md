---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: [1.0, 2.0, 4.0]
---

<task_context>
<domain>frontend/property-registration/update</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database,http_server</dependencies>
<unblocks>"F02, F03 e F07"</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="PropertyUpdate"` executa RTL/MSW e Playwright contra o Catalog real, provando PATCH parcial, imutabilidade, erros e 403; geração OpenAPI não apresenta drift</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="PropertyUpdate"</gate_command>
<gate_test_selector>Arquivos `PropertyUpdate.test.tsx` e `e2e/PropertyUpdate.spec.ts`, ambos selecionáveis por `PropertyUpdate`</gate_test_selector>
<gate_expected_result>Filtro encontra testes nos dois níveis; todos passam; create→edit real altera localização e preserva identidade/Host/status; Host divergente recebe 403; sem drift</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Após criar uma Property, o Host responsável edita nome/localização na sessão; a UI envia somente diferenças, preserva dados em falhas e a jornada é confirmada no backend real</vertical_slice>
</task_context>

# Tarefa 5.0: Editar Property na interface e provar a jornada full-stack

## Relacionada às User Stories

- Host cadastra hospedagem (cobertura E2E da etapa inicial).
- Host corrige nome/localização (cobertura direta na UI e E2E).
- Autor observa respostas inequívocas (200/400/403/404/500 e rede).

## Visão Geral

Completa RF-02 na interface e fecha F01 com browser/backend reais. O formulário parte da resposta
criada, calcula o diff, envia PATCH e atualiza o resumo. MSW isola estados; Playwright prova
create→edit e ownership sem antecipar GET/F03.

## Entrega Observável

- **Entrada ou gatilho:** “Editar” no resumo, alterar um/dois campos e confirmar.
- **Resultado esperado:** 200 atualiza resumo preservando ID/Host/status; falhas mantêm valores e permitem retry.
- **Checkpoint de feedback:** gate `PropertyUpdate` com RTL/MSW, Playwright real e drift check.
- **Seletor focalizado:** `PropertyUpdate.test.tsx` e `PropertyUpdate.spec.ts`.
- **Fora deste checkpoint:** ID arbitrário, refresh, consulta, transferência e desativação.

## Requisitos

- Prefill; exigir mudança; enviar somente campos alterados.
- Preservar UUID, Host e status da resposta; não expô-los como editáveis.
- Distinguir 400/403/404/500/rede e mostrar `traceId`.
- Preservar inputs/foco em falhas; cancelar na desmontagem; impedir duplo submit.
- Não persistir snapshot nem criar GET; refresh encerra contexto.
- E2E usa dados fictícios, cria antes de editar e isola estado.

## Arquivos Envolvidos

- **Criar:**
  - `frontend/localize-stay-frontend/src/features/property-registration/components/EditPropertyForm.tsx`
  - `frontend/localize-stay-frontend/src/features/property-registration/PropertyUpdate.test.tsx`
  - `frontend/localize-stay-frontend/e2e/PropertyUpdate.spec.ts`
  - `frontend/localize-stay-frontend/playwright.config.ts`
- **Modificar:**
  - `frontend/localize-stay-frontend/src/features/property-registration/pages/PropertyWorkspacePage.tsx`
  - `frontend/localize-stay-frontend/src/features/property-registration/components/CreatedPropertySummary.tsx`
  - `frontend/localize-stay-frontend/src/features/property-registration/components/OperationFeedback.tsx`
  - `frontend/localize-stay-frontend/src/features/property-registration/validation/propertyFormValidation.ts`
  - `frontend/localize-stay-frontend/src/features/property-registration/api/propertyApi.ts`
  - `frontend/localize-stay-frontend/src/test/mocks/handlers.ts`
  - `frontend/localize-stay-frontend/package.json` e `package-lock.json`
- **Referência:**
  - `tasks/prd-cadastro-property/{prd.md,frontend-techspec.md,techspec.md,api-contract.yaml}`
  - artefatos 1.0–4.0 e `docs/adr/adr-003-frontend-teste-react.md`
- **Skills para consultar durante implementação:**
  - `react-architecture` — estado/fronteira.
  - `react-testing`, `test-guide` — MSW, Playwright e não duplicação.

## Subtarefas

- [ ] 5.1 Implementar diff, validação de mudança e formulário acessível.
- [ ] 5.2 Evoluir para `editing → updating → created`, preservando falhas.
- [ ] 5.3 Cobrir 200, inválido, 400/403/404/500/rede, retry e imutabilidade com MSW.
- [ ] 5.4 Configurar Playwright e create→edit real preservando ID/Host/status.
- [ ] 5.5 Criar E2E ownership negado com Host divergente.
- [ ] 5.6 Executar gate, E2E e geração/diff do contrato.

## Sequenciamento

- Bloqueado por: 1.0, 2.0 e 4.0.
- Desbloqueia: conclusão de F01 e F02/F03/F07.
- Paralelizável: Não; converge backend/frontend e modifica a página da 4.0.

## Rastreabilidade

- Cobre RF-02, DP-02, Frontend TechSpec V-02/V-03 e fechamento full-stack de RF-01.
- Evidência: RTL/MSW por erro e browser real UI→HTTP→domínio→PostgreSQL.

## Detalhes de Implementação

Estado: `created → editing → updating → created`. O formulário recebe a resposta confirmada,
mantém draft e calcula `UpdatePropertyRequest`; sem diferenças, não envia. Sucesso substitui o
resumo; falha mantém draft/contexto.

Playwright gera UUIDs por cenário, cria via UI e edita somente localização. O 403 cria com Host A e
edita com Host B. Não duplicar no E2E casos exaustivos do RTL nem depender da ordem.

**Convenções da stack:** queries semânticas, `userEvent`, MSW resetado, Playwright por papel/label,
AAA, sem snapshots ou mocks de hooks.

## Prontidão para Implementação

- **Decisões fechadas:** edição após create, PATCH diff, sem GET/storage, mensagens por status, E2E real, sem retry automático.
- **Limites de decisão do implementer:** textos coerentes em português e fixtures isoladas; sem ampliar contrato.
- **Dependências disponíveis:** backend 1.0/2.0 e frontend 3.0/4.0, Postgres e CORS herdados.
- **Artefatos exigidos pelo gate:** testes/config E2E desta task; app/adapter/mocks/backend anteriores.
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Gate passa: `scripts/ai-flow/gate.sh --filter="PropertyUpdate"`.
- [ ] Seletor encontra RTL e Playwright, sem casos alheios.
- [ ] Lint, type-check, cobertura mínima de 70%, build e E2E passam.
- [ ] PATCH envia só diferenças; sem mudança não envia.
- [ ] 200 preserva ID/Host/status.
- [ ] 400/403/404/500/rede preservam draft e feedback acessível.
- [ ] E2E prova create→edit e 403 com dados isolados.
- [ ] Geração seguida de diff não altera `catalog.ts`.
- [ ] Nenhum GET, storage, transferência, desativação ou retry foi introduzido.
- [ ] Todos os artefatos existem antes ou nesta task.
- [ ] Validator focused aprova no perfil standard.
