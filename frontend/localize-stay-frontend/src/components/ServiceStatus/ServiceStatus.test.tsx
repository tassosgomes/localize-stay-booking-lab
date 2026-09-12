// @vitest-environment jsdom
// Ambiente e matchers declarados no próprio arquivo para que o teste passe
// tanto via `npm run test` (vite.config.ts) quanto via `vitest run <filtro>`
// invocado da raiz do repo (gate), onde o config do frontend não é carregado.
import '@testing-library/jest-dom/vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import ServiceStatus from './ServiceStatus.tsx';

const catalogUrl = 'http://catalog.test';
const bookingUrl = 'http://booking.test';
const paymentUrl = 'http://payment.test';

function stubBackendUrls(): void {
  vi.stubEnv('VITE_CATALOG_URL', catalogUrl);
  vi.stubEnv('VITE_BOOKING_URL', bookingUrl);
  vi.stubEnv('VITE_PAYMENT_URL', paymentUrl);
}

const server = setupServer(
  http.get(`${catalogUrl}/health/ready`, () => HttpResponse.text('Healthy')),
  http.get(`${bookingUrl}/health/ready`, () => HttpResponse.text('Healthy')),
  http.get(`${paymentUrl}/health/ready`, () => HttpResponse.text('Healthy')),
);

beforeAll(() => {
  server.listen();
});

afterEach(() => {
  server.resetHandlers();
  cleanup();
  vi.unstubAllEnvs();
});

afterAll(() => {
  server.close();
});

describe('ServiceStatus', () => {
  it('renders Healthy for all three services when every fetch returns 200', async () => {
    stubBackendUrls();
    render(<ServiceStatus />);

    expect(await screen.findByText('Catalog: Healthy')).toBeInTheDocument();
    expect(await screen.findByText('Booking: Healthy')).toBeInTheDocument();
    expect(await screen.findByText('Payment: Healthy')).toBeInTheDocument();
  });

  it('renders the error status when one fetch fails', async () => {
    stubBackendUrls();
    server.use(
      http.get(`${paymentUrl}/health/ready`, () => HttpResponse.text('Degraded', { status: 503 })),
    );
    render(<ServiceStatus />);

    expect(await screen.findByText('Catalog: Healthy')).toBeInTheDocument();
    expect(await screen.findByText('Booking: Healthy')).toBeInTheDocument();
    expect(await screen.findByText('Payment: Unhealthy (HTTP 503)')).toBeInTheDocument();
  });
});
