# Especificação Técnica Frontend — Cadastro de Property

> **PRD de origem:** `tasks/prd-cadastro-property/prd.md`
> **API Contract:** `tasks/prd-cadastro-property/api-contract.yaml` (OpenAPI 3.1.0, aprovado, versão 1.0.0)
> **Baseline e ADRs:** `context/architecture-baseline.md`, `docs/adr/adr-003-frontend-teste-react.md`
> **TechSpec backend relacionada:** `tasks/prd-cadastro-property/techspec.md`
> **Data:** 2026-09-12
> **Status:** Aprovado

---

## Resumo Executivo

A F01 será adicionada ao frontend de teste React + Vite + TypeScript definido pela Fundação da
Fase 0. A interface será um cliente fino do serviço Catalog: um formulário cria a `Property` via
`createProperty` e, no mesmo fluxo, a resposta de criação alimenta o formulário de edição via
`updateProperty`. A feature não consulta banco, não replica regras de domínio e não introduz
autenticação; o Host é um UUID fictício informado explicitamente pelo usuário e enviado no header
`X-Host-Reference-Id`.

Como o frontend da Fundação ainda não existe no worktree, as versões exatas devem ser fixadas no
`package.json` durante a execução da Fundação V-04. Esta especificação escolhe APIs nativas de
React e `fetch` para estado e mutations, `openapi-typescript` para tipos de transporte, Vitest +
React Testing Library + MSW para testes e Playwright para a jornada crítica. Não há necessidade de
TanStack Query, biblioteca global de estado ou biblioteca de formulário para duas mutations sem
consulta/cache.

**Trade-off primário:** manter a edição como continuação da criação permite cumprir a jornada com
o contrato F01 aprovado e sem antecipar o endpoint de consulta da F03; em troca, o contexto de
edição é perdido no refresh e não é possível abrir uma `Property` arbitrária por URL nesta entrega.

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|---|---|---|
| `tsg-flow-frontend-techspec-creator` | `.agents/skills/tsg-flow-frontend-techspec-creator` | Estrutura, rastreabilidade, fatias verticais e ciclo de aprovação |
| `react-architecture` | `.agents/skills/react-architecture` | Evolução para estrutura intermediária, fronteira da feature, nomes e API pública |
| `react-testing` | `.agents/skills/react-testing` | Vitest, RTL, `userEvent`, MSW, Playwright e cobertura mínima |

O frontend materializado pela Fundação deve ser inspecionado antes da implementação. Se seus
caminhos ou ferramentas diferirem da TechSpec aprovada, adaptar os caminhos preservando as
fronteiras e os comportamentos definidos aqui, sem criar uma segunda estrutura concorrente.

---

## Mapeamento User Story → Tela → Operação → Teste

| User Story | Tela / Componente | Operação | Evidência automatizada |
|---|---|---|---|
| Host cadastra hospedagem | `PropertyWorkspacePage` → `CreatePropertyForm` | `createProperty` — `POST /v1/properties` | Integração RTL + MSW para loading, 201, 400 e 500; E2E do happy path |
| Host corrige nome/localização | `PropertyWorkspacePage` → `CreatedPropertySummary` → `EditPropertyForm` | ação local `startEditing` e `updateProperty` — `PATCH /v1/properties/{propertyId}` | Integração RTL + MSW para PATCH parcial, 200, 400, 403, 404 e 500; E2E create→edit |
| Autor observa respostas inequívocas | `FormErrorSummary`, erros por campo, `OperationFeedback` e resumo da Property | mapeamento local de `ProblemDetails` | Unitário do mapeamento; integração para mensagens, foco e preservação dos valores |

---

## Arquitetura de Frontend

### Nível de Estrutura

A Fundação aprovou estrutura Base enquanto existiam apenas health checks. A primeira feature de
negócio introduz duas jornadas, componentes, integração e testes próprios; por isso o frontend
evolui para a estrutura **intermediária** da skill `react-architecture`. A mudança é incremental:
os artefatos genéricos existentes continuam em `src/components`, `src/services` e `src/config`, e
a lógica específica fica isolada em `src/features/property-registration`.

