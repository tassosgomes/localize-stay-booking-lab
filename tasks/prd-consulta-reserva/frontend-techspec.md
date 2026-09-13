# Especificação Técnica Frontend — Consulta de Reserva

> **PRD de origem:** `tasks/prd-consulta-reserva/prd.md`
> **API Contract:** `tasks/prd-consulta-reserva/api-contract.yaml` (OpenAPI 3.1.0, "Em Revisão", versão 1.0.0) — único endpoint (`GET /reservations/{reservationId}`), tag `Reservations`.
> **Baseline e ADRs:** `context/architecture-baseline.md` (§frontend de teste, §CORS), `docs/adr/adr-003-frontend-teste-react.md`
> **TechSpec backend relacionada:** `tasks/prd-consulta-reserva/techspec.md` (Aprovado)
> **Data:** 2026-09-12
> **Status:** Aprovado
> **Handoff:** approved — pode alimentar o Task Creator

Aprovado pelo autor nesta revisão: as duas decisões novas sem precedente direto no repositório —
nome do arquivo gerado (`reservationDetail.ts`) e rota da consulta (`/reservations/consultar`) —
foram confirmadas nas opções recomendadas desta TechSpec. Nenhuma outra ressalva bloqueia a
aprovação.

---

## Resumo Executivo

Esta feature adiciona ao frontend de teste (já com a estrutura intermediária materializada por
Cadastro de Property e Solicitação de Reserva, ambas em `main`) uma segunda jornada de Booking: um
solicitante informa o identificador de uma Reservation recebido em F01 e vê, na mesma página, seus
dados congelados, o estado atual (`solicitada`/`confirmada`/`cancelada`), a situação da saga de
pagamento (`pendente`/`autorizado`/`rejeitado`) e, quando cancelada, o motivo. É uma consulta pontual
somente leitura — sem listagem, sem atualização automática (PRD §Experiência do Usuário e
Não-Objetivos).

A stack reaproveita integralmente as decisões já validadas pelas duas features de negócio existentes
(`tasks/prd-solicitacao-reserva/frontend-techspec.md`): APIs nativas de React e `fetch` (via
`apiClient.ts` compartilhado) para a única leitura, `openapi-typescript` para tipos de transporte,
Vitest + React Testing Library + MSW para testes, Playwright para a jornada crítica. Nenhuma
biblioteca de fetching/cache é introduzida para uma única query sob demanda, sem revalidação.

**Trade-off primário:** o contrato desta feature (`GET /reservations/{reservationId}`) é um segundo
contrato do mesmo serviço Booking, distinto do já consumido em `booking.ts` (gerado a partir de
`tasks/prd-solicitacao-reserva/api-contract.yaml`). Em vez de fundir os dois YAMLs ou renomear o
arquivo gerado já em uso pela feature de solicitação (o que forçaria reabrir aquela feature já
entregue), esta TechSpec gera um segundo arquivo de tipos (`reservationDetail.ts`) a partir do
contrato próprio desta feature — mesma convenção de "um contrato por pasta de PRD, um arquivo gerado
por contrato" já em uso, só que agora com dois arquivos para o mesmo serviço. O custo é que os dois
arquivos gerados duplicam a definição de `ProblemDetails` e de alguns campos de `Reservation`; aceito
porque os contratos são versionados de forma independente por feature neste laboratório e não há
hoje um pipeline de composição de specs.

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|---|---|---|
| `tsg-flow-frontend-techspec-creator` | `.claude/skills/tsg-flow-frontend-techspec-creator` | Estrutura, rastreabilidade, fatias verticais e ciclo de aprovação |
| `react-architecture` | `.claude/skills/react-architecture` | Estrutura de pastas, fronteira da feature, API pública |
| `react-testing` | `.claude/skills/react-testing` | Vitest, RTL, `userEvent`, MSW, Playwright e cobertura mínima |

