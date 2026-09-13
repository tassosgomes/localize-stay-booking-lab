// @vitest-environment node
// Teste do adapter HTTP da Consulta de Reserva: sem DOM, com
// fetch/AbortSignal/Request do próprio Node (mesma realm do fetch interceptado
// pelo MSW). O ciclo de vida do servidor MSW (listen/resetHandlers/close) é o
// compartilhado de src/test/setup.ts; este arquivo compõe apenas cenários via
// server.use(...), nunca mockando `fetch` diretamente.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import {
  BOOKING_API_BASE_URL,
  RESERVATION_DETAIL_BASE_URL,
  RESERVATION_DETAIL_FIXTURES,
  reservationDetailFoundHandler,
  reservationDetailInternalErrorHandler,
  reservationDetailMalformedHandler,
  reservationDetailNotFoundHandler,
} from '../../../test/mocks/handlers.ts';
import { server } from '../../../test/mocks/server.ts';
import { getById } from './reservationDetailApi.ts';

const KNOWN_ID = '8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e';

function stubBookingApiUrl(): void {
  vi.stubEnv('VITE_BOOKING_API_URL', BOOKING_API_BASE_URL);
}

function newSignal(): AbortSignal {
  return new AbortController().signal;
}

afterEach(() => {
  server.resetHandlers();
  vi.unstubAllEnvs();
});

describe('reservationDetailApi.getById', () => {
  it('envia GET /v1/reservations/{reservationId} sem corpo', async () => {
    stubBookingApiUrl();
    let captured: Request | undefined;
    server.use(
      http.get(`${RESERVATION_DETAIL_BASE_URL}/:reservationId`, async ({ request }) => {
        captured = request;
        return HttpResponse.json(RESERVATION_DETAIL_FIXTURES.pendente, { status: 200 });
      }),
    );

    await getById(KNOWN_ID, newSignal());

    expect(captured).toBeDefined();
    const sent = captured as Request;
    expect(sent.method).toBe('GET');
    expect(new URL(sent.url).pathname).toBe(`/v1/reservations/${KNOWN_ID}`);
  });

  it('retorna found com sagaStatus pendente e cancellationReason null (solicitada)', async () => {
    stubBookingApiUrl();
    server.use(reservationDetailFoundHandler('pendente'));

    const result = await getById(KNOWN_ID, newSignal());

    expect(result).toEqual({ kind: 'found', reservation: RESERVATION_DETAIL_FIXTURES.pendente });
    if (result.kind === 'found') {
      expect(result.reservation.status).toBe('solicitada');
      expect(result.reservation.sagaStatus).toBe('pendente');
      expect(result.reservation.cancellationReason).toBeNull();
    }
  });

  it('retorna found com sagaStatus autorizado e cancellationReason null (confirmada)', async () => {
    stubBookingApiUrl();
    server.use(reservationDetailFoundHandler('autorizado'));

    const result = await getById(KNOWN_ID, newSignal());

    expect(result).toEqual({ kind: 'found', reservation: RESERVATION_DETAIL_FIXTURES.autorizado });
    if (result.kind === 'found') {
      expect(result.reservation.status).toBe('confirmada');
      expect(result.reservation.sagaStatus).toBe('autorizado');
      expect(result.reservation.cancellationReason).toBeNull();
    }
  });

  it('retorna found com cancellationReason preenchido (cancelada/rejeitado)', async () => {
    stubBookingApiUrl();
    server.use(reservationDetailFoundHandler('rejeitado'));

    const result = await getById(KNOWN_ID, newSignal());

    expect(result).toEqual({ kind: 'found', reservation: RESERVATION_DETAIL_FIXTURES.rejeitado });
    if (result.kind === 'found') {
      expect(result.reservation.status).toBe('cancelada');
      expect(result.reservation.sagaStatus).toBe('rejeitado');
      expect(result.reservation.cancellationReason).toBe(
        'Pagamento rejeitado pela simulação de Payment.',
      );
    }
  });

  it('classifica 400 VALIDATION_ERROR como malformed', async () => {
    stubBookingApiUrl();
    server.use(reservationDetailMalformedHandler());

    expect(await getById('nao-e-um-uuid', newSignal())).toEqual({ kind: 'malformed' });
  });

  it('classifica 404 RESERVATION_NOT_FOUND como notFound', async () => {
    stubBookingApiUrl();
    server.use(reservationDetailNotFoundHandler());

    expect(await getById(KNOWN_ID, newSignal())).toEqual({ kind: 'notFound' });
  });

  it('classifica 500 INTERNAL_ERROR como failed com traceId', async () => {
    stubBookingApiUrl();
    server.use(reservationDetailInternalErrorHandler());

    expect(await getById(KNOWN_ID, newSignal())).toEqual({
      kind: 'failed',
      status: 500,
      traceId: 'abc123xyz',
    });
  });
});