### Estrutura de Pastas

```text
frontend/localize-stay-frontend/
├── e2e/
│   └── property-registration.spec.ts
├── src/
│   ├── components/                 # UI genérica já criada pela Fundação
│   ├── config/
│   │   └── env.ts                  # incluir URL do Catalog
│   ├── features/
│   │   └── property-registration/
│   │       ├── api/
│   │       │   └── propertyApi.ts
│   │       ├── components/
│   │       │   ├── CreatePropertyForm.tsx
│   │       │   ├── CreatedPropertySummary.tsx
│   │       │   ├── EditPropertyForm.tsx
│   │       │   ├── FormErrorSummary.tsx
│   │       │   └── OperationFeedback.tsx
│   │       ├── pages/
│   │       │   └── PropertyWorkspacePage.tsx
│   │       ├── types/
│   │       │   └── propertyForm.ts
│   │       ├── validation/
│   │       │   └── propertyFormValidation.ts
│   │       └── index.ts            # API pública da feature
│   ├── services/
│   │   ├── apiClient.ts
│   │   └── api/generated/
│   │       └── catalog.ts          # gerado; não editar manualmente
│   └── test/
│       ├── mocks/
│       │   ├── handlers.ts
│       │   └── server.ts
│       └── setup.ts
└── vite.config.ts
```

Consumidores importam `PropertyWorkspacePage` somente de
`@features/property-registration`. A feature pode importar `@/components`, `@/services` e
`@/config`; módulos compartilhados não importam a feature. Os aliases `@/*` e `@features/*` devem
existir de forma idêntica em Vite e TypeScript.

### Roteamento

| Rota | Componente | Layout | Auth |
|---|---|---|:---:|
| `/properties` | `PropertyWorkspacePage` | layout existente do frontend de teste | Não |

A rota concentra cadastro e edição contextual. Não criar `/properties/:id`, pois a F01 não possui
operação de leitura capaz de reidratar essa rota. Se a Fundação ainda não usar React Router, a
navegação existente pode registrar `/properties` com o mecanismo já adotado; adicionar uma
biblioteca de roteamento só para esta página não é requisito desta feature.

### Hierarquia de Componentes e Estados

```text
PropertyWorkspacePage
├── CreatePropertyForm
│   └── FormErrorSummary
├── OperationFeedback
└── CreatedPropertySummary
    └── EditPropertyForm            (aberto por ação explícita)
        └── FormErrorSummary
```

Estado da página: `idle → creating → created → editing → updating → created`. Falhas de rede ou
API retornam ao estado editável correspondente e preservam os valores digitados. Uma criação nova
substitui apenas o contexto local anterior após confirmação explícita do usuário.

---

## Geração de Tipos do API Contract

- **Ferramenta:** `openapi-typescript`, como dev dependency com versão fixada no `package-lock.json`
- **Comando no diretório do frontend:** `npm run api:generate`
- **Script:** `openapi-typescript ../../tasks/prd-cadastro-property/api-contract.yaml -o src/services/api/generated/catalog.ts`
- **Saída:** `frontend/localize-stay-frontend/src/services/api/generated/catalog.ts`
- **Regeneração:** manual durante desenvolvimento e verificação determinística no CI

| Schema/operation do contrato | Uso no frontend |
|---|---|
| `Property` | resumo após create/update e fonte inicial do formulário de edição |
| `CreatePropertyRequest` | payload de `createProperty` |
| `UpdatePropertyRequest` | payload parcial de `updateProperty` |
| `ProblemDetails` / `ProblemDetailItem` | tratamento uniforme de erros e associação por campo |
| `createProperty` / `updateProperty` | paths, request/response e status tipados em `propertyApi.ts` |