O frontend já materializado (`frontend/localize-stay-frontend`, estrutura intermediária de
`react-architecture`: `src/features`, `src/services`, `src/config`, `src/test`) é reaproveitado sem
alteração estrutural. `apiClient.ts`, `env.ts` (função `getBookingApiUrl()`), `FormErrorSummary` e
`OperationFeedback` (`src/components/`) já existem — esta feature os reutiliza sem duplicar.

---

## Mapeamento User Story → Tela → Operação → Teste

| User Story | Tela / Componente | Operação | Evidência automatizada |
|---|---|---|---|
| Guest consulta sua Reservation pelo identificador para saber se foi confirmada, cancelada ou aguarda pagamento | `ReservationLookupPage` → `ReservationLookupForm` | `getReservationById` — `GET /v1/reservations/{reservationId}` | Integração RTL + MSW: loading, 200 (3 combinações de status/sagaStatus), 404, 400 |
| Autor/arquiteto consulta situação da saga e `correlationId` para observar a jornada entre Booking e Payment | `ReservationDetailView` | mesmo endpoint — projeção de `sagaStatus`/`correlationId` | Integração RTL cobrindo os 3 `sagaStatus`; verifica `correlationId` sempre visível |
| Frontend de teste busca por identificador e exibe dados/estado sem acesso ao banco | `ReservationLookupPage` (estado da página) | mesmo endpoint | Integração RTL + MSW cobrindo os 5 cenários do AC de RF-01 |
| Guest recebe resposta clara quando o identificador não corresponde a nenhuma Reservation | `ReservationLookupPage` (estado `notFound`) | 404 `RESERVATION_NOT_FOUND` | Integração RTL + MSW para 404, distinta textualmente de 400 |

---

## Arquitetura de Frontend

### Nível de Estrutura

Nenhuma promoção de estrutura é necessária: a estrutura intermediária já existe em `main` (herdada de
Cadastro de Property e Solicitação de Reserva). Esta feature apenas adiciona uma pasta em
`src/features`.

### Estrutura de Pastas

```text
frontend/localize-stay-frontend/
├── e2e/
│   └── reservation-lookup.spec.ts
├── src/
│   ├── components/                        # reaproveitados sem alteração
│   │   ├── FormErrorSummary.tsx
│   │   └── OperationFeedback.tsx
│   ├── config/
│   │   └── env.ts                         # getBookingApiUrl() reaproveitado; nenhuma var nova
│   ├── features/
│   │   └── reservation-lookup/
│   │       ├── api/
│   │       │   └── reservationDetailApi.ts
│   │       ├── components/
│   │       │   └── ReservationDetailView.tsx
│   │       ├── pages/
│   │       │   └── ReservationLookupPage.tsx
│   │       ├── validation/
│   │       │   └── reservationIdValidation.ts
│   │       └── index.ts                   # API pública da feature
│   ├── services/
│   │   ├── apiClient.ts                   # reaproveitado, sem alteração
│   │   └── api/generated/
│   │       └── reservationDetail.ts       # gerado a partir do contrato desta feature; não editar
│   └── test/
│       ├── mocks/
│       │   ├── handlers.ts                # adiciona handlers desta feature aos existentes
│       │   └── server.ts
│       └── setup.ts
└── vite.config.ts
```

Consumidores importam `ReservationLookupPage` somente de `@features/reservation-lookup` (ou caminho
relativo equivalente, mesma convenção já usada por `reservation-request`). A feature importa
`@/components`, `@/services` e `@/config`; módulos compartilhados não importam a feature.

### Roteamento

| Rota | Componente | Layout | Auth |
|---|---|---|:---:|
| `/reservations/consultar` | `ReservationLookupPage` | layout existente do frontend de teste | Não |

