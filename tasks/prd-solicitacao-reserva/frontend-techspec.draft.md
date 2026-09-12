# Especificação Técnica Frontend — Solicitação de Reserva

> **PRD de origem:** `tasks/prd-solicitacao-reserva/prd.md`
> **API Contract:** `tasks/prd-solicitacao-reserva/api-contract.yaml` (OpenAPI 3.1.0, "Em Revisão", versão 1.0.0) — só a tag `Reservations` (`POST /v1/reservations`) é consumida pelo frontend; a tag `Catalog Availability (dependency)` é backend-to-backend e não gera artefato de UI.
> **Baseline e ADRs:** `context/architecture-baseline.md` (§frontend de teste, §CORS), `docs/adr/adr-003-frontend-teste-react.md`
> **TechSpec backend relacionada:** `tasks/prd-solicitacao-reserva/techspec.md` (Aprovado)
> **Data:** 2026-09-12
> **Status:** Em Revisão

---

## Resumo Executivo

Esta feature adiciona ao frontend de teste React + Vite + TypeScript da Fase 0 uma única jornada: um
Guest preenche Accommodation, Guest de referência, período e número de hóspedes, envia a solicitação
a `POST /v1/reservations` (Booking) e vê o resultado — a Reservation criada em `solicitada` com
preço/moeda/total congelados, ou o motivo pelo qual nenhuma Reservation foi criada. Não há edição,
listagem ou consulta por ID: o contrato de Booking F01 só expõe criação, e a consulta (F02) ainda não
existe. É um cliente fino, sem regra de negócio própria — toda validação client-side apenas antecipa
o que o backend já decide (RN-02/RN-03 de `domains/booking/domain.md`).

A stack reaproveita as decisões já registradas no draft irmão de Catalog
(`tasks/prd-cadastro-property/frontend-techspec.draft.md`): APIs nativas de React e `fetch` para
estado e mutation, `openapi-typescript` para tipos de transporte, Vitest + React Testing Library + MSW
para testes, Playwright para a jornada crítica. Não há biblioteca de fetching/cache nem de formulário
para uma única mutation sem consulta.

**Trade-off primário:** como o PRD declara a feature "única e indivisível" (nenhum rollout interno —
o objetivo de estudo só é demonstrado com sucesso e as 6 rejeições implementados juntos), a jornada
frontend também é entregue como uma fatia vertical única cobrindo todos os cenários do contrato, em
vez de fatiar por tipo de rejeição; em troca, a primeira fatia comportamental é maior que a de
Cadastro de Property (que pôde separar criação de edição).

---

## Skills de Referência

| Skill | Caminho | Decisões Influenciadas |
|---|---|---|
| `tsg-flow-frontend-techspec-creator` | `.agents/skills/tsg-flow-frontend-techspec-creator` | Estrutura, rastreabilidade, fatias verticais e ciclo de aprovação |
| `react-architecture` | `.agents/skills/react-architecture` | Estrutura de pastas, fronteira da feature, API pública |
| `react-testing` | `.agents/skills/react-testing` | Vitest, RTL, `userEvent`, MSW, Playwright e cobertura mínima |

O frontend materializado pela Fundação (e, possivelmente, já evoluído pela feature de Cadastro de
Property) deve ser inspecionado antes da implementação. Reaproveitar estrutura, `apiClient.ts`,
`env.ts` e componentes genéricos (`FormErrorSummary`, `OperationFeedback`) se já existirem; criá-los
aqui apenas se ainda não existirem, sem duplicar uma segunda versão concorrente.

---

## Mapeamento User Story → Tela → Operação → Teste