O form state é local e separado dos tipos de transporte para representar texto ainda inválido e
erros de campo. `X-Host-Reference-Id` não entra nos bodies. O arquivo gerado não deve conter lógica
manual nem ser ajustado à mão.

Check de drift proposto:

```bash
npm run api:generate
git diff --exit-code -- src/services/api/generated/catalog.ts
```

---

## Estratégia de Fetching

### Cliente e Operações

- **Lib:** `fetch` nativo encapsulado em `src/services/apiClient.ts`.
- `propertyApi.create(input, hostReferenceId, signal)` envia `POST`, JSON e o header obrigatório.
- `propertyApi.update(propertyId, changes, hostReferenceId, signal)` envia `PATCH` somente com
  campos alterados.
- A base URL vem de `env.ts`/variável Vite já definida pela Fundação para Catalog; `/v1` permanece
  parte do contrato, sem ser duplicado de forma inconsistente.
- Cada submit usa `AbortController`; desmontagem da página cancela a requisição pendente.
- Botão de submit fica desabilitado durante a mutation para impedir duplo envio acidental.

Não há cache, invalidação, retry automático ou atualização otimista. Retentar automaticamente um
`POST` sem chave de idempotência poderia criar duplicata, comportamento permitido pelo domínio mas
indesejado como efeito técnico. O usuário pode tentar novamente de forma explícita.

### Tratamento Centralizado de Erros

| HTTP / `code` | Comportamento na UI |
|---|---|
| `400 / VALIDATION_ERROR` | associar `details[].field` a `name`, `location` ou Host; manter todos os valores; focar o resumo de erros |
| `403 / HOST_OWNERSHIP_FORBIDDEN` | alerta textual no formulário de edição; manter dados; não sugerir que houve autenticação |
| `404 / PROPERTY_NOT_FOUND` | informar que a Property não existe mais/não foi encontrada; manter contexto para copiar os dados ou reiniciar |
| `500 / INTERNAL_ERROR` | mensagem genérica e `traceId` visível para diagnóstico, sem mostrar detalhes internos |
| resposta não contratual, parse inválido ou falha de rede | mensagem genérica de indisponibilidade; preservar formulário e permitir nova tentativa explícita |

Mensagens vindas de `details` podem ser exibidas porque são contratuais e legíveis. O frontend não
interpreta `title` ou texto livre para decidir comportamento; usa status e `code`.

---

## Gerenciamento de Estado

### Server State

Não há consulta nem cache. O último `Property` retornado com sucesso por create/update é um
snapshot efêmero mantido em `PropertyWorkspacePage` e substituído somente por outra resposta 2xx.

### Client State

| Estado | Onde vive | Justificativa |
|---|---|---|
| valores, touched e erros dos forms | estado local dos formulários | efêmero e específico de cada formulário |
| mutation/status/erro | página ou hook local da feature | coordena feedback sem store global |
| `createdProperty` | estado da página | permite edição na mesma sessão usando dado retornado pelo contrato |
| Host fictício | estado local; valor inicial de desenvolvimento opcional em env não secreta | explícito, mutável e não confundido com sessão autenticada |
| Property ID | derivado de `createdProperty.id` | evita ID manual sem dados atuais e sem endpoint de leitura |

Não usar `localStorage`/`sessionStorage` para a `Property`: persistir uma resposta antiga criaria
aparência de leitura atual sem consultar o Catalog. O refresh limpa o contexto deliberadamente.

---

## Validação de Formulários

- **Form lib:** APIs nativas de React e elementos HTML semânticos.
- **Schema validation:** funções locais puras em `propertyFormValidation.ts`, alinhadas ao contrato.
- **Host:** obrigatório e UUID válido.
- **Nome:** obrigatório, pelo menos um caractere não branco, máximo 120.
- **Localização:** obrigatória, pelo menos um caractere não branco, máximo 500.
- **Edição:** ao menos um campo deve mudar; o payload contém apenas os campos alterados.