Rota irmã de `/reservations` (a jornada de criação de F01), não um path param (`/reservations/:id`):
o PRD descreve uma consulta acionada por um identificador que o solicitante *informa* (cola/digita),
não uma navegação vinda de outra tela do laboratório — não há hoje nenhuma tela que já conheça esse ID
para linkar diretamente. `App.tsx` ganha uma nova entrada de navegação ("Consultar reserva").

### Hierarquia de Componentes e Estados

```text
ReservationLookupPage
├── ReservationLookupForm             (campo único: reservationId)
│   └── FormErrorSummary              (reaproveitado — formato inválido)
├── OperationFeedback                 (loading / notFound / erro genérico)
└── ReservationDetailView             (exibido só após 200)
```

Estado da página: `idle → searching → found (200) | notFound (404) | invalid (400) | failed
(500/rede)`. Diferente da jornada de F01, aqui **não há preservação de "resultado anterior" durante
uma nova busca**: trocar o identificador e buscar de novo substitui o resultado exibido (ou limpa,
se a nova busca falhar) — não há um formulário de múltiplos campos a preservar, só um campo de busca.

---

## Geração de Tipos do API Contract

- **Ferramenta:** `openapi-typescript` (mesma dev dependency já fixada no `package-lock.json`).
- **Comando:** `openapi-typescript ../../tasks/prd-consulta-reserva/api-contract.yaml -o src/services/api/generated/reservationDetail.ts`.
- **Nome do arquivo (decisão desta TechSpec, ver Trade-off primário):** `reservationDetail.ts`, não
  `booking.ts` — evita reabrir/renomear o arquivo já consumido por `reservation-request` para um
  contrato de feature diferente. Convenção resultante: **um arquivo gerado por contrato de PRD**, não
  necessariamente um por serviço backend.
- **Regeneração:** manual durante desenvolvimento; verificação determinística no CI via
  `git diff --exit-code` (mesmo mecanismo já usado para `booking.ts`, sem script dedicado de drift
  como o de Catalog).

| Schema/operation do contrato | Uso no frontend |
|---|---|
| `ReservationDetail` / `ReservationDetailResponse` | corpo de sucesso exibido em `ReservationDetailView` |
| `ProblemDetails` | tratamento uniforme de erros (400/404/500) via `code` |
| `getReservationById` | path e response tipados em `reservationDetailApi.ts` |

Check de drift:

```bash
npm run api:generate
git diff --exit-code -- src/services/api/generated/reservationDetail.ts
```

`package.json` (`api:generate`) ganha uma terceira chamada, encadeada às duas já existentes (Booking
F01 + Catalog):

```json
"api:generate": "openapi-typescript ../../tasks/prd-solicitacao-reserva/api-contract.yaml -o src/services/api/generated/booking.ts && openapi-typescript ../../tasks/prd-cadastro-property/api-contract.yaml -o src/services/api/generated/catalog.ts && openapi-typescript ../../tasks/prd-consulta-reserva/api-contract.yaml -o src/services/api/generated/reservationDetail.ts"
```

---

## Estratégia de Fetching

### Cliente e Operação

- **Lib:** `fetch` nativo via `apiClient.ts` (`apiRequest({ baseUrl, path, method: 'GET', signal })`),
  reaproveitado sem alteração.
- `reservationDetailApi.getById(reservationId, signal)` envia `GET /v1/reservations/{reservationId}`.
- Base URL: `getBookingApiUrl()` de `env.ts`, a mesma variável (`VITE_BOOKING_API_URL`) já usada por
  `reservation-request` — mesmo serviço Booking, nenhuma variável de ambiente nova.
- Cada busca usa `AbortController`; uma nova busca cancela a anterior; desmontagem da página cancela a
  pendente.
- Botão de busca fica desabilitado durante `searching` para impedir buscas concorrentes confusas.

Sem cache, invalidação, retry automático nem revalidação em background: o PRD exige explicitamente
que uma mudança de estado só apareça em uma nova consulta manual (§Experiência do Usuário, Riscos e
Mitigações — "leitura inconsistente durante a corrida da saga" é aceita).