| User Story | Tela / Componente | Operação | Evidência automatizada |
|---|---|---|---|
| Guest solicita reserva informando Accommodation, período e hóspedes | `ReservationRequestPage` → `RequestReservationForm` | `requestReservation` — `POST /v1/reservations` | Integração RTL + MSW para loading, 201, 400, 422 (5 variações) e 500; unitário da validação local |
| Guest é informado imediatamente quando o pedido não pode ser aceito (período, capacidade, disponibilidade) | `RequestReservationForm` → `FormErrorSummary` | mapeamento local de `ProblemDetails.code` para os 5 `code` de 422 | Unitário do mapeamento `code → campo/mensagem`; integração cobrindo os 5 cenários 422 |
| Guest é informado que a validação não pôde ser concluída (Catalog indisponível), sem interpretar como rejeição | `OperationFeedback` (tom de falha temporária, não de erro de formulário) | resposta 503 `CATALOG_INDISPONIVEL` | Integração RTL + MSW para 503; distingue textualmente de um 422 |
| Frontend de teste exibe o resultado (sucesso com dados congelados) | `ReservationResultSummary` | corpo de `201` (`Reservation`) | Integração RTL + MSW para 201; E2E do happy path |

---

## Arquitetura de Frontend

### Nível de Estrutura

Mesma decisão do draft de Cadastro de Property: a Fundação entrega apenas a estrutura Base (health
checks). Esta é a segunda feature de negócio do frontend — se a primeira (Cadastro de Property) já
tiver promovido o projeto para a estrutura **intermediária** da skill `react-architecture`
(`src/features/<feature>/`, `src/services`, `src/config`, `src/test`), esta feature apenas adiciona
mais uma pasta em `src/features`. Se nenhuma das duas tiver sido implementada ainda, esta TechSpec
promove a estrutura sozinha, seguindo o mesmo padrão — sem introduzir uma convenção divergente.

### Estrutura de Pastas

```text
frontend/localize-stay-frontend/
├── e2e/
│   └── reservation-request.spec.ts
├── src/
│   ├── components/                       # UI genérica compartilhada entre features
│   │   ├── FormErrorSummary.tsx          # reaproveitar se Cadastro de Property já criou
│   │   └── OperationFeedback.tsx         # reaproveitar se Cadastro de Property já criou
│   ├── config/
│   │   └── env.ts                        # incluir URL do Booking (além da de Catalog, se existir)
│   ├── features/
│   │   └── reservation-request/
│   │       ├── api/
│   │       │   └── reservationApi.ts
│   │       ├── components/
│   │       │   ├── RequestReservationForm.tsx
│   │       │   └── ReservationResultSummary.tsx
│   │       ├── pages/
│   │       │   └── ReservationRequestPage.tsx
│   │       ├── types/
│   │       │   └── reservationForm.ts
│   │       ├── validation/
│   │       │   └── reservationFormValidation.ts
│   │       ├── errors/
│   │       │   └── reservationErrorMapping.ts
│   │       └── index.ts                  # API pública da feature
│   ├── services/
│   │   ├── apiClient.ts                  # reaproveitado/estendido, não duplicado
│   │   └── api/generated/
│   │       └── booking.ts                # gerado; não editar manualmente
│   └── test/
│       ├── mocks/
│       │   ├── handlers.ts               # adiciona handlers de Booking aos existentes
│       │   └── server.ts
│       └── setup.ts
└── vite.config.ts
```

Consumidores importam `ReservationRequestPage` somente de `@features/reservation-request`. A feature
pode importar `@/components`, `@/services` e `@/config`; módulos compartilhados não importam a
feature — mesma regra de fronteira do draft de Catalog.

### Roteamento

| Rota | Componente | Layout | Auth |
|---|---|---|:---:|
| `/reservations` | `ReservationRequestPage` | layout existente do frontend de teste | Não |

Não criar `/reservations/:id`: o contrato de F01 não tem operação de leitura (isso é F02). O
Accommodation ID é copiado pelo Guest de outra tela (ex.: o resumo de criação de `/properties`, se
Cadastro de Property já estiver implementado) ou digitado manualmente — não há seletor/lista de
Accommodations nesta feature.

### Hierarquia de Componentes e Estados

```text
ReservationRequestPage
├── RequestReservationForm
│   └── FormErrorSummary
├── OperationFeedback
└── ReservationResultSummary          (exibido só após 201)
```

Estado da página: `idle → submitting → requested (201) | rejected (422) | unavailable (503) | failed
(500/rede)`. Em `rejected`/`unavailable`/`failed`, a página retorna ao formulário editável com os
valores preservados. Em `requested`, o formulário é substituído pelo resumo da Reservation criada; um
botão explícito "Nova solicitação" reseta para `idle` com formulário vazio — não há continuação para
pagamento (isso é F03, fora do escopo).

