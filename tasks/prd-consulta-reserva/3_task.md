---
status: pending
slice_type: enabling
verification_type: behavioral
parallelizable: true
blocked_by: []
---

<task_context>
<domain>frontend/reservation-lookup/integration</domain>
<type>integration</type>
<scope>configuration</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"4.0, 5.0"</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="reservationDetailApi.test"` gera os tipos do contrato desta feature e executa os testes MSW do adapter, provando a classificação dos 6 cenários (`200`×3, 400, 404, 500) sem rede real</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="reservationDetailApi.test"</gate_command>
<gate_test_selector>Arquivo `frontend/localize-stay-frontend/src/features/reservation-lookup/api/reservationDetailApi.test.ts`</gate_test_selector>
<gate_expected_result>Filtro encontra testes; todos passam; `reservationDetailApi.getById` envia `GET /v1/reservations/{reservationId}` e classifica os 6 cenários (200 solicitada/pendente, 200 confirmada/autorizado, 200 cancelada/rejeitado+motivo, 400, 404, 500) em um resultado tipado seguro; type-check e build permanecem verdes; `npm run api:generate` seguido de `git diff --exit-code -- src/services/api/generated/reservationDetail.ts` não altera o arquivo gerado</gate_expected_result>
<static_evidence>N/A — enabling behavioral validado pelo adapter HTTP com MSW</static_evidence>
<vertical_slice>N/A — enabling</vertical_slice>
</task_context>

# Tarefa 3.0: Preparar integração frontend tipada com o contrato desta feature (EN-01 frontend)

## Relacionada às User Stories

- "Como frontend de teste, eu quero buscar uma Reservation por identificador e exibir seus dados e o
  estado da saga, para permitir verificação manual do fluxo completo sem acesso direto ao banco."
  (suporte — esta task só prepara o adapter tipado; a UI é a task 4.0)

## Visão Geral

O contrato desta feature (`GET /reservations/{reservationId}`) é um segundo contrato do mesmo serviço
Booking, distinto do já consumido em `booking.ts` (gerado a partir de
`tasks/prd-solicitacao-reserva/api-contract.yaml`). Em vez de fundir os dois YAMLs ou renomear o
arquivo já consumido por `reservation-request` (o que forçaria reabrir aquela feature já entregue),
esta task gera um segundo arquivo de tipos (`reservationDetail.ts`) a partir do contrato próprio desta
feature — mesma convenção de "um contrato por pasta de PRD, um arquivo gerado por contrato" já em uso.
Cria também o único ponto de contato do frontend com F02 (`reservationDetailApi.ts`) e os handlers MSW
dos 6 cenários de resposta, que tanto a fatia de UI (4.0) quanto o E2E/drift-check (5.0) vão consumir
sem duplicar parsing de `ProblemDetails` nem mocks divergentes.

## Entrega Observável

- **Entrada ou gatilho:** `npm run api:generate`; chamadas a `reservationDetailApi.getById(id, signal)`
  interceptadas por MSW nos 6 cenários de resposta do contrato.
- **Resultado esperado:** `reservationDetail.ts` gerado sem erro de type-check; o adapter classifica
  cada resposta MSW no resultado tipado correto (`found`/`notFound`/`malformed`/`failed`), incluindo
  `cancellationReason: null` vs. preenchido.
- **Checkpoint de feedback:** `scripts/ai-flow/gate.sh --filter="reservationDetailApi.test"` verde.
- **Seletor focalizado:** `reservationDetailApi.test.ts`
- **Fora deste checkpoint:** nenhum componente de UI, formulário ou rota (isso é a task 4.0); nenhuma
  chamada contra o backend real (isso é a task 5.0).

## Requisitos

- **Geração de tipos:** `openapi-typescript ../../tasks/prd-consulta-reserva/api-contract.yaml -o
  src/services/api/generated/reservationDetail.ts`; `package.json` (`api:generate`) ganha uma terceira
  chamada encadeada às duas já existentes (Booking F01 + Catalog).
- **Adapter (`reservationDetailApi.ts`):** `getById(reservationId, signal)` envia
  `GET /reservations/{reservationId}` via `apiRequest({ baseUrl: getBookingApiUrl(), path, method:
  'GET', signal })` (`apiClient.ts`/`env.ts` reaproveitados sem alteração) e classifica a resposta:
  - `200` → `{ kind: 'found', reservation: ReservationDetail }`
  - `400` com `code=VALIDATION_ERROR` → `{ kind: 'malformed' }`
  - `404` com `code=RESERVATION_NOT_FOUND` → `{ kind: 'notFound' }`
  - qualquer outro status, corpo não contratual ou falha de rede → `{ kind: 'failed', status?,
    traceId? }` — `AbortError` propaga para o chamador decidir (mesmo princípio de
    `reservationApi.ts` de F01: nunca lança exceção não tratada).
- **Handlers MSW:** adicionar a `src/test/mocks/handlers.ts` os 6 cenários desta feature (200×3
  combinações de `status`/`sagaStatus`, 400, 404, 500), como factories compostas por teste
  (`server.use(...)`), no mesmo padrão de `reservationRejectedHandler`/`catalogUnavailableHandler` já
  usados por `reservation-request`.
- **Testes do adapter:** `reservationDetailApi.test.ts` cobre os 6 cenários via MSW (nunca mockando
  `fetch` diretamente), incluindo `cancellationReason: null` vs. preenchido.
- **Check de drift:** `npm run api:generate && git diff --exit-code -- src/services/api/generated/reservationDetail.ts`
  não produz diferença.

## Arquivos Envolvidos

- **Criar:**
  - `frontend/localize-stay-frontend/src/services/api/generated/reservationDetail.ts` (gerado via
    `openapi-typescript`; não editar à mão)
  - `frontend/localize-stay-frontend/src/features/reservation-lookup/api/reservationDetailApi.ts`
  - `frontend/localize-stay-frontend/src/features/reservation-lookup/api/reservationDetailApi.test.ts`
- **Modificar:**
  - `frontend/localize-stay-frontend/package.json` (`api:generate` ganha a terceira chamada, contrato
    desta feature → `reservationDetail.ts`)
  - `frontend/localize-stay-frontend/src/test/mocks/handlers.ts` (adiciona os 6 handlers desta
    feature aos handlers existentes de Catalog/Booking F01)
- **Referência:**
  - `frontend/localize-stay-frontend/src/features/reservation-request/api/reservationApi.ts` —
    padrão de adapter HTTP com resultado em união discriminada a seguir
  - `frontend/localize-stay-frontend/src/config/env.ts` (`getBookingApiUrl()`),
    `frontend/localize-stay-frontend/src/services/apiClient.ts` (`apiRequest`) — reaproveitados sem
    alteração
  - `tasks/prd-consulta-reserva/api-contract.yaml` — schemas `ReservationDetail`,
    `ReservationDetailResponse`, `ProblemDetails`; operação `getReservationById`
  - `frontend-techspec.md` (§Geração de Tipos, §Estratégia de Fetching, §Tratamento Centralizado de
    Erros) — comandos e mapeamento `(status, code)` normativos
- **Skills para consultar durante implementação:**
  - `react-architecture` — adapter HTTP isolado em `api/`, sem lógica de UI
  - `react-testing` — MSW no processo do Vitest, sem mockar `fetch` diretamente

## Subtarefas

- [ ] 3.1 Adicionar a terceira chamada de `openapi-typescript` a `api:generate` e gerar
      `reservationDetail.ts`; confirmar `npm run type-check` verde
- [ ] 3.2 Implementar `reservationDetailApi.getById(reservationId, signal)` com a classificação
      `found`/`malformed`/`notFound`/`failed` por `(status, code)`
- [ ] 3.3 Adicionar a `src/test/mocks/handlers.ts` os 6 handlers desta feature (200×3, 400, 404, 500)
      como factories reutilizáveis por teste
- [ ] 3.4 Escrever `reservationDetailApi.test.ts` cobrindo os 6 cenários via MSW, incluindo
      `cancellationReason` nulo vs. preenchido

## Sequenciamento

- Bloqueado por: Nenhuma (contrato desta feature já aprovado; estrutura intermediária do frontend já
  em `main`)
- Desbloqueia: 4.0 (consome `reservationDetailApi`/tipos/handlers MSW), 5.0 (E2E/drift-check)
- Paralelizável: Sim, com 1.0/2.0 (backend) — nenhum arquivo compartilhado

## Rastreabilidade

- Esta tarefa cobre: suporte à User Story do "frontend de teste" (integração tipada, sem UI ainda).
- Evidência esperada: `reservationDetailApi.test.ts` verde cobrindo os 6 cenários; `api:generate` sem
  drift.

## Detalhes de Implementação

```ts
// features/reservation-lookup/api/reservationDetailApi.ts
import { getBookingApiUrl } from '../../../config/env.ts';
import { apiRequest } from '../../../services/apiClient.ts';
import type { components } from '../../../services/api/generated/reservationDetail.ts';

