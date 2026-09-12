---
status: done
slice_type: vertical
verification_type: behavioral
parallelizable: true
blocked_by: [3.0]
---

<task_context>
<domain>frontend/property-registration/create</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"5.0"</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="PropertyCreate.test"` prova 201, validação local/400/500, duplicata aceita, preservação de valores e feedback acessível usando MSW</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="PropertyCreate.test"</gate_command>
<gate_test_selector>Arquivo `frontend/localize-stay-frontend/src/features/property-registration/PropertyCreate.test.tsx`</gate_test_selector>
<gate_expected_result>Filtro encontra testes; todos passam; `/properties` permite criar e exibe ID/status; erros impedem request ou preservam entradas; build verde</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Host informa UUID fictício, nome e localização em `/properties`; sucesso mostra resumo editável e erros locais/API são acessíveis sem perder valores</vertical_slice>
</task_context>

# Tarefa 4.0: Cadastrar Property pela interface acessível

## Relacionada às User Stories

- Host cadastra hospedagem com dados básicos (cobertura direta na UI).
- Autor observa respostas inequívocas para cadastro válido e inválido (cobertura direta na UI).

## Visão Geral

Entrega RF-01 no frontend sobre a 3.0. A página coleta Host, nome e localização, valida sem
normalização silenciosa, previne submit duplicado e mostra a Property ou erros acionáveis.

## Entrega Observável

- **Entrada ou gatilho:** abrir `/properties`, preencher e submeter.
- **Resultado esperado:** 201 mostra confirmação, ID copiável e status; validações/400/500 aparecem semanticamente e preservam o formulário.
- **Checkpoint de feedback:** gate `PropertyCreate.test` com RTL/userEvent/MSW.
- **Seletor focalizado:** testes com `PropertyCreate.test`.
- **Fora deste checkpoint:** edição, refresh/reidratação, backend real e ownership.

## Requisitos

- Evoluir para estrutura intermediária e exportar somente a página em `index.ts`.
- Registrar `/properties` no mecanismo existente; não adicionar router só para esta tela.
- Validar Host UUID, nome 1..120 e localização 1..500, rejeitando whitespace sem trim.
- Desabilitar submit duplicado, cancelar na desmontagem e não aplicar retry automático.
- Associar 400 aos campos; falha geral mostra mensagem e `traceId`.
- Cumprir WCAG 2.1 AA da spec: labels, foco, aria-invalid/describedby, status/alert e foco visível.

## Arquivos Envolvidos

- **Criar:**
  - `frontend/localize-stay-frontend/src/features/property-registration/index.ts`
  - `frontend/localize-stay-frontend/src/features/property-registration/pages/PropertyWorkspacePage.tsx`
  - `frontend/localize-stay-frontend/src/features/property-registration/components/CreatePropertyForm.tsx`
  - `frontend/localize-stay-frontend/src/features/property-registration/components/CreatedPropertySummary.tsx`
  - `frontend/localize-stay-frontend/src/features/property-registration/components/FormErrorSummary.tsx`
  - `frontend/localize-stay-frontend/src/features/property-registration/components/OperationFeedback.tsx`
  - `frontend/localize-stay-frontend/src/features/property-registration/types/propertyForm.ts`
  - `frontend/localize-stay-frontend/src/features/property-registration/validation/propertyFormValidation.ts`
  - `frontend/localize-stay-frontend/src/features/property-registration/validation/propertyFormValidation.test.ts`
  - `frontend/localize-stay-frontend/src/features/property-registration/PropertyCreate.test.tsx`
- **Modificar:**
  - `frontend/localize-stay-frontend/src/App.tsx`
  - `frontend/localize-stay-frontend/src/App.css` (layout, estados e foco visível; sem design system global)
- **Referência:**
  - `tasks/prd-cadastro-property/{prd.md,frontend-techspec.md,api-contract.yaml}`
  - artefatos da 3.0 e `docs/adr/adr-003-frontend-teste-react.md`
- **Skills para consultar durante implementação:**
  - `react-architecture` — API pública e fronteiras.
  - `react-testing`, `test-guide` — formulário, MSW e queries semânticas.

## Subtarefas

- [ ] 4.1 Criar tipos/validação e cobrir UUID, whitespace e limites.
- [ ] 4.2 Implementar formulário, resumo e feedback acessíveis.
- [ ] 4.3 Orquestrar `idle → creating → created` com cancelamento/preservação.
- [ ] 4.4 Expor `index.ts` e registrar `/properties` sem router novo.
- [ ] 4.5 Criar RTL+MSW para 201, duplicata, validação, 400, 500 e duplo submit.
- [ ] 4.6 Executar gate, type-check e build.

## Sequenciamento

- Bloqueado por: 3.0.
- Desbloqueia: 5.0.
- Paralelizável: Sim com 1.0/2.0; usa MSW, não backend real.

## Rastreabilidade

- Cobre RF-01 na interface, experiência e Frontend TechSpec V-01.
- Evidência: comportamento por label/role, request MSW e resumo após 201.

## Detalhes de Implementação

`PropertyWorkspacePage` mantém estado local. Após create, conserva a resposta para resumo e edição;
refresh volta ao início. Nova criação só substitui o contexto após confirmação explícita.

Se não houver router, `App.tsx` usa navegação existente e seleção mínima pelo pathname; não instalar
React Router. Componentes apresentam; validação/adapter carregam lógica; compartilhados não importam
a feature.

**Convenções da stack:** PascalCase, camelCase, pasta kebab-case, imports pela API pública, RTL com
`userEvent` e role/label, sem snapshots.

## Prontidão para Implementação

- **Decisões fechadas:** `/properties`, estado local, sem GET/deep link/store/form lib/router, português e acessibilidade aprovada.
- **Limites de decisão do implementer:** composição visual e stylesheet local seguindo tokens existentes.
- **Dependências disponíveis:** adapter/tipos/MSW da 3.0.
- **Artefatos exigidos pelo gate:** testes/validação desta task; setup/handlers da 3.0.
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Gate passa: `scripts/ai-flow/gate.sh --filter="PropertyCreate.test"`.
- [ ] Seletor encontra teste e exclui edição/E2E.
- [ ] Type-check, cobertura mínima de 70% e build passam.
- [ ] 201 mostra confirmação, ID, Host, dados e `active`.
- [ ] Inválido local não chama API; 400 associa campos; 500/rede permitem retry manual.
- [ ] Entradas permanecem em erro e loading impede submit duplicado.
- [ ] Labels, foco e ARIA são observados nos testes.
- [ ] `/properties` não quebra a tela de saúde.
- [ ] Nenhum artefato vem de task futura.
- [ ] Validator focused aprova no perfil standard.