---

## Geração de Tipos do API Contract

- **Ferramenta:** `openapi-typescript`, dev dependency com versão fixada no `package-lock.json`.
- **Comando:** `openapi-typescript ../../tasks/prd-solicitacao-reserva/api-contract.yaml -o src/services/api/generated/booking.ts`
  (caminho `src/services/api/generated/` para seguir a mesma convenção já usada por `catalog.ts` no
  draft de Cadastro de Property; corrige a sugestão genérica `src/types/booking-api.ts` de
  `api-contract.md`, que foi escrita antes de existir uma convenção de frontend no projeto).
- **Regeneração:** manual durante desenvolvimento e verificação determinística no CI (`git diff --exit-code`).

| Schema/operation do contrato | Uso no frontend |
|---|---|
| `CreateReservationRequest` | payload de `requestReservation` |
| `Reservation` / `ReservationResponse` | corpo de sucesso exibido em `ReservationResultSummary` |
| `ProblemDetails` | tratamento uniforme de erros (400/422/503/500) via `code` |
| `requestReservation` | path, request/response e status tipados em `reservationApi.ts` |

`AvailabilityCheckResponse` e o path `checkAccommodationAvailability` existem no mesmo arquivo YAML,
mas pertencem à dependência backend-to-backend de Catalog — o arquivo gerado os inclui, mas nenhum
código do frontend os importa; não filtrar o gerador por operação para não desviar da convenção já
estabelecida (arquivo gerado inteiro, uso seletivo no consumidor).

O form state é local, separado dos tipos de transporte, para representar texto ainda inválido (ex.:
campo de data vazio) antes de existir um `CreateReservationRequest` válido.

Check de drift:

```bash
npm run api:generate
git diff --exit-code -- src/services/api/generated/booking.ts
```

---

## Estratégia de Fetching

### Cliente e Operações

- **Lib:** `fetch` nativo encapsulado em `src/services/apiClient.ts` (reaproveitado; se Cadastro de
  Property ainda não o criou, esta feature cria a versão mínima genérica, não uma cópia paralela).
- `reservationApi.request(input, signal)` envia `POST /v1/reservations`, JSON, sem headers de
  identidade adicionais (o Guest de referência vai no corpo, não em header — diferente do
  `X-Host-Reference-Id` de Cadastro de Property, porque o contrato desta feature modela
  `guestReference` como campo do payload, não como header).
- Base URL do Booking vem de `env.ts`/variável Vite dedicada (`VITE_BOOKING_API_URL`), distinta da
  variável de Catalog — os dois serviços rodam em portas diferentes (Booking `:5102`, conforme a
  resolução de conflito de porta já registrada na TechSpec backend).
- Cada submit usa `AbortController`; desmontagem da página cancela a requisição pendente.
- Botão de submit fica desabilitado durante a mutation para impedir duplo envio.

Sem cache, invalidação ou retry automático. O contrato não define idempotência para `POST
/reservations`; retentar automaticamente poderia criar uma segunda Reservation para a mesma
intenção. O Guest pode tentar novamente de forma explícita (o próprio AC de RF-01 prevê isso para o
cenário 503).

### Tratamento Centralizado de Erros

| HTTP / `code` | Comportamento na UI |
|---|---|
| `400 / VALIDATION_ERROR` | mensagem genérica de requisição malformada no `FormErrorSummary` (o contrato não expõe `details[].field` — diferente do contrato de Cadastro de Property — então não há como associar a um campo específico); preserva valores; foca o resumo |
| `422 / PERIODO_INVALIDO` | erro associado a `checkIn`/`checkOut`; mensagem pede correção do período |
| `422 / QUANTIDADE_HOSPEDES_INVALIDA` | erro associado a `guestsCount`; mensagem pede valor positivo |
| `422 / ACOMODACAO_INDISPONIVEL` | erro associado a `accommodationId`; mensagem indica acomodação inexistente ou inativa |
| `422 / CAPACIDADE_EXCEDIDA` | erro associado a `guestsCount` (e referência a `accommodationId`); mensagem indica capacidade máxima excedida |
| `422 / PERIODO_INDISPONIVEL` | erro associado a `checkIn`/`checkOut`; mensagem indica indisponibilidade no período |
| `503 / CATALOG_INDISPONIVEL` | **não** tratado como erro de formulário: `OperationFeedback` em tom de falha temporária ("não foi possível validar agora, tente novamente"), preserva valores, permite reenvio imediato — AC de RF-01 exige que isto nunca pareça rejeição |
| `500 / INTERNAL_ERROR` | mensagem genérica + `traceId` visível para diagnóstico |
| resposta não contratual, parse inválido ou falha de rede | mensagem genérica de indisponibilidade; preserva formulário; permite nova tentativa explícita |