`required`, `maxLength` e `aria-describedby` melhoram feedback imediato, mas as funções locais são
a fonte de validação client-side para whitespace e UUID. O backend continua sendo a autoridade; os
itens de `ProblemDetails.details` sobrescrevem/complementam erros locais após o submit. Não aplicar
`trim()` silencioso antes de enviar, pois o contrato rejeita whitespace, mas não declara
normalização de valores válidos.

---

## Mocks e Ambiente de Desenvolvimento

### Estratégia

- **Dev sem backend:** Prism a partir do contrato aprovado.
- **Testes:** MSW no processo do Vitest, com handlers por cenário e reset após cada teste.
- **E2E:** backend real da suíte quando disponível; enquanto a Fundação/backend F01 não estiverem
  materializados, um servidor MSW/fixture de browser pode provar apenas o comportamento da UI.

```bash
# Na raiz do repositório
npx @stoplight/prism-cli mock tasks/prd-cadastro-property/api-contract.yaml

# No diretório frontend/localize-stay-frontend
VITE_CATALOG_API_URL=http://localhost:4010/v1 npm run dev
```

Prism valida exemplos básicos, mas os estados 400/403/404/500 e a sequência create→update exigem
handlers MSW explícitos. Não gerar handlers automaticamente enquanto o projeto não possuir uma
ferramenta adotada para isso.

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Tipo | Skills Aplicáveis | Descrição |
|---|---|---|---|
| `frontend/localize-stay-frontend/src/features/property-registration/index.ts` | API pública | `react-architecture` | exporta somente a página necessária ao app |
| `.../pages/PropertyWorkspacePage.tsx` | Page/state | `react-architecture` | orquestra create→edit e feedback |
| `.../components/CreatePropertyForm.tsx` | Form | `react-architecture`, `react-testing` | Host, nome e localização para criação |
| `.../components/CreatedPropertySummary.tsx` | UI | `react-architecture`, `react-testing` | confirmação, ID, status e ação de editar |
| `.../components/EditPropertyForm.tsx` | Form | `react-architecture`, `react-testing` | edição prefill e PATCH somente dos campos alterados |
| `.../components/FormErrorSummary.tsx` | UI acessível | `react-testing` | resumo focável e links para campos inválidos |
| `.../components/OperationFeedback.tsx` | UI acessível | `react-testing` | feedback de sucesso/erro e trace ID |
| `.../api/propertyApi.ts` | Adapter HTTP | `react-architecture` | POST/PATCH tipados e parser de Problem Details |
| `.../types/propertyForm.ts` | Tipos locais | `react-architecture` | estado editável e erros do formulário |
| `.../validation/propertyFormValidation.ts` | Funções puras | `react-testing` | limites, whitespace, UUID e diff do PATCH |
| `src/services/api/generated/catalog.ts` | Gerado | — | tipos derivados do OpenAPI |
| `src/test/mocks/handlers.ts`, `server.ts` | Test support | `react-testing` | MSW central e handlers padrão |
| `src/test/setup.ts` | Test config | `react-testing` | jest-dom, lifecycle MSW e cleanup |
| arquivos `*.test.ts(x)` colocalizados | Testes | `react-testing` | unitários e integração por componente/fluxo |
| `e2e/property-registration.spec.ts` | E2E | `react-testing` | jornada crítica create→edit |

`...` acima representa `frontend/localize-stay-frontend/src/features/property-registration`.

### Arquivos a Modificar

| Caminho | Alteração |
|---|---|
| `frontend/localize-stay-frontend/package.json` | scripts `api:generate`, `type-check`, testes/cobertura/e2e e dev dependencies fixadas |
| `frontend/localize-stay-frontend/package-lock.json` | lock das ferramentas adicionadas |
| `frontend/localize-stay-frontend/src/App.tsx` ou router existente | registrar navegação/rota `/properties` pela API pública da feature |
| `frontend/localize-stay-frontend/src/config/env.ts` | expor e validar `VITE_CATALOG_API_URL` |
| `frontend/localize-stay-frontend/src/services/apiClient.ts` | reutilizar/estender cliente HTTP genérico, se a Fundação já o criar |
| `frontend/localize-stay-frontend/vite.config.ts` e `tsconfig*.json` | aliases coerentes e setup de teste, se ainda ausentes |
| `frontend/localize-stay-frontend/.env.example` | documentar URL local/mock do Catalog; nenhum segredo |