export type ReservationDetail = components['schemas']['ReservationDetail'];
export type ProblemDetails = components['schemas']['ProblemDetails'];

export type ReservationDetailApiResult =
  | { kind: 'found'; reservation: ReservationDetail }
  | { kind: 'notFound' }
  | { kind: 'malformed' }
  | { kind: 'failed'; status?: number; traceId?: string };

export async function getById(
  reservationId: string,
  signal: AbortSignal,
): Promise<ReservationDetailApiResult> {
  try {
    const response = await apiRequest({
      baseUrl: getBookingApiUrl(),
      path: `/reservations/${reservationId}`,
      method: 'GET',
      signal,
    });

    if (response.status === 200) {
      try {
        return { kind: 'found', reservation: (await response.json()) as ReservationDetail };
      } catch {
        return { kind: 'failed', status: response.status };
      }
    }

    const problem = await parseProblemDetails(response); // mesmo padrão de reservationApi.ts
    if (response.status === 400 && problem.code === 'VALIDATION_ERROR') {
      return { kind: 'malformed' };
    }
    if (response.status === 404 && problem.code === 'RESERVATION_NOT_FOUND') {
      return { kind: 'notFound' };
    }
    return { kind: 'failed', status: response.status, traceId: problem.traceId ?? undefined };
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }
    return { kind: 'failed' };
  }
}