### Tratamento Centralizado de Erros

| HTTP / `code` | Comportamento na UI |
|---|---|
| `200` | `ReservationDetailView` substitui o feedback; foco move para o título do resultado |
| `400 / VALIDATION_ERROR` | `FormErrorSummary` associado ao campo do identificador; distinto textualmente de "não encontrada" (AC de RF-01 exige essa distinção) |
| `404 / RESERVATION_NOT_FOUND` | `OperationFeedback` tom `neutral` ("Nenhuma reserva encontrada com esse identificador. Confira o identificador recebido.") — não é erro de formulário nem falha técnica |
| `500 / INTERNAL_ERROR` | `OperationFeedback` tom `error` + `traceId` visível para diagnóstico |
| resposta não contratual, parse inválido ou falha de rede | `OperationFeedback` tom `error`, mensagem genérica, permite nova tentativa |

Como o contrato desta feature não define nenhum `code` de regra de negócio (só validação e
não-encontrada), o mapeamento é direto por `(status, code)`, sem tabela de campos como em F01.

---

## Gerenciamento de Estado

### Server State

Não há cache nem revalidação. O último resultado (`ReservationDetail`, `notFound` ou erro) é um
snapshot efêmero mantido em `ReservationLookupPage`, substituído a cada nova busca.

### Client State

| Estado | Onde vive | Justificativa |
|---|---|---|
| valor digitado do identificador | estado local do formulário | efêmero, específico da busca corrente |
| status da busca (`idle/searching/found/notFound/invalid/failed`) | estado da página | coordena feedback sem store global |
| `reservation` (resultado 200) | estado da página | sem endpoint de listagem/cache; refeito a cada busca |

Sem `localStorage`/URL state para o identificador: o PRD não exige compartilhar um link direto para
uma consulta (Não-Objetivos: sem listagem, sem tempo real); manter o identificador só no estado local
evita sugerir uma capacidade de "link direto" que o contrato não declara.

---

## Validação de Formulários

- **Form lib:** APIs nativas de React (`<input type="text">` para o identificador).
- **Schema validation:** função local pura em `reservationIdValidation.ts`.
- **`reservationId`:** obrigatório, formato UUID (`Guid`). Validação local antecipa o 400 do backend
  (mesmo princípio já usado em F01: "antecipa o que o backend já decide", sem se tornar autoridade).

Diferente de F01, aqui a validação local cobre 100% do que o backend valida (só formato) — não há
regra de negócio adicional (capacidade, disponibilidade) nesta consulta somente leitura. Ainda assim,
o caminho de erro 400 do backend permanece testado (integração RTL + MSW), para cobrir uma resposta
malformada que escape da validação local (ex.: `code=VALIDATION_ERROR` com um payload que a regex
local não antecipou).

---

## Mocks e Ambiente de Desenvolvimento

### Estratégia

- **Dev sem backend:** Prism a partir do contrato aprovado desta feature (cobre os 3 exemplos de 200
  do YAML: pendente/autorizado/rejeitado).
- **Testes:** MSW no processo do Vitest, com handlers por cenário (200 × 3 combinações, 400, 404,
  500), somados aos handlers já existentes de Catalog/Booking F01 em `src/test/mocks/handlers.ts`.
- **E2E:** Booking real (mesma instância usada pela E2E de F01, `:5102`); os 3 estados de saga exigem
  a mesma técnica de seed via SQL direto já usada nos testes de integração backend desta feature
  (`techspec.md` §Testes de Integração) — o E2E frontend cobre apenas o estado alcançável pelo caminho
  real (`solicitada`/`pendente`, criado por uma solicitação real via F01) mais um cenário de
  "não encontrada"; os estados `confirmada`/`cancelada` ficam cobertos por RTL+MSW, não por E2E, pois
  dependem do mesmo artifício de seed que o backend documenta como dívida técnica (não uma jornada de
  usuário real reproduzível pelo browser).