### Arquivos de Referência (não alterar nesta feature)

| Caminho | Motivo |
|---|---|
| `tasks/prd-cadastro-property/api-contract.yaml` | fonte de verdade de operações e schemas |
| `tasks/prd-cadastro-property/techspec.md` | semântica backend, dependências e erros |
| `tasks/prd-fundacao-fase0/techspec.md` | localização e estrutura inicial do frontend |
| `context/architecture-baseline.md` | cliente fino, CORS, dados fictícios e ausência de auth |
| `docs/adr/adr-003-frontend-teste-react.md` | React/Vite/TS e chamadas diretas às APIs |

---

## Acessibilidade

- Meta: WCAG 2.1 AA para a jornada entregue.
- Todo input possui `label`; obrigatório/erro não depende apenas de cor.
- Erros usam `aria-invalid` e `aria-describedby`; o resumo recebe foco após submit inválido ou 400.
- Feedback assíncrono usa região `role="status"` para sucesso/loading e `role="alert"` para erro.
- Ao abrir a edição, mover foco para o título ou primeiro campo sem retirar o controle do teclado.
- Durante loading, anunciar a operação e desabilitar apenas controles que causariam submit
  duplicado; não bloquear leitura/cópia do ID retornado.
- A ordem visual segue a ordem do DOM; todos os interativos têm foco visível e contraste conforme
  os estilos/tokens existentes da Fundação.

Não há biblioteca de i18n definida. Textos ficam em português nesta feature, coerentes com PRD e
contrato; não introduzir i18n sem requisito global.

---

## Análise de Impacto

| Componente afetado | Tipo | Descrição e risco | Ação |
|---|---|---|---|
| frontend da Fundação | Modificado | primeira feature de negócio; risco de criar estrutura paralela antes de V-04 | depender da Fundação e adaptar aos artefatos reais |
| navegação/roteamento | Modificado | entrada `/properties` | registrar pela API pública da feature e testar navegação |
| cliente HTTP/config | Modificado | primeira chamada de negócio ao Catalog | reutilizar base URL e parser; sem BFF |
| contrato Catalog | Referência | fonte dos tipos e erros; risco de drift | geração + diff no CI |
| CORS do Catalog | Dependência backend | browser precisa chamar origem direta | confirmar origem explícita conforme Fundação/ADR-003 |
| F03 consulta de Property | Dependência futura | desbloqueará edição reidratável por ID/rota | não antecipar GET nem persistir snapshot local |

---

## Abordagem de Testes

### Unitários — Vitest

- `propertyFormValidation`: vazio, whitespace, limites 120/500, acima do limite, UUID e diff parcial.
- `propertyApi`: headers/body corretos, ausência de campos inalterados e parsing seguro de
  `ProblemDetails`, usando MSW em vez de mockar `fetch`.
- Cobertura mínima de 70% para statements/branches/functions/lines no projeto enquanto não houver
  limite superior definido; 100% dos ramos contratuais desta feature devem ter cenário.

### Integração — React Testing Library + MSW

- render inicial e navegação por labels/roles;
- criação 201 mostra ID/status e habilita edição;
- duplicata é aceita como qualquer 201;
- validação local não dispara request e move foco ao erro;
- 400 associa detalhes aos campos e preserva entradas;
- loading evita duplo submit;
- edição preenche nome/localização e envia apenas o campo alterado;
- update 200 atualiza resumo e preserva ID/Host/status;
- 403, 404, 500 e falha de rede produzem mensagens distintas, preservam valores e permitem retry;
- refresh/remount não simula uma consulta nem restaura snapshot obsoleto.

