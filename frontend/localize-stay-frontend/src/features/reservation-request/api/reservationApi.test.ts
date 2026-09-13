// @vitest-environment node
// Teste de adapter HTTP: sem DOM, com fetch/AbortSignal/Request do próprio
// Node (mesma realm do fetch interceptado pelo MSW). O ciclo de vida do
// servidor MSW (listen/resetHandlers/close) é o compartilhado de
// src/test/setup.ts; este arquivo compõe apenas cenários via server.use(...).
import { afterEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import {
  BOOKING_API_BASE_URL,
  RESERVATION_CREATED_FIXTURE,
  RESERVATIONS_URL,
  reservationMalformedHandler,
  reservationRejectedHandler,
  catalogUnavailableHandler,
  reservationInternalErrorHandler,
} from '../../../test/mocks/handlers.ts';
import { server } from '../../../test/mocks/server.ts';
import {
  REJECTION_CODES,
  request,
  type CreateReservationRequest,
} from './reservationApi.ts';

const validInput: CreateReservationRequest = {
  accommodationId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  guestReference: 'guest-marina-alves',
  checkIn: '2026-10-10',
  checkOut: '2026-10-13',
  guestsCount: 2,
};

function stubBookingApiUrl(): void {
  vi.stubEnv('VITE_BOOKING_API_URL', BOOKING_API_BASE_URL);
}

function newSignal(): AbortSignal {
  return new AbortController().signal;
}

// Ciclo de vida do servidor MSW (listen/resetHandlers/close): setup
// compartilhado em src/test/setup.ts. Este arquivo compõe apenas cenários
// via server.use(...) e limpa envs stubbed após cada teste.
afterEach(() => {
  server.resetHandlers();
  vi.unstubAllEnvs();
});

describe('reservationApi.request', () => {
  it('envia POST /v1/reservations com JSON e guestReference no corpo, nunca em header', async () => {
    stubBookingApiUrl();
    let captured: Request | undefined;
    let capturedBody: unknown;
    server.use(
      http.post(RESERVATIONS_URL, async ({ request }) => {
        captured = request;
        capturedBody = await request.json();
        return HttpResponse.json(RESERVATION_CREATED_FIXTURE, { status: 201 });
      }),
    );

    await request(validInput, newSignal());

    expect(captured).toBeDefined();
    const sent = captured as Request;
    expect(sent.method).toBe('POST');
    expect(new URL(sent.url).pathname).toBe('/v1/reservations');
    expect(sent.headers.get('content-type')).toBe('application/json');
    expect(sent.headers.get('guestReference')).toBeNull();
    expect(sent.headers.get('guest-reference')).toBeNull();
    expect(sent.headers.get('x-guest-reference')).toBeNull();
    expect(capturedBody).toEqual(validInput);
  });

  it('retorna success com a Reservation tipada em 201', async () => {
    stubBookingApiUrl();

    const result = await request(validInput, newSignal());

    expect(result).toEqual({
      kind: 'success',
      reservation: RESERVATION_CREATED_FIXTURE,
    });
  });

  it('classifica 400 VALIDATION_ERROR como malformed', async () => {
    stubBookingApiUrl();
    server.use(reservationMalformedHandler());

    expect(await request(validInput, newSignal())).toEqual({ kind: 'malformed', status: 400 });
  });

  it.each([...REJECTION_CODES])('classifica 422 %s como rejected', async (code) => {
    stubBookingApiUrl();
    server.use(reservationRejectedHandler(code));

    expect(await request(validInput, newSignal())).toEqual({
      kind: 'rejected',
      code,
      status: 422,
    });
  });

  it('classifica 503 CATALOG_INDISPONIVEL como unavailable com traceId', async () => {
    stubBookingApiUrl();
    server.use(catalogUnavailableHandler());

    expect(await request(validInput, newSignal())).toEqual({
      kind: 'unavailable',
      status: 503,
      traceId: 'abc123xyz',
    });
  });

  it('classifica 500 INTERNAL_ERROR como failed com traceId', async () => {
    stubBookingApiUrl();
    server.use(reservationInternalErrorHandler());

    expect(await request(validInput, newSignal())).toEqual({
      kind: 'failed',
      status: 500,
      traceId: 'abc123xyz',
    });
  });

  it('retorna failed sem status em falha de rede', async () => {
    stubBookingApiUrl();
    server.use(
      http.post(RESERVATIONS_URL, () => HttpResponse.error()),
    );

    expect(await request(validInput, newSignal())).toEqual({ kind: 'failed' });
  });

  it('retorna failed com status em resposta não contratual (não-JSON)', async () => {
    stubBookingApiUrl();
    server.use(
      http.post(RESERVATIONS_URL, () => new HttpResponse('boom', { status: 418 })),
    );

    expect(await request(validInput, newSignal())).toEqual({ kind: 'failed', status: 418 });
  });

  it('retorna failed quando 422 traz code fora do contrato', async () => {
    stubBookingApiUrl();
    server.use(
      http.post(
        RESERVATIONS_URL,
        () =>
          HttpResponse.json(
            { type: 'about:blank', title: 'Inesperado', status: 422, code: 'CODE_NOVO' },
            { status: 422, headers: { 'Content-Type': 'application/problem+json' } },
          ),
      ),
    );

    expect(await request(validInput, newSignal())).toEqual({ kind: 'failed', status: 422 });
  });

  it('retorna failed quando o corpo de 201 não é JSON parseável', async () => {
    stubBookingApiUrl();
    server.use(
      http.post(
        RESERVATIONS_URL,
        () =>
          new HttpResponse('<html>not json</html>', {
            status: 201,
            headers: { 'Content-Type': 'application/json' },
          }),
      ),
    );

    expect(await request(validInput, newSignal())).toEqual({ kind: 'failed', status: 201 });
  });
});