```bash
# Na raiz do repositório
npx @stoplight/prism-cli mock tasks/prd-consulta-reserva/api-contract.yaml --port 4011

# No diretório frontend/localize-stay-frontend
VITE_BOOKING_API_URL=http://localhost:4011/v1 npm run dev
```

Porta `4011` (distinta de `4010`, já usada pelo Prism do contrato de F01) para permitir rodar os dois
mocks simultaneamente durante desenvolvimento, se necessário.

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Tipo | Skills Aplicáveis | Descrição |
|---|---|---|---|
| `.../reservation-lookup/index.ts` | API pública | `react-architecture` | exporta somente `ReservationLookupPage` |
| `.../pages/ReservationLookupPage.tsx` | Page/state | `react-architecture` | orquestra busca → resultado/erro |
| `.../components/ReservationDetailView.tsx` | UI | `react-testing` | exibe dados congelados, status, `sagaStatus`, `correlationId` e `cancellationReason` (condicional) |
| `.../api/reservationDetailApi.ts` | Adapter HTTP | `react-architecture` | GET tipado e parser de Problem Details |
| `.../validation/reservationIdValidation.ts` | Função pura | `react-testing` | formato UUID |
| `src/services/api/generated/reservationDetail.ts` | Gerado | — | tipos derivados do OpenAPI desta feature |
| `arquivos *.test.ts(x)` colocalizados | Testes | `react-testing` | unitários e integração por componente/fluxo |
| `e2e/reservation-lookup.spec.ts` | E2E | `react-testing` | jornada crítica (encontrada + não encontrada) |

`...` acima representa `frontend/localize-stay-frontend/src/features/reservation-lookup`.

### Arquivos a Modificar

| Caminho | Alteração |
|---|---|
| `frontend/localize-stay-frontend/package.json` | `api:generate` ganha a terceira chamada (contrato desta feature → `reservationDetail.ts`) |
| `frontend/localize-stay-frontend/src/App.tsx` | registrar navegação/rota `/reservations/consultar` pela API pública da feature |
| `frontend/localize-stay-frontend/src/test/mocks/handlers.ts` | adicionar handlers desta feature aos handlers existentes |

Nenhuma alteração em `env.ts` (reaproveita `getBookingApiUrl()`) nem em `apiClient.ts`.

### Arquivos de Referência (não alterar nesta feature)

| Caminho | Motivo |
|---|---|
| `tasks/prd-consulta-reserva/api-contract.yaml` | fonte de verdade das operações e schemas desta feature |
| `tasks/prd-consulta-reserva/techspec.md` | semântica backend, `sagaStatus`, `cancellationReason`, códigos de erro |
| `tasks/prd-solicitacao-reserva/frontend-techspec.md` | convenções de estrutura/fetching/testes já decididas para este mesmo frontend |
| `src/features/reservation-request/api/reservationApi.ts` | padrão de adapter HTTP com resultado em união discriminada a seguir |
| `context/architecture-baseline.md` | cliente fino, CORS, ausência de auth |
| `docs/adr/adr-003-frontend-teste-react.md` | React/Vite/TS e chamadas diretas às APIs |

---

## Acessibilidade

- Meta: WCAG 2.1 AA para a jornada entregue, mesma disciplina de F01.
- Campo do identificador possui `label`; erro de formato usa `aria-invalid`/`aria-describedby` e não
  depende apenas de cor.
- `OperationFeedback` usa `role="status"` para loading e "não encontrada" (não é erro, é ausência de
  registro) e `role="alert"` apenas para erro real (500/rede) — mesma distinção de tom já usada em F01,
  adaptada: aqui "não encontrada" fica no lado `status`, nunca `alert`.
- Ao exibir o resultado (200), mover foco para o título de `ReservationDetailView`, sem retirar o
  controle do teclado — mesmo padrão de `ReservationResultSummary` (F01).