Testes usam AAA, `userEvent` e queries semânticas. MSW reseta handlers após cada teste; nenhuma
chamada externa real ocorre no Vitest.

### E2E — Playwright

Um cenário crítico cria uma Property, copia/valida a identificação, entra em edição, altera apenas
localização e confirma que ID, Host e status permanecem. Um segundo cenário cobre rejeição de
ownership na edição. Rodar contra backend F01 isolado com dados fictícios quando ele estiver
materializado; até lá, o E2E fica dependente da fatia backend correspondente.

### Gate e Contrato

```bash
npm run lint
npm run type-check
npm run test:coverage
npm run build
npm run api:generate
git diff --exit-code -- src/services/api/generated/catalog.ts
npm run test:e2e
```

Os nomes finais dos scripts devem respeitar o `package.json` criado pela Fundação. O gate rápido
pode executar tudo exceto E2E; o E2E deve permanecer obrigatório no checkpoint final da F01.

---

## Sequenciamento de Desenvolvimento

| Fatia | Jornada e artefatos, incluindo testes | Dependências | Checkpoint |
|---|---|---|---|
| EN-01 | geração de tipos, cliente HTTP/Problem Details, env Catalog e MSW; necessário para consumir um contrato OpenAPI sem DTO manual | Fundação V-04 e contrato aprovado | `npm run api:generate && npm run type-check`; testes do adapter com MSW |
| V-01 | rota, formulário de criação, validação/acessibilidade, loading, 201/400/500, resumo e testes no mesmo incremento | EN-01; backend F01 V-01 ou Prism/MSW | testes focados de create + `npm run build`; cenário 201 e rejeições observáveis |
| V-02 | ação editar a resposta criada, form prefill, PATCH parcial, 200/400/403/404/500, atualização do resumo e testes | V-01; backend F01 V-02 | testes focados de update + suíte frontend; prova de payload parcial e preservação de estado |
| V-03 | jornada Playwright create→edit e ownership negado, contrato gerado sem drift e gate completo | V-02 e backend completo | gate completo + `npm run test:e2e` |

EN-01 é um habilitador inevitável porque o frontend da Fundação não possui geração/mocks de
operações de negócio; sua evidência é o tipo gerado e chamadas HTTP validadas por MSW, e ele
desbloqueia imediatamente V-01.

### Dependências Técnicas Bloqueantes

- Fundação V-04 materializada em `frontend/localize-stay-frontend` com React/Vite/TypeScript.
- Backend F01 V-01 para integração real de criação e V-02 para edição/E2E final.
- CORS do Catalog permitindo explicitamente a origem do frontend.
- Node/npm e versões de dependências fixadas no manifesto/lockfile da Fundação.

Mocks permitem desenvolver V-01/V-02 antes do backend, mas não substituem o E2E final contra a
implementação real.

---

## Performance

- A rota pode usar lazy loading apenas se o router/app da Fundação já adotar code splitting; não
  introduzir abstração exclusiva para uma página pequena.
- Sem prefetch/cache: existem apenas duas mutations e não há consulta.
- Nenhuma imagem ou lista volumosa faz parte da F01.
- Evitar memoização antecipada; os formulários têm poucos campos e renderização local.
- O gate de build deve sinalizar crescimento inesperado, mas não há orçamento de bundle definido
  no baseline para este frontend de laboratório.

---

## Considerações Técnicas

### Decisões Principais

- **Estrutura intermediária com feature isolada:** separa a primeira jornada de negócio do health
  dashboard e prepara APIs públicas por feature. Trade-off: mais pastas que a estrutura Base.
- **`fetch` + estado local:** atende duas mutations sem cache/store. Trade-off: política de loading e
  erro é implementada no adapter/hook local; introduzir TanStack Query agora seria custo sem query.