Como os 5 `code` de 422 já identificam univocamente qual dado corrigir, o mapeamento `code → campo`
é uma função pura local (`reservationErrorMapping.ts`), sem depender de `details[]`. O frontend não
interpreta `title`/`detail` para decidir comportamento.

---

## Gerenciamento de Estado

### Server State

Não há consulta nem cache. A última `Reservation` criada com sucesso é um snapshot efêmero mantido em
`ReservationRequestPage`, exibido até o Guest iniciar uma nova solicitação.

### Client State

| Estado | Onde vive | Justificativa |
|---|---|---|
| valores, touched e erros do formulário | estado local do formulário | efêmero e específico da solicitação corrente |
| status da mutation (`idle/submitting/requested/rejected/unavailable/failed`) | estado da página | coordena feedback sem store global |
| `createdReservation` | estado da página | dado retornado pelo contrato; não há endpoint de leitura para reidratar depois |

Sem `localStorage`/`sessionStorage` para a `Reservation`: persistir um resultado antigo criaria
aparência de estado atual sem consultar Booking (que hoje nem expõe consulta — F02). Refresh limpa o
contexto deliberadamente, mesma disciplina do draft de Catalog.

---

## Validação de Formulários

- **Form lib:** APIs nativas de React e elementos HTML semânticos (`<input type="date">` para
  check-in/check-out, produzindo `yyyy-MM-dd` nativamente — mesmo formato do contrato).
- **Schema validation:** funções locais puras em `reservationFormValidation.ts`.
- **`accommodationId`:** obrigatório, formato UUID.
- **`guestReference`:** obrigatório, pelo menos um caractere não branco, máximo 255 (mesmo limite do
  contrato).
- **Período:** `checkIn` e `checkOut` obrigatórios; `checkOut` deve ser posterior a `checkIn`
  (antecipa RN-02/`PERIODO_INVALIDO`, mas o backend continua sendo a autoridade).
- **`guestsCount`:** obrigatório, inteiro, mínimo 1 (antecipa RN-03/`QUANTIDADE_HOSPEDES_INVALIDA`).

A validação local só antecipa período e hóspedes (checagens locais, sem I/O). Capacidade,
disponibilidade e existência/status da Accommodation (`ACOMODACAO_INDISPONIVEL`,
`CAPACIDADE_EXCEDIDA`, `PERIODO_INDISPONIVEL`) dependem da resposta de Catalog e só podem ser
conhecidos depois do submit — não há como (nem deveria) o frontend simulá-los. Não aplicar `trim()`
silencioso em `guestReference` antes de enviar, pelo mesmo motivo já registrado no draft de Catalog:
o contrato não declara normalização de valores válidos.

---

## Mocks e Ambiente de Desenvolvimento

### Estratégia

- **Dev sem backend:** Prism a partir do contrato aprovado (cobre só os exemplos de 200/201/404
  declarados no YAML).
- **Testes:** MSW no processo do Vitest, com handlers por cenário (201, 400, cada um dos 5 `code` de
  422, 503, 500) e reset após cada teste.
- **E2E:** backend real (Booking + Catalog) quando disponível; enquanto a fundação/backend F01 não
  estiverem materializados, o E2E fica bloqueado (ver Dependências Técnicas Bloqueantes) — um
  servidor MSW/fixture de browser pode no máximo provar a UI isoladamente, não substitui o E2E real.

```bash
# Na raiz do repositório
npx @stoplight/prism-cli mock tasks/prd-solicitacao-reserva/api-contract.yaml

# No diretório frontend/localize-stay-frontend
VITE_BOOKING_API_URL=http://localhost:4010/v1 npm run dev
```