export const reservationDetailApi = { getById };
```

`parseProblemDetails` segue exatamente o padrão já existente em `reservation-request/api/reservationApi.ts`
(try/catch em torno de `response.json()`, retorna `{}` em corpo não parseável) — reaproveitar a mesma
lógica, não uma segunda implementação divergente.

Handlers MSW (`src/test/mocks/handlers.ts`), mesmo padrão de `reservationRejectedHandler`:

```ts
export const RESERVATION_DETAIL_BASE_URL = `${BOOKING_API_BASE_URL}/reservations`;

export const RESERVATION_DETAIL_FIXTURES = {
  pendente: { /* status: 'solicitada', sagaStatus: 'pendente', cancellationReason: null, ... */ },
  autorizado: { /* status: 'confirmada', sagaStatus: 'autorizado', cancellationReason: null, ... */ },
  rejeitado: { /* status: 'cancelada', sagaStatus: 'rejeitado', cancellationReason: '...', ... */ },
} as const;

export function reservationDetailFoundHandler(fixture: keyof typeof RESERVATION_DETAIL_FIXTURES) { /* ... */ }
export function reservationDetailMalformedHandler() { /* ... 400 VALIDATION_ERROR ... */ }
export function reservationDetailNotFoundHandler() { /* ... 404 RESERVATION_NOT_FOUND ... */ }
export function reservationDetailInternalErrorHandler(traceId?: string) { /* ... 500 INTERNAL_ERROR ... */ }
```

Os valores de exemplo (`pricePerNight`, `correlationId`, etc.) devem espelhar os três exemplos já
presentes em `api-contract.yaml` (`pagamentoPendente`, `confirmadaPagamentoAutorizado`,
`canceladaPagamentoRejeitado`) para que Prism (dev) e MSW (testes) fiquem consistentes.

**Convenções da stack:**
- Nenhuma exceção não tratada escapa do adapter (mesmo princípio de `reservationApi.ts`); `AbortError`
  propaga para o chamador.
- `RESERVATIONS_PATH`/`path` tipado com `satisfies keyof paths` quando o schema gerado permitir,
  mesma convenção de F01.

## Prontidão para Implementação

- **Decisões fechadas:** nome do arquivo gerado (`reservationDetail.ts`, não `booking.ts` — decisão já
  confirmada na TechSpec frontend); classificação por `(status, code)`, nunca por `title`/`detail`;
  `reservationDetailApi` não faz retry nem cache.
- **Limites de decisão do implementer:** nomes exatos dos helpers de fixture/handler MSW; organização
  interna do arquivo de handlers (uma função por cenário, como já é o padrão).
- **Dependências disponíveis:** `api-contract.yaml` desta feature (aprovado); `apiClient.ts`,
  `env.ts` (`getBookingApiUrl()`), `src/test/mocks/handlers.ts`/`server.ts` já existentes.
- **Artefatos exigidos pelo gate:** `reservationDetailApi.test.ts` é criado nesta própria task;
  `reservationDetail.ts` é gerado nesta própria task (não preexiste).
- **Dependências futuras:** Nenhuma — as tasks 4.0/5.0 consomem o adapter/tipos/handlers já prontos e
  testados por esta task.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `npm run test -- reservationDetailApi.test` (via
      `scripts/ai-flow/gate.sh --filter="reservationDetailApi.test"`)
- [ ] O seletor encontra pelo menos um teste e não executa casos sem relação com esta task
- [ ] Build compila sem erros: `npm run build` (inclui `tsc --noEmit`)
- [ ] `npm run type-check` verde após `npm run api:generate`
- [ ] `reservationDetailApi.getById` classifica corretamente os 6 cenários MSW: 200×3 (`found` com os
      campos certos, incluindo `cancellationReason` nulo/preenchido), 400 (`malformed`), 404
      (`notFound`), 500 (`failed` com `traceId`)
- [ ] `npm run api:generate && git diff --exit-code -- src/services/api/generated/reservationDetail.ts`
      não produz diferença
- [ ] Checkpoint de feedback executado conforme descrito acima
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente a integração tipada, não a UI (task 4.0) nem o E2E (task 5.0)
