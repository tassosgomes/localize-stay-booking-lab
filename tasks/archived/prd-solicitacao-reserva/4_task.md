---
status: done
slice_type: enabling
verification_type: behavioral
parallelizable: true
blocked_by: []
---

<task_context>
<domain>frontend/reservation-request/integration</domain>
<type>integration</type>
<scope>configuration</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"5.0, 6.0"</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="reservationApi.test"` gera/valida os tipos de Booking e executa os testes MSW do adapter, provando body/headers do `POST /v1/reservations` e o parsing de `ProblemDetails` (400/422×5/503/500) sem rede real</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="reservationApi.test"</gate_command>
<gate_test_selector>Arquivo `frontend/localize-stay-frontend/src/features/reservation-request/api/reservationApi.test.ts`</gate_test_selector>
<gate_expected_result>Filtro encontra testes; todos passam; request usa `POST /v1/reservations` com JSON e `guestReference` no corpo (nunca header); os 7 cenários de resposta do contrato (201, 400, 5×422, 503, 500) são reconhecidos e retornam um resultado tipado seguro; type-check e build permanecem verdes; `npm run api:generate` seguido de `git diff --exit-code -- src/services/api/generated/booking.ts` não altera o arquivo gerado</gate_expected_result>
<static_evidence>N/A — enabling behavioral validado pelo adapter HTTP com MSW</static_evidence>
<vertical_slice>N/A — enabling</vertical_slice>
</task_context>

# Tarefa 4.0: Preparar integração frontend tipada com o contrato de Booking (EN-FE-01)

## Relacionada às User Stories

- "Como frontend de teste, eu quero enviar a solicitação de reserva e exibir o resultado..."
  (suporte contratual — este adapter é o único ponto de contato HTTP da feature com Booking)
- "Como Guest, eu quero ser informado imediatamente quando meu pedido não pode ser aceito..."
  (suporte — o parsing uniforme de `ProblemDetails` é o que a UI (5.0) consome para decidir tom e
  campo do erro)

## Visão Geral

Estabelece o contrato compartilhado do frontend para Booking F01: tipos gerados de
`api-contract.yaml`, a URL dedicada `VITE_BOOKING_API_URL` (distinta da URL de Catalog), o adapter
`reservationApi.ts` (`POST /v1/reservations` tipado + parser seguro de `ProblemDetails`) e a
infraestrutura MSW (handlers para os 7 cenários de resposta do contrato). É habilitador porque tanto
a fatia de UI (5.0) quanto o E2E/drift-check (6.0) dependem exatamente destes mesmos artefatos —
mantê-los numa única task evita um segundo parser de erro ou um segundo conjunto de mocks divergente.
Reaproveita `apiClient.ts`/`env.ts`/`src/test/mocks/*` se `prd-cadastro-property` já os tiver criado;
cria a versão mínima genérica apenas se ainda não existirem (ordem entre as duas features de frontend
é livre — ver `frontend-techspec.md` §Riscos e Mitigações).

## Entrega Observável

- **Entrada ou gatilho:** chamadas `reservationApi.request(input, signal)` em testes MSW, para cada
  um dos 7 cenários de resposta do contrato.
- **Resultado esperado:** o request usa `POST /v1/reservations`, JSON, `guestReference` no corpo (não
  em header); 201 retorna a `Reservation` tipada; 400/422×5/503/500 retornam um resultado de erro
  normalizado com `code` (quando presente) e, em 503/500, `traceId`; resposta não contratual ou falha
  de rede retornam um resultado de erro genérico seguro, sem lançar exceção não tratada.
- **Checkpoint de feedback:** `gate.sh --filter="reservationApi.test"` + `npm run api:generate` sem
  diff em `booking.ts` + `npm run type-check`.
- **Seletor focalizado:** `reservationApi.test.ts`.
- **Fora deste checkpoint:** formulário, navegação visual, validação local de campos e backend real
  (isso é 5.0 e 6.0).

## Requisitos

- Fixar `openapi-typescript` (e demais dependências de teste/E2E, se ainda não fixadas por
  `prd-cadastro-property`) no `package.json`/`package-lock.json`, sem upgrades amplos de outras
  dependências.
- Gerar `src/services/api/generated/booking.ts` somente a partir de
  `tasks/prd-solicitacao-reserva/api-contract.yaml`; proibir edição manual do arquivo gerado; incluir
  script `api:generate` (adicionar Booking ao script existente, se `prd-cadastro-property` já o criou
  para Catalog, em vez de criar um segundo script).
- `reservationApi.request(input, signal)` envia `POST /v1/reservations` usando `fetch` via
  `apiClient.ts` (reaproveitado/estendido, não duplicado); usa tipos derivados de `paths`/`components`
  do arquivo gerado, sem DTOs manuais paralelos.
- Base URL de Booking via `VITE_BOOKING_API_URL` em `env.ts`, validada e distinta da variável de
  Catalog (os dois serviços rodam em portas diferentes — Booking `:5102`, Catalog `:5101`, conforme
  `techspec.md` §Considerações Técnicas); `apiClient.ts` deve suportar múltiplas base URLs por
  serviço se ainda não suportar.
- Parsear `ProblemDetails` defensivamente (`code`, `traceId` quando presentes), com fallback seguro
  para corpo malformado, resposta não-JSON ou falha de rede — nunca lançar exceção não tratada para o
  chamador.
- Cada chamada aceita um `AbortSignal` (a task 5.0 é quem cria o `AbortController` por submit; este
  adapter apenas o repassa ao `fetch`).
- Sem retry automático, cache ou invalidação — uma única mutation sem idempotência garantida pelo
  contrato.
- Criar/estender `src/test/mocks/{handlers,server}.ts` e `src/test/setup.ts` com handlers MSW para os
  7 cenários de resposta de Booking, adicionados aos handlers existentes (não substituindo os de
  Catalog, se já existirem); reset de handlers após cada teste; nenhuma chamada de rede real no
  Vitest.
- Documentar `VITE_BOOKING_API_URL` em `.env.example`.

## Arquivos Envolvidos

- **Criar:**
  - `frontend/localize-stay-frontend/src/features/reservation-request/api/reservationApi.ts`
  - `frontend/localize-stay-frontend/src/features/reservation-request/api/reservationApi.test.ts`
  - `frontend/localize-stay-frontend/src/services/api/generated/booking.ts` (gerado; não editar
    manualmente)
  - `frontend/localize-stay-frontend/src/test/mocks/handlers.ts` (criar apenas se
    `prd-cadastro-property` não o tiver criado; caso contrário, estender com os handlers de Booking)
  - `frontend/localize-stay-frontend/src/test/mocks/server.ts` (criar se ainda não existir)
  - `frontend/localize-stay-frontend/src/test/setup.ts` (criar se ainda não existir)
- **Modificar:**
  - `frontend/localize-stay-frontend/package.json` e `package-lock.json` (script `api:generate`
    passa a incluir Booking; dependências de dev fixadas)
  - `frontend/localize-stay-frontend/src/config/env.ts` (expor e validar `VITE_BOOKING_API_URL`)
  - `frontend/localize-stay-frontend/src/services/apiClient.ts` (estender para múltiplas base URLs,
    se necessário; reaproveitar se já suportar)
  - `frontend/localize-stay-frontend/.env.example` (documentar `VITE_BOOKING_API_URL`)
- **Referência:**
  - `tasks/prd-solicitacao-reserva/{api-contract.yaml,frontend-techspec.md,techspec.md}` — fonte de
    schemas, `code` de erro e portas de serviço
  - `tasks/prd-cadastro-property/3_task.md` — mesmo padrão já aplicado a `propertyApi.ts`/`catalog.ts`
    neste mesmo frontend; reaproveitar convenções, não duplicar estrutura
  - `docs/adr/adr-003-frontend-teste-react.md` — cliente fino, CORS, ausência de auth
- **Skills para consultar durante implementação:**
  - `react-architecture` — aliases e fronteira da feature (`api/` dentro de `features/reservation-request`)
  - `react-testing` — MSW, isolamento, `AAA`, sem mock direto de `fetch`

## Subtarefas

- [ ] 4.1 Fixar dependências (`openapi-typescript` e demais, se ainda não fixadas) e adicionar/estender
      o script `api:generate` para Booking
- [ ] 4.2 Gerar `booking.ts` e adicionar verificação determinística de drift
      (`git diff --exit-code -- src/services/api/generated/booking.ts`)
- [ ] 4.3 Estender `env.ts`/`apiClient.ts` para `VITE_BOOKING_API_URL` e implementar
      `reservationApi.request` com `AbortSignal` e parsing seguro de `ProblemDetails`
- [ ] 4.4 Criar/estender handlers MSW para os 7 cenários de resposta de Booking (201, 400, 5×422, 503,
      500) e `server.ts`/`setup.ts` compartilhados
- [ ] 4.5 Escrever `reservationApi.test.ts` cobrindo os 7 cenários e executar o gate, confirmando
      seleção de teste sem chamadas externas

## Sequenciamento

- Bloqueado por: Nenhuma (depende apenas da Fundação V-04 externa e do contrato já aprovado)
- Desbloqueia: 5.0, 6.0
- Paralelizável: Sim — nenhum arquivo compartilhado com 1.0, 2.0 ou 3.0 (backend); pode avançar antes,
  durante ou depois delas

## Rastreabilidade

- Esta tarefa cobre: EN-01 da `frontend-techspec.md` e os artefatos de transporte/mocks de seu
  inventário (`booking.ts`, `reservationApi.ts`, handlers MSW).
- Evidência esperada: `reservationApi.test.ts` verde para os 7 cenários; ausência de drift entre
  `api-contract.yaml` e `booking.ts`.

## Detalhes de Implementação

`reservationApi.request(input, signal)` — assinatura e comportamento (de `frontend-techspec.md`
§Estratégia de Fetching e §Tratamento Centralizado de Erros):

```ts
type ReservationApiResult =
  | { kind: "success"; reservation: Reservation }
  | { kind: "rejected"; code: RejectionCode; status: 422 }
  | { kind: "unavailable"; status: 503; traceId?: string }
  | { kind: "malformed"; status: 400 }
  | { kind: "failed"; status?: number; traceId?: string };

async function request(
  input: CreateReservationRequest,
  signal: AbortSignal,
): Promise<ReservationApiResult> { /* ... */ }
```

Tabela de mapeamento HTTP/`code` → resultado (autoridade: `frontend-techspec.md`
§Tratamento Centralizado de Erros — a task 5.0 é quem decide texto/campo/tom exibidos, este adapter só
classifica a resposta):

| HTTP / `code` | Resultado do adapter |
|---|---|
| `201` | `{ kind: "success", reservation }` |
| `400 / VALIDATION_ERROR` | `{ kind: "malformed", status: 400 }` |
| `422 / PERIODO_INVALIDO` \| `QUANTIDADE_HOSPEDES_INVALIDA` \| `ACOMODACAO_INDISPONIVEL` \| `CAPACIDADE_EXCEDIDA` \| `PERIODO_INDISPONIVEL` | `{ kind: "rejected", code, status: 422 }` |
| `503 / CATALOG_INDISPONIVEL` | `{ kind: "unavailable", status: 503, traceId? }` |
| `500 / INTERNAL_ERROR` \| resposta não contratual \| falha de rede | `{ kind: "failed", status?, traceId? }` |

O form state (task 5.0) é local, separado destes tipos de transporte — este adapter nunca importa
tipos de formulário, e a UI nunca importa `paths`/`components` gerados diretamente, só através deste
adapter.

**Convenções da stack:**
- `fetch` nativo via `apiClient.ts`, sem biblioteca de fetching/cache (`react-architecture`).
- MSW no processo do Vitest, reset por teste, no máximo três mocks por teste, sem mock direto de
  `fetch` (`react-testing`).
- API pública da feature (`index.ts` exportando só `ReservationRequestPage`) é criada na 5.0, não
  aqui — este adapter fica em `api/`, não exportado no nível da feature ainda.

## Prontidão para Implementação

- **Decisões fechadas:** `fetch` nativo, `openapi-typescript`, MSW, tipos gerados sem edição manual,
  sem retry/cache, `guestReference` no corpo (não em header — diferente do `X-Host-Reference-Id` de
  `prd-cadastro-property`), `VITE_BOOKING_API_URL` distinta de Catalog.
- **Limites de decisão do implementer:** shape exato do tipo de resultado do adapter (`kind`
  discriminado sugerido acima é orientativo, não normativo); nome exato do script de type-check/build
  deve seguir o `package.json` real do worktree.
- **Dependências disponíveis:** `api-contract.yaml` aprovado; Fundação V-04 (app Vite/React
  materializado); `apiClient.ts`/`env.ts`/mocks de `prd-cadastro-property`, se já implementada.
- **Artefatos exigidos pelo gate:** `reservationApi.test.ts`, `booking.ts` gerado e handlers MSW são
  criados/gerados nesta própria task.
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `scripts/ai-flow/gate.sh --filter="reservationApi.test"`
- [ ] O seletor encontra o teste e não executa componentes de UI
- [ ] Build compila sem erros e type-check passa
- [ ] `npm run api:generate` seguido de `git diff --exit-code -- src/services/api/generated/booking.ts`
      não produz diferença
- [ ] `POST /v1/reservations` é enviado com rota, header e body exatos (`guestReference` no corpo)
- [ ] Os 7 cenários de resposta do contrato (201, 400, 5×422, 503, 500) e falha de rede/parse produzem
      um resultado tipado seguro, sem exceção não tratada
- [ ] Vitest não acessa rede real e os handlers MSW são resetados entre testes
- [ ] Checkpoint de feedback executado: `gate.sh --filter="reservationApi.test"` → verde
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente o adapter/tipos/mocks, sem depender de 5.0 ou 6.0
