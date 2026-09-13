// Handlers MSW compartilhados (Catalog/Booking + fundação).
// União dos dois domínios: health/fundação da irmã (origin/main) + Booking da
// Solicitação de Reserva (tasks/prd-solicitacao-reserva/api-contract.yaml).
// O handler padrão de 201 do Booking fica registrado em `handlers`; os
// cenários de erro são factories compostas por teste via server.use(...), no
// máximo uma por cenário.
import { http, HttpResponse } from 'msw';
import type { components } from '../../services/api/generated/booking.ts';
import type { components as reservationDetailComponents } from '../../services/api/generated/reservationDetail.ts';

export function problemResponse(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

export const BOOKING_API_BASE_URL = 'http://localhost:5102/v1';
export const RESERVATIONS_URL = `${BOOKING_API_BASE_URL}/reservations`;

export type ReservationRejectionCode =
  | 'PERIODO_INVALIDO'
  | 'QUANTIDADE_HOSPEDES_INVALIDA'
  | 'ACOMODACAO_INDISPONIVEL'
  | 'CAPACIDADE_EXCEDIDA'
  | 'PERIODO_INDISPONIVEL';

export const RESERVATION_CREATED_FIXTURE: components['schemas']['Reservation'] = {
  id: '8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e',
  accommodationId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  guestReference: 'guest-marina-alves',
  checkIn: '2026-10-10',
  checkOut: '2026-10-13',
  guestsCount: 2,
  status: 'solicitada',
  pricePerNight: '350.00',
  currency: 'BRL',
  totalAmount: '1050.00',
  createdAt: '2026-09-12T14:22:00Z',
};

type ProblemDetails = components['schemas']['ProblemDetails'];

function reservationProblemResponse(problem: ProblemDetails) {
  return HttpResponse.json(problem, {
    status: problem.status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

// 201 — Reservation criada; congela preço/moeda/total e ecoa os dados da solicitação.
export const reservationCreatedHandler = http.post(RESERVATIONS_URL, async ({ request }) => {
  const input = (await request.json()) as components['schemas']['CreateReservationRequest'];
  const reservation: components['schemas']['Reservation'] = {
    ...RESERVATION_CREATED_FIXTURE,
    accommodationId: input.accommodationId,
    guestReference: input.guestReference,
    checkIn: input.checkIn,
    checkOut: input.checkOut,
    guestsCount: input.guestsCount,
  };
  return HttpResponse.json(reservation, {
    status: 201,
    headers: { Location: `/v1/reservations/${reservation.id}` },
  });
});

// 400 — VALIDATION_ERROR (corpo malformado; não é rejeição de negócio).
export function reservationMalformedHandler() {
  return http.post(
    RESERVATIONS_URL,
    () =>
      reservationProblemResponse({
        type: 'https://localize-stay.lab/problems/validation-error',
        title: 'Requisição inválida',
        status: 400,
        detail: "O campo 'accommodationId' é obrigatório.",
        instance: '/v1/reservations',
        code: 'VALIDATION_ERROR',
      }),
  );
}

const REJECTION_PROBLEMS: Record<ReservationRejectionCode, Omit<ProblemDetails, 'status'>> = {
  PERIODO_INVALIDO: {
    type: 'https://localize-stay.lab/problems/periodo-invalido',
    title: 'Período inválido',
    detail: 'A data de check-out deve ser posterior à data de check-in.',
    instance: '/v1/reservations',
    code: 'PERIODO_INVALIDO',
  },
  QUANTIDADE_HOSPEDES_INVALIDA: {
    type: 'https://localize-stay.lab/problems/quantidade-hospedes-invalida',
    title: 'Quantidade de hóspedes inválida',
    detail: 'O número de hóspedes deve ser maior que zero.',
    instance: '/v1/reservations',
    code: 'QUANTIDADE_HOSPEDES_INVALIDA',
  },
  ACOMODACAO_INDISPONIVEL: {
    type: 'https://localize-stay.lab/problems/acomodacao-indisponivel',
    title: 'A Accommodation informada não existe ou não está ativa.',
    instance: '/v1/reservations',
    code: 'ACOMODACAO_INDISPONIVEL',
  },
  CAPACIDADE_EXCEDIDA: {
    type: 'https://localize-stay.lab/problems/capacidade-excedida',
    title: 'O número de hóspedes solicitado excede a capacidade máxima da Accommodation.',
    instance: '/v1/reservations',
    code: 'CAPACIDADE_EXCEDIDA',
  },
  PERIODO_INDISPONIVEL: {
    type: 'https://localize-stay.lab/problems/periodo-indisponivel',
    title: 'Período indisponível',
    detail: 'A Accommodation não está disponível em todo o período solicitado.',
    instance: '/v1/reservations',
    code: 'PERIODO_INDISPONIVEL',
  },
};

// 422 — rejeição de regra de negócio (RF-01); `code` identifica o motivo exato.
export function reservationRejectedHandler(code: ReservationRejectionCode) {
  return http.post(RESERVATIONS_URL, () =>
    reservationProblemResponse({ ...REJECTION_PROBLEMS[code], status: 422 }),
  );
}

// 503 — CATALOG_INDISPONIVEL: falha temporária, nunca rejeição.
export function catalogUnavailableHandler(traceId: string = 'abc123xyz') {
  return http.post(
    RESERVATIONS_URL,
    () =>
      reservationProblemResponse({
        type: 'https://localize-stay.lab/problems/catalog-indisponivel',
        title: 'Não foi possível validar a solicitação no momento',
        status: 503,
        detail: 'A consulta síncrona a Catalog falhou. Tente novamente em instantes.',
        instance: '/v1/reservations',
        code: 'CATALOG_INDISPONIVEL',
        traceId,
      }),
  );
}

// 500 — INTERNAL_ERROR, com traceId para diagnóstico.
export function reservationInternalErrorHandler(traceId: string = 'abc123xyz') {
  return http.post(
    RESERVATIONS_URL,
    () =>
      reservationProblemResponse({
        type: 'about:blank',
        title: 'Erro interno',
        status: 500,
        detail: 'Ocorreu um erro interno. Tente novamente mais tarde.',
        instance: '/v1/reservations',
        code: 'INTERNAL_ERROR',
        traceId,
      }),
  );
}

export const handlers = [
  http.get(/\/health\/ready$/, () => HttpResponse.text('Healthy')),
  reservationCreatedHandler,
];

// ─── Booking F02 — Consulta de Reserva (tasks/prd-consulta-reserva) ─────────
// Fixtures espelham os três exemplos de 200 do contrato
// (`pagamentoPendente`, `confirmadaPagamentoAutorizado`,
// `canceladaPagamentoRejeitado`) para que Prism (dev) e MSW (testes) fiquem
// consistentes. Cenários de erro são factories compostas por teste via
// server.use(...), no mesmo padrão dos handlers de F01 acima.
type ReservationDetail = reservationDetailComponents['schemas']['ReservationDetail'];
type ReservationDetailProblem = reservationDetailComponents['schemas']['ProblemDetails'];

export const RESERVATION_DETAIL_BASE_URL = `${BOOKING_API_BASE_URL}/reservations`;
const RESERVATION_DETAIL_URL = `${BOOKING_API_BASE_URL}/reservations/:reservationId`;

const RESERVATION_DETAIL_BASE = {
  id: '8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e',
  accommodationId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  guestReference: 'guest-marina-alves',
  checkIn: '2026-10-10',
  checkOut: '2026-10-13',
  guestsCount: 2,
  pricePerNight: '350.00',
  currency: 'BRL',
  totalAmount: '1050.00',
  createdAt: '2026-09-12T14:22:00Z',
  correlationId: 'b2c4e6a8-1234-4abc-9def-0123456789ab',
} as const;

export const RESERVATION_DETAIL_FIXTURES = {
  pendente: {
    ...RESERVATION_DETAIL_BASE,
    status: 'solicitada',
    sagaStatus: 'pendente',
    cancellationReason: null,
  },
  autorizado: {
    ...RESERVATION_DETAIL_BASE,
    status: 'confirmada',
    sagaStatus: 'autorizado',
    cancellationReason: null,
  },
  rejeitado: {
    ...RESERVATION_DETAIL_BASE,
    status: 'cancelada',
    sagaStatus: 'rejeitado',
    cancellationReason: 'Pagamento rejeitado pela simulação de Payment.',
  },
} as const satisfies Record<string, ReservationDetail>;

export type ReservationDetailFixture = keyof typeof RESERVATION_DETAIL_FIXTURES;

function reservationDetailProblemResponse(problem: ReservationDetailProblem) {
  return HttpResponse.json(problem, {
    status: problem.status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

// 200 — Reservation encontrada; `fixture` seleciona a combinação
// status/sagaStatus (pendente/autorizado/rejeitado).
export function reservationDetailFoundHandler(fixture: ReservationDetailFixture) {
  const detail: ReservationDetail = { ...RESERVATION_DETAIL_FIXTURES[fixture] };
  return http.get(RESERVATION_DETAIL_URL, () => HttpResponse.json(detail, { status: 200 }));
}

// 400 — VALIDATION_ERROR (identificador fora do formato UUID esperado).
export function reservationDetailMalformedHandler() {
  return http.get(
    RESERVATION_DETAIL_URL,
    () =>
      reservationDetailProblemResponse({
        type: 'https://localize-stay.lab/problems/validation-error',
        title: 'Requisição inválida',
        status: 400,
        detail: 'O identificador informado não está em um formato válido.',
        instance: '/v1/reservations/nao-e-um-uuid',
        code: 'VALIDATION_ERROR',
      }),
  );
}

// 404 — RESERVATION_NOT_FOUND (identificador bem formado, sem registro).
export function reservationDetailNotFoundHandler() {
  return http.get(
    RESERVATION_DETAIL_URL,
    () =>
      reservationDetailProblemResponse({
        type: 'https://localize-stay.lab/problems/reservation-not-found',
        title: 'Reservation não encontrada',
        status: 404,
        detail: 'Nenhuma Reservation existe com o identificador informado.',
        instance: '/v1/reservations/8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e',
        code: 'RESERVATION_NOT_FOUND',
      }),
  );
}

// 500 — INTERNAL_ERROR, com traceId para diagnóstico.
export function reservationDetailInternalErrorHandler(traceId: string = 'abc123xyz') {
  return http.get(
    RESERVATION_DETAIL_URL,
    () =>
      reservationDetailProblemResponse({
        type: 'about:blank',
        title: 'Erro interno',
        status: 500,
        detail: 'Ocorreu um erro interno. Tente novamente mais tarde.',
        instance: '/v1/reservations/8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e',
        code: 'INTERNAL_ERROR',
        traceId,
      }),
  );
}