- `cancellationReason` só é renderizado (e anunciado) quando presente; não há `dt`/`dd` vazio para os
  demais estados.

---

## Análise de Impacto

| Componente afetado | Tipo | Descrição e risco | Ação |
|---|---|---|---|
| frontend da Fundação | Modificado | terceira feature de negócio; nenhuma mudança estrutural | inspecionar handlers/rotas existentes antes de codar |
| navegação/roteamento | Modificado | entrada `/reservations/consultar` | registrar pela API pública da feature |
| `package.json` (`api:generate`) | Modificado | terceira chamada de geração encadeada | validar que as três continuam idempotentes em sequência |
| contrato de Booking F02 | Referência | fonte dos tipos e erros desta feature; risco de drift | geração + `git diff --exit-code` no CI |
| CORS de Booking | Dependência backend | já configurado para a origem do frontend (herdado de F01) | nenhuma ação nova |
| F01 (Solicitação de Reserva) | Dependência | fornece o identificador que o solicitante cola nesta tela | nenhuma integração de código, só jornada de uso |
| F04 (Conclusão da Saga, futura) | Dependência futura | hoje só o estado `solicitada`/`pendente` é alcançável por uma jornada real ponta a ponta | E2E cobre só esse estado + "não encontrada"; `confirmada`/`cancelada` ficam em RTL+MSW até F04 existir |

---

## Abordagem de Testes

### Unitários — Vitest

- `reservationIdValidation`: vazio, whitespace, UUID inválido, UUID válido.
- `reservationDetailApi`: parsing de `ReservationDetail` e de `ProblemDetails` via MSW (sem mockar
  `fetch` diretamente), incluindo `cancellationReason: null` vs. preenchido.
- Cobertura mínima de 70% (statements/branches/functions/lines), mesmo piso global do projeto; 100%
  dos ramos contratuais desta feature (5 cenários do AC de RF-01) devem ter cenário de teste.

### Integração — React Testing Library + MSW

- render inicial e navegação por labels/roles;
- identificador vazio ou malformado não dispara request e move foco ao erro local;
- 200 com `status=solicitada`/`sagaStatus=pendente` exibe todos os campos, sem `cancellationReason`;
- 200 com `status=confirmada`/`sagaStatus=autorizado` exibe os mesmos campos, sem `cancellationReason`;
- 200 com `status=cancelada`/`sagaStatus=rejeitado` exibe `cancellationReason`;
- 400 (identificador malformado vindo do backend, não bloqueado localmente) exibe erro distinto de
  "não encontrada";
- 404 exibe "não encontrada" em tom neutro (não erro);
- 500 e falha de rede exibem mensagem genérica com `traceId` quando presente e permitem nova busca;
- buscar um segundo identificador após um resultado anterior substitui o resultado exibido;
- loading evita busca concorrente (botão desabilitado).

Testes usam AAA, `userEvent` e queries semânticas. MSW reseta handlers após cada teste.

### E2E — Playwright

Um cenário crítico solicita uma reserva via jornada real de F01 (ou reaproveita um `beforeEach` que
cria uma via API direta), depois consulta esse identificador em `/reservations/consultar` e confirma
os dados exibidos, incluindo `sagaStatus=pendente`. Um segundo cenário consulta um identificador bem
formado, porém inexistente, e confirma a mensagem de "não encontrada". Estados
`confirmada`/`cancelada` não são exercitados em E2E (ver Análise de Impacto).

### Gate e Contrato

```bash
npm run lint
npm run type-check
npm run test:coverage
npm run build
npm run api:generate
git diff --exit-code -- src/services/api/generated/reservationDetail.ts
npm run test:e2e
```

---

## Sequenciamento de Desenvolvimento