Prism só cobre os exemplos do contrato (sucesso e `PERIODO_INVALIDO`/`QUANTIDADE_HOSPEDES_INVALIDA`
implícitos via corpo malformado); os 5 cenários de 422 completos, 503 e 500 exigem handlers MSW
explícitos para os testes automatizados.

---

## Inventário de Artefatos

### Arquivos a Criar

| Caminho | Tipo | Skills Aplicáveis | Descrição |
|---|---|---|---|
| `.../reservation-request/index.ts` | API pública | `react-architecture` | exporta somente `ReservationRequestPage` |
| `.../pages/ReservationRequestPage.tsx` | Page/state | `react-architecture` | orquestra submit → resultado/erro |
| `.../components/RequestReservationForm.tsx` | Form | `react-architecture`, `react-testing` | Accommodation, Guest de referência, período, hóspedes |
| `.../components/ReservationResultSummary.tsx` | UI | `react-testing` | exibe ID, status `solicitada`, preço/moeda/total, nota de que pagamento ainda não foi solicitado |
| `.../api/reservationApi.ts` | Adapter HTTP | `react-architecture` | POST tipado e parser de Problem Details |
| `.../types/reservationForm.ts` | Tipos locais | `react-architecture` | estado editável e erros do formulário |
| `.../validation/reservationFormValidation.ts` | Funções puras | `react-testing` | período, hóspedes, UUID, whitespace |
| `.../errors/reservationErrorMapping.ts` | Funções puras | `react-testing` | `code` → campo/mensagem/tom (rejeição vs. falha temporária) |
| `src/components/FormErrorSummary.tsx` | UI acessível | `react-testing` | **só se ainda não existir** (Cadastro de Property pode já tê-lo criado) |
| `src/components/OperationFeedback.tsx` | UI acessível | `react-testing` | **só se ainda não existir** |
| `src/services/api/generated/booking.ts` | Gerado | — | tipos derivados do OpenAPI de Booking |
| `arquivos *.test.ts(x)` colocalizados | Testes | `react-testing` | unitários e integração por componente/fluxo |
| `e2e/reservation-request.spec.ts` | E2E | `react-testing` | jornada crítica (sucesso + uma rejeição representativa) |

`...` acima representa `frontend/localize-stay-frontend/src/features/reservation-request`.

### Arquivos a Modificar

| Caminho | Alteração |
|---|---|
| `frontend/localize-stay-frontend/package.json` | scripts `api:generate` (adicionar Booking, se o script já existir para Catalog) |
| `frontend/localize-stay-frontend/src/App.tsx` ou router existente | registrar navegação/rota `/reservations` pela API pública da feature |
| `frontend/localize-stay-frontend/src/config/env.ts` | expor e validar `VITE_BOOKING_API_URL` |
| `frontend/localize-stay-frontend/src/services/apiClient.ts` | reutilizar/estender, se necessário suportar múltiplas base URLs por serviço |
| `frontend/localize-stay-frontend/src/test/mocks/handlers.ts` | adicionar handlers de Booking aos handlers existentes |
| `frontend/localize-stay-frontend/.env.example` | documentar `VITE_BOOKING_API_URL` local/mock |

### Arquivos de Referência (não alterar nesta feature)

| Caminho | Motivo |
|---|---|
| `tasks/prd-solicitacao-reserva/api-contract.yaml` | fonte de verdade das operações e schemas de Booking |
| `tasks/prd-solicitacao-reserva/techspec.md` | semântica backend, códigos de erro, ordem de validação |
| `tasks/prd-cadastro-property/frontend-techspec.draft.md` | convenções de estrutura/fetching/testes já decididas para este mesmo frontend |
| `context/architecture-baseline.md` | cliente fino, CORS, ausência de auth |
| `docs/adr/adr-003-frontend-teste-react.md` | React/Vite/TS e chamadas diretas às APIs |

---

## Acessibilidade