- **Tipos gerados por `openapi-typescript`:** reduz drift de DTO. Trade-off: tipos não validam
  runtime, por isso respostas de erro são parseadas defensivamente e a UI mantém validação local.
- **Edição somente na sessão de criação:** respeita a ausência de GET/F03. Trade-off: sem deep link,
  refresh ou retomada de Property preexistente.
- **Sem retry automático no POST/PATCH:** evita efeitos repetidos sem idempotency key. Trade-off: o
  usuário precisa solicitar nova tentativa.

### Riscos e Mitigações

- **Fundação ainda ausente:** estrutura/configuração podem divergir quando V-04 for implementada.
  Mitigação: bloquear a execução desta feature até V-04 e adaptar caminhos, não duplicar app.
- **Contrato permite edição, mas não leitura:** refresh perde os dados atuais. Mitigação: deixar o
  limite explícito e usar somente o retorno confirmado do create/update; F03 deve evoluir a jornada.
- **Host fictício parecer autenticação:** mitigação: rótulo e ajuda textual dizem que é um UUID de
  laboratório, sem linguagem de login/sessão.
- **Duplo POST:** mitigação: botão desabilitado durante envio e nenhum retry automático. O backend
  continua aceitando duplicatas conforme DP-01.
- **Drift entre mock e backend:** mitigação: tipos gerados, Prism, MSW baseado no contrato e E2E real.

### Conformidade com Skills

| Decisão | Skill | Conforme? |
|---|---|:---:|
| feature em `features/property-registration` com `index.ts` público | `react-architecture` | ✅ |
| aliases consistentes e sem imports relativos profundos | `react-architecture` | ✅ |
| Vitest/RTL, AAA, `userEvent`, queries semânticas e MSW | `react-testing` | ✅ |
| formulários com sucesso e erros; jornada crítica em Playwright | `react-testing` | ✅ |
| estrutura intermediária em vez da Base inicial | `react-architecture` | ⚠️ evolução justificada pela primeira feature de negócio e pelos fluxos futuros |

---

## Questões em Aberto

Nenhuma questão bloqueia a aprovação desta especificação. Na implementação, confirmar apenas fatos
que ainda não existem no worktree:

- [ ] caminhos e mecanismo de navegação materializados pela Fundação V-04;
- [ ] versões fixadas de Node, React, Vite, Vitest, MSW, Playwright e `openapi-typescript`;
- [ ] nome definitivo da variável de base URL do Catalog, reutilizando `env.ts` da Fundação.

Esses itens não alteram o contrato nem a jornada. Se a revisão exigir edição de uma Property após
refresh ou por ID arbitrário antes da F03, isso será mudança de escopo/contrato e deverá passar pelo
`tsg-flow-contract-creator` em modo update; não será resolvido com armazenamento local oculto.

---

## Architecture Decision Records

### Herdadas

- [ADR-003: Frontend de teste/visualização — React, sem gateway/BFF na Fase 0](../../docs/adr/adr-003-frontend-teste-react.md)
  — define stack, papel de cliente fino e chamadas diretas às APIs com CORS.

### Criadas nesta sessão

Nenhuma. Estrutura intermediária, `fetch`, geração de tipos e estado local são escolhas locais e
reversíveis da implementação da F01; não alteram o estilo arquitetural aceito.

---

## Próximos Passos

1. Materializar a Fundação V-04 antes da implementação desta feature.
2. Gerar tipos no frontend:

   ```bash
   npm run api:generate
   ```

3. Subir o mock a partir da raiz quando o backend ainda não estiver disponível:

   ```bash
   npx @stoplight/prism-cli mock tasks/prd-cadastro-property/api-contract.yaml
   ```

4. Encaminhar `prd.md`, `api-contract.yaml`, `techspec.md` e
   `frontend-techspec.md` juntos ao `tsg-flow-task-creator`, reconciliando as tasks preexistentes da
   pasta em vez de criar um plano paralelo.