| Fatia | Jornada e artefatos, incluindo testes | Dependências | Checkpoint |
|---|---|---|---|
| EN-01 | geração de tipos desta feature (`reservationDetail.ts`), handlers MSW dos 6 cenários (200×3, 400, 404, 500); necessário para consumir o contrato sem DTO manual | contrato desta feature aprovado; estrutura intermediária já existente em `main` | `npm run api:generate && npm run type-check`; chamadas do adapter validadas com MSW |
| V-01 | rota `/reservations/consultar`, formulário de um campo, validação local, loading, os 5 cenários do AC de RF-01 (200×3 + 400 + 404) mais 500, `ReservationDetailView` completo, acessibilidade e testes no mesmo incremento | EN-01; backend F02 V-01 ou MSW/Prism para os cenários cobertos por exemplo | testes focados da feature + `npm run build`; todos os cenários observáveis via MSW |
| V-02 | jornada Playwright (encontrada + não encontrada), contrato gerado sem drift e gate completo | V-01 e backend F02 completo (mesclado em `main`) | gate completo + `npm run test:e2e` |

Mesma disciplina de F01: V-01 não é fatiado por tipo de resposta — o PRD trata a feature como única e
indivisível (§Plano de Rollout Faseado: "não há Fase 2/3... a conclusão de RF-01 dá visibilidade
completa").

### Dependências Técnicas Bloqueantes

- Backend F02 (`tasks/prd-consulta-reserva/techspec.md`) mesclado em `main` para integração real e E2E
  final; até lá, Prism/MSW cobrem apenas desenvolvimento e testes automatizados.
- CORS de Booking já permite a origem do frontend (herdado de F01, sem nova configuração).
- Estrutura intermediária do frontend já materializada em `main` (confirmado nesta sessão).

---

## Performance

- Sem lazy loading exclusivo para uma página pequena; segue o padrão do router/app existente.
- Sem prefetch/cache: uma única leitura sob demanda, sem revalidação.
- Sem lista ou imagem volumosa nesta feature.
- Evitar memoização antecipada; página com um campo de busca e um bloco de detalhe.
- Sem orçamento de bundle formal, mesma posição já adotada pelas duas features anteriores.

---

## Considerações Técnicas

### Decisões Principais

- **Decisão:** gerar um segundo arquivo de tipos (`reservationDetail.ts`) para o segundo contrato do
  serviço Booking, em vez de reabrir/renomear `booking.ts`.
  **Racional:** `booking.ts` já é consumido por `reservation-request` (feature entregue); renomear
  forçaria revisar aquele código só por causa de uma convenção de nomenclatura.
  **Trade-offs:** os dois arquivos gerados duplicam `ProblemDetails` e parte dos campos de
  `Reservation`; aceitável neste laboratório (contratos pequenos, versionados por feature).
  **Alternativas rejeitadas:** compor um único `api-contract.yaml` de Booking com as duas operações —
  rejeitada por exigir tocar o contrato já aprovado de F01 (mudança de escopo fora desta feature) e
  por não existir hoje um processo de composição entre PRDs distintos.

- **Decisão:** rota própria `/reservations/consultar` (não `/reservations/:id`).
  **Racional:** o PRD descreve um solicitante que *informa* o identificador recebido fora de banda
  (ex.: copiado do resultado de F01); não há hoje nenhuma tela que já conheça esse ID para linkar
  diretamente com um path param.
  **Trade-offs:** sem deep-link direto para uma Reservation específica por enquanto; pode ser
  revisitado como mudança de contrato/rota se uma feature futura precisar linkar diretamente (ex.: um
  botão "ver reserva" ao final de F01) — não é uma mudança de contrato de API, só de rota frontend.

- **Decisão:** 404 (`RESERVATION_NOT_FOUND`) tratado com `role="status"` (tom neutro), nunca
  `role="alert"`.
  **Racional:** AC de RF-01 do PRD trata "não encontrada" como uma resposta esperada e válida do
  fluxo, distinta de um erro técnico — mesmo princípio de tom que F01 já aplicou ao 503.
  **Trade-offs:** nenhum.

- **Decisão:** sem preservação de resultado anterior ao iniciar uma nova busca (diferente de F01, que
  preserva os valores do formulário em toda rejeição).
  **Racional:** aqui o "formulário" é um único campo de busca; não há um conjunto de valores
  complexos a proteger de perda, e mostrar um resultado antigo junto de uma nova busca em andamento
  poderia ser lido como o resultado da nova busca.
  **Trade-offs:** nenhum — o campo de busca em si preserva o texto digitado até uma nova submissão.

### Riscos e Mitigações

- **Estados `confirmada`/`cancelada` não exercitáveis por uma jornada real de usuário ainda:** aceito
  no backend (`techspec.md` §Riscos Conhecidos) e propagado aqui — cobertos por RTL+MSW, não por E2E,
  até F04 existir.
- **Confusão entre 400 e 404:** um solicitante pode não perceber a diferença entre "identificador mal
  formatado" e "identificador não encontrado". Mitigação: textos e handlers de erro
  deliberadamente distintos, cada um com seu próprio cenário de teste.
- **Leitura inconsistente durante a corrida da saga:** já aceito no PRD; sem mitigação nesta feature,
  o solicitante busca novamente.
- **Drift entre mock e backend:** mitigação: tipos gerados, Prism, MSW baseados no contrato e E2E
  real para o estado alcançável.

### Conformidade com Skills

| Decisão | Skill | Conforme? |
|---|---|:---:|
| feature em `features/reservation-lookup` com `index.ts` público | `react-architecture` | ✅ |
| aliases consistentes e sem imports relativos profundos | `react-architecture` | ✅ |
| Vitest/RTL, AAA, `userEvent`, queries semânticas e MSW | `react-testing` | ✅ |
| jornada com sucesso e erros; caminho crítico em Playwright | `react-testing` | ✅ |
| componentes genéricos reaproveitados (`FormErrorSummary`, `OperationFeedback`) | `react-architecture` | ✅ |

---

## Questões em Aberto

- [x] **Nome do arquivo gerado (`reservationDetail.ts`) e da rota (`/reservations/consultar`)** —
  confirmado pelo autor nesta revisão (opções recomendadas desta TechSpec).
- [ ] Confirmar se um botão "Consultar esta reserva" ao final da jornada de F01
  (`ReservationResultSummary`) deveria linkar diretamente para esta consulta — não é exigido pelo PRD
  desta feature (Não-Objetivos globais: sem alteração de F01) nem pelo PRD de F01; registrar aqui como
  melhoria futura, não bloqueia esta entrega.
- [ ] Versões fixadas de Node/React/Vite/Vitest/MSW/Playwright/`openapi-typescript` — já fixadas pelo
  projeto (ver `package.json`); nenhuma mudança de versão é necessária para esta feature.

---

## Architecture Decision Records

### Herdadas

- [ADR-003: Frontend de teste/visualização — React, sem gateway/BFF na Fase 0](../../docs/adr/adr-003-frontend-teste-react.md)
  — define stack, papel de cliente fino e chamadas diretas às APIs com CORS.

### Criadas nesta sessão

Nenhuma. Um segundo arquivo de tipos gerados e uma rota adicional são decisões locais e reversíveis
desta implementação; não alteram o estilo arquitetural já aceito.

---

## Próximos Passos

1. **Implementação:** usar `tsg-flow-task-creator` referenciando `frontend-techspec.md`,
   `techspec.md`, `prd.md` e `api-contract.yaml` desta pasta.
2. Gerar tipos no frontend:

   ```bash
   npm run api:generate
   ```

3. Subir o mock a partir da raiz quando o backend F02 ainda não estiver disponível:

   ```bash
   npx @stoplight/prism-cli mock tasks/prd-consulta-reserva/api-contract.yaml --port 4011
   ```