- Meta: WCAG 2.1 AA para a jornada entregue.
- Todo input possui `label`; obrigatório/erro não depende apenas de cor.
- Erros usam `aria-invalid` e `aria-describedby`; o resumo recebe foco após submit inválido, 400 ou 422.
- `OperationFeedback` usa `role="status"` para sucesso/loading e `role="alert"` para erro — o cenário
  503 usa `role="status"` (não é erro de formulário, é um estado temporário) com texto que deixa claro
  que não é uma rejeição.
- Ao exibir o resultado (201), mover foco para o título do resumo, sem retirar o controle do teclado.
- Durante loading, anunciar a operação e desabilitar apenas o botão de submit; não bloquear leitura do
  formulário.

Sem biblioteca de i18n. Textos em português, coerentes com PRD e contrato.

---

## Análise de Impacto

| Componente afetado | Tipo | Descrição e risco | Ação |
|---|---|---|---|
| frontend da Fundação | Modificado | segunda feature de negócio; risco de estrutura divergente se implementada antes/depois de Cadastro de Property | inspecionar o estado real antes de codar; adaptar caminhos, sem criar app paralelo |
| navegação/roteamento | Modificado | entrada `/reservations` | registrar pela API pública da feature |
| cliente HTTP/config | Modificado | segunda base URL (Booking, além de Catalog) | estender `env.ts`/`apiClient.ts` sem duplicar o cliente |
| contrato de Booking | Referência | fonte dos tipos e erros; risco de drift | geração + diff no CI |
| CORS de Booking | Dependência backend | browser precisa chamar `:5102` diretamente | confirmar origem explícita conforme baseline/ADR-003 |
| F02 (Consulta de Reserva) | Dependência futura | desbloqueará ver uma Reservation existente/atualizar status após pagamento | não antecipar GET nem persistir snapshot local |

---

## Abordagem de Testes

### Unitários — Vitest

- `reservationFormValidation`: vazio, whitespace, UUID inválido, `checkOut <= checkIn`, `guestsCount <= 0`.
- `reservationErrorMapping`: cada um dos 5 `code` de 422 mapeado ao campo/mensagem correto; 503
  mapeado a tom "falha temporária" (não erro de campo); 400/500/desconhecido mapeados a mensagem
  genérica.
- `reservationApi`: corpo/headers corretos e parsing seguro de `ProblemDetails`, usando MSW em vez de
  mockar `fetch`.
- Cobertura mínima de 70% para statements/branches/functions/lines, alinhada ao mesmo piso definido
  no draft de Catalog; 100% dos ramos contratuais desta feature (os 7 cenários de resposta) devem ter
  cenário de teste.

### Integração — React Testing Library + MSW

- render inicial e navegação por labels/roles;
- validação local não dispara request e move foco ao erro (período/hóspedes inválidos);
- 201 mostra `ReservationResultSummary` com ID, status `solicitada`, preço/moeda/total e nota de que
  o pagamento ainda não foi solicitado;
- 400 exibe mensagem genérica e preserva valores;
- cada um dos 5 cenários 422 associa a mensagem ao campo correto e preserva valores;
- 503 exibe feedback de falha temporária (não de rejeição) e permite reenvio imediato;
- 500 e falha de rede produzem mensagens distintas, preservam valores e permitem retry;
- loading evita duplo submit;
- "Nova solicitação" após 201 limpa o formulário para uma nova jornada.

Testes usam AAA, `userEvent` e queries semânticas. MSW reseta handlers após cada teste; nenhuma
chamada externa real ocorre no Vitest.

### E2E — Playwright

Um cenário crítico solicita uma reserva válida e confirma ID, status `solicitada` e valores
congelados exibidos. Um segundo cenário cobre uma rejeição representativa (ex.: `PERIODO_INVALIDO`)
sem criar Reservation. Rodar contra Booking (+ Catalog real ou stub) quando materializados; até lá, o
E2E fica dependente das fatias backend correspondentes (ver Dependências Técnicas Bloqueantes).

### Gate e Contrato

```bash
npm run lint
npm run type-check
npm run test:coverage
npm run build
npm run api:generate
git diff --exit-code -- src/services/api/generated/booking.ts
npm run test:e2e
```

Os nomes finais dos scripts devem respeitar o `package.json` já existente no worktree (Fundação e/ou
Cadastro de Property). O gate rápido pode executar tudo exceto E2E; o E2E permanece obrigatório no
checkpoint final desta feature.

---

## Sequenciamento de Desenvolvimento

| Fatia | Jornada e artefatos, incluindo testes | Dependências | Checkpoint |
|---|---|---|---|
| EN-01 | geração de tipos de Booking, `VITE_BOOKING_API_URL`, handlers MSW de Booking; necessário para consumir o contrato sem DTO manual | Fundação V-04 (e estrutura intermediária, própria ou herdada de Cadastro de Property) e este contrato aprovado | `npm run api:generate && npm run type-check`; chamadas do adapter validadas com MSW |
| V-01 | rota `/reservations`, formulário completo, validação local, loading, os 7 cenários de resposta (201 + 400 + 5×422 + 503 + 500), resumo de resultado, acessibilidade e testes no mesmo incremento | EN-01; backend F01 V-03 ou Prism/MSW para os cenários cobertos por exemplo | testes focados da feature + `npm run build`; todos os 7 cenários observáveis manualmente ou via MSW |
| V-02 | jornada Playwright (sucesso + uma rejeição), contrato gerado sem drift e gate completo | V-01 e backend completo | gate completo + `npm run test:e2e` |

V-01 não é fatiado por tipo de rejeição porque o PRD declara a feature indivisível — fatiar aqui
recriaria, no frontend, o rollout interno que o PRD proíbe explicitamente no backend.

### Dependências Técnicas Bloqueantes

- Fundação V-04 materializada em `frontend/localize-stay-frontend` com React/Vite/TypeScript.
- Backend F01 V-03 (endpoint completo) para integração real e E2E final; até lá, Prism/MSW cobrem
  apenas desenvolvimento e testes automatizados, não substituem o E2E real.
- CORS de Booking permitindo explicitamente a origem do frontend.
- Se Cadastro de Property ainda não tiver sido implementada, esta feature cria sozinha os artefatos
  compartilhados mínimos (`apiClient.ts`, `env.ts`, `src/test/mocks/*`, `FormErrorSummary`,
  `OperationFeedback`) — sem duplicá-los depois, quando a outra feature for implementada.

---

## Performance

- Sem lazy loading exclusivo para uma página pequena; seguir o padrão do router/app existente.
- Sem prefetch/cache: uma única mutation, sem consulta.
- Sem lista ou imagem volumosa nesta feature.
- Evitar memoização antecipada; formulário com poucos campos.
- Sem orçamento de bundle formal, mesma posição do draft de Catalog para este frontend de laboratório.

---

## Considerações Técnicas

### Decisões Principais

- **Decisão:** Jornada única (`ReservationRequestPage`) sem edição, listagem ou consulta por ID.
  **Racional:** o contrato de F01 só expõe criação; consulta é F02, fora de escopo.
  **Trade-offs:** refresh perde o resultado; não há como reabrir uma Reservation específica por URL.
  **Alternativas rejeitadas:** guardar a última Reservation em `localStorage` para sobreviver ao
  refresh — rejeitada pela mesma disciplina do draft de Catalog (criaria aparência de leitura atual
  sem consultar o backend).

- **Decisão:** `guestReference` viaja no corpo da requisição, não em header (diferente do
  `X-Host-Reference-Id` de Cadastro de Property).
  **Racional:** o contrato desta feature modela `guestReference` como campo de
  `CreateReservationRequest`, não como header — a TechSpec frontend segue o contrato como está, sem
  reintroduzir o padrão de header de outra feature por consistência estética.
  **Trade-offs:** nenhum — são contratos diferentes, decisões independentes.

- **Decisão:** 503 (`CATALOG_INDISPONIVEL`) tratado como estado de falha temporária, com região
  `role="status"`, nunca como erro de validação do formulário.
  **Racional:** AC de RF-01 exige explicitamente que essa falha não seja interpretada como rejeição
  de negócio pelo solicitante.
  **Trade-offs:** exige um componente/estado de feedback com tom diferente do usado para 400/422 —
  aceito porque é a distinção central que a feature deve demonstrar.

- **Decisão:** validação local cobre apenas período e hóspedes; capacidade/disponibilidade/existência
  não são simuladas no cliente.
  **Racional:** essas regras dependem de dados de Catalog que só chegam na resposta do submit; simular
  seria antecipar dado que o frontend não possui e poderia divergir do backend.
  **Trade-offs:** o Guest só descobre capacidade/disponibilidade após o submit — aceitável e já
  previsto pelo próprio design do contrato (nenhum endpoint de consulta prévia é exposto ao
  frontend).

### Riscos e Mitigações

- **Ordem de implementação entre features de frontend:** esta feature e Cadastro de Property podem
  ser implementadas em qualquer ordem. Mitigação: artefatos compartilhados (`apiClient`, `env.ts`,
  `FormErrorSummary`, `OperationFeedback`) são descritos como "criar se ainda não existir" nas duas
  TechSpecs, evitando estrutura paralela quando a segunda for implementada.
- **Confusão entre 503 e 422:** um Guest pode não perceber a diferença entre "corrija seu pedido" e
  "tente de novo mais tarde". Mitigação: tom, região ARIA e texto explicitamente diferentes (ver
  Decisões Principais).
- **Drift entre mock e backend:** mitigação: tipos gerados, Prism, MSW baseado no contrato e E2E real.
- **Ausência de F02:** refresh limpa o resultado. Mitigação: limite documentado nesta TechSpec; F02
  deve evoluir a jornada quando existir.

### Conformidade com Skills

| Decisão | Skill | Conforme? |
|---|---|:---:|
| feature em `features/reservation-request` com `index.ts` público | `react-architecture` | ✅ |
| aliases consistentes e sem imports relativos profundos | `react-architecture` | ✅ |
| Vitest/RTL, AAA, `userEvent`, queries semânticas e MSW | `react-testing` | ✅ |
| formulário com sucesso e erros; jornada crítica em Playwright | `react-testing` | ✅ |
| componentes genéricos reaproveitados entre features (`FormErrorSummary`, `OperationFeedback`) | `react-architecture` | ✅ |

---

## Questões em Aberto

Nenhuma questão bloqueia a aprovação desta especificação. Na implementação, confirmar apenas fatos
que ainda não existem no worktree:

- [ ] caminhos e mecanismo de navegação materializados pela Fundação V-04 (e/ou por Cadastro de
  Property, se implementada primeiro);
- [ ] versões fixadas de Node, React, Vite, Vitest, MSW, Playwright e `openapi-typescript`;
- [ ] nome definitivo da variável de base URL de Booking (`VITE_BOOKING_API_URL` proposto),
  coexistindo com a variável de Catalog em `env.ts`.

Estes itens não alteram o contrato nem a jornada. Se surgir necessidade de consultar uma Reservation
existente antes de F02 estar pronta, isso é mudança de escopo/contrato e deve passar pelo
`tsg-flow-contract-creator` em modo update — não deve ser resolvido com armazenamento local oculto.

---

## Architecture Decision Records

### Herdadas

- [ADR-003: Frontend de teste/visualização — React, sem gateway/BFF na Fase 0](../../docs/adr/adr-003-frontend-teste-react.md)
  — define stack, papel de cliente fino e chamadas diretas às APIs com CORS.

### Criadas nesta sessão

Nenhuma. Jornada única, `fetch`, geração de tipos e estado local são escolhas locais e reversíveis da
implementação desta feature; não alteram o estilo arquitetural já aceito.

---

## Próximos Passos

1. Revisar e aprovar este draft; depois promovê-lo para `frontend-techspec.md` com status `Aprovado`.
2. Materializar a Fundação V-04 (e, se ainda pendente, a estrutura intermediária) antes da
   implementação desta feature.
3. Gerar tipos no frontend:

   ```bash
   npm run api:generate
   ```

4. Subir o mock a partir da raiz quando o backend ainda não estiver disponível:

   ```bash
   npx @stoplight/prism-cli mock tasks/prd-solicitacao-reserva/api-contract.yaml
   ```

5. Após aprovação, encaminhar `prd.md`, `api-contract.yaml`, `techspec.md` e `frontend-techspec.md`
   juntos ao `tsg-flow-task-creator`, reconciliando as tasks backend preexistentes da pasta em vez de
   criar um plano paralelo.
