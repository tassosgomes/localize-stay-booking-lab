// @vitest-environment jsdom
// Teste de integração RTL + MSW da jornada de Consulta de Reserva (task
// 4.0 / V-01): prova os 5 cenários do AC de RF-01 (200×3 + 400 + 404) mais
// 500/falha de rede no mesmo formulário de um campo, com validação local,
// loading, ReservationDetailView completo e foco acessível. O ciclo de vida
// do servidor MSW (listen/resetHandlers/close) é o compartilhado de
// src/test/setup.ts; este arquivo compõe apenas cenários via server.use(...).
import '@testing-library/jest-dom/vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
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
import { reservationDetailApi } from '../api/reservationDetailApi.ts';
import ReservationLookupPage from './ReservationLookupPage.tsx';

const ID_PENDENTE = '8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e';
const ID_AUTORIZADO = '11111111-2222-4333-8444-555555555555';
const CORRELATION_ID = 'b2c4e6a8-1234-4abc-9def-0123456789ab';
const CANCELLATION_REASON = 'Pagamento rejeitado pela simulação de Payment.';

function stubBookingApiUrl(): void {
  vi.stubEnv('VITE_BOOKING_API_URL', BOOKING_API_BASE_URL);
}

type User = ReturnType<typeof userEvent.setup>;

function field(): HTMLInputElement {
  return screen.getByLabelText('Identificador da reserva');
}

function searchButton(): HTMLButtonElement {
  return screen.getByRole('button', { name: 'Buscar reserva' });
}

async function search(user: User, reservationId: string): Promise<void> {
  await user.clear(field());
  await user.type(field(), reservationId);
  await user.click(searchButton());
}

async function expectDetailFocused(): Promise<void> {
  const title = await screen.findByRole('heading', { name: 'Reserva encontrada', level: 2 });
  await waitFor(() => expect(title).toHaveFocus());
}

function expectCommonDetailFields(): void {
  expect(screen.getByText(ID_PENDENTE)).toBeInTheDocument();
  expect(screen.getByText(CORRELATION_ID)).toBeInTheDocument();
  expect(screen.getByText('guest-marina-alves')).toBeInTheDocument();
  expect(screen.getByText('3fa85f64-5717-4562-b3fc-2c963f66afa6')).toBeInTheDocument();
  expect(screen.getByText('2')).toBeInTheDocument();
  expect(screen.getByText('350.00 BRL')).toBeInTheDocument();
  expect(screen.getByText('1050.00 BRL')).toBeInTheDocument();
}

// Ciclo de vida do servidor MSW: setup compartilhado em src/test/setup.ts.
afterEach(() => {
  server.resetHandlers();
  cleanup();
  vi.unstubAllEnvs();
  vi.restoreAllMocks();
});

describe('ReservationLookupPage', () => {
  it('renderiza o formulário inicial com label, campo e botão habilitado', () => {
    stubBookingApiUrl();
    render(<ReservationLookupPage />);

    expect(screen.getByRole('heading', { name: 'Consultar reserva', level: 1 })).toBeInTheDocument();
    expect(field()).toBeInTheDocument();
    expect(searchButton()).toBeEnabled();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  describe('validação local (sem request)', () => {
    it('vazio não dispara request e move foco ao erro', async () => {
      stubBookingApiUrl();
      const getByIdSpy = vi.spyOn(reservationDetailApi, 'getById');
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await user.click(searchButton());

      expect(getByIdSpy).not.toHaveBeenCalled();
      const summary = screen.getByRole('alert');
      expect(summary).toHaveTextContent('Informe o identificador da reserva.');
      await waitFor(() => expect(summary).toHaveFocus());
      expect(field()).toHaveAttribute('aria-invalid', 'true');
    });

    it('identificador malformado não dispara request e move foco ao erro', async () => {
      stubBookingApiUrl();
      const getByIdSpy = vi.spyOn(reservationDetailApi, 'getById');
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await user.type(field(), 'nao-e-um-uuid');
      await user.click(searchButton());

      expect(getByIdSpy).not.toHaveBeenCalled();
      const summary = screen.getByRole('alert');
      expect(summary).toHaveTextContent('Informe um identificador (UUID) válido');
      await waitFor(() => expect(summary).toHaveFocus());
    });
  });

  describe('200 — três pares estado/sagaStatus', () => {
    it('solicitada/pendente exibe todos os campos, sem motivo de cancelamento', async () => {
      stubBookingApiUrl();
      server.use(reservationDetailFoundHandler('pendente'));
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await search(user, ID_PENDENTE);
      await expectDetailFocused();

      expect(screen.getByText('solicitada')).toBeInTheDocument();
      expect(screen.getByText('pendente')).toBeInTheDocument();
      expectCommonDetailFields();
      expect(screen.queryByText('Motivo do cancelamento')).not.toBeInTheDocument();
      expect(screen.queryByText(CANCELLATION_REASON)).not.toBeInTheDocument();
    });

    it('confirmada/autorizado exibe todos os campos, sem motivo de cancelamento', async () => {
      stubBookingApiUrl();
      server.use(reservationDetailFoundHandler('autorizado'));
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await search(user, ID_AUTORIZADO);
      await expectDetailFocused();

      expect(screen.getByText('confirmada')).toBeInTheDocument();
      expect(screen.getByText('autorizado')).toBeInTheDocument();
      expect(screen.getByText(CORRELATION_ID)).toBeInTheDocument();
      expect(screen.queryByText('Motivo do cancelamento')).not.toBeInTheDocument();
    });

    it('cancelada/rejeitado exibe o motivo do cancelamento', async () => {
      stubBookingApiUrl();
      server.use(reservationDetailFoundHandler('rejeitado'));
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await search(user, ID_PENDENTE);
      await expectDetailFocused();

      expect(screen.getByText('cancelada')).toBeInTheDocument();
      expect(screen.getByText('rejeitado')).toBeInTheDocument();
      expect(screen.getByText('Motivo do cancelamento')).toBeInTheDocument();
      expect(screen.getByText(CANCELLATION_REASON)).toBeInTheDocument();
    });
  });

  describe('erros por (status, code)', () => {
    it('400 do backend mostra erro de formato distinto de "não encontrada"', async () => {
      stubBookingApiUrl();
      server.use(reservationDetailMalformedHandler());
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await search(user, ID_PENDENTE);

      const summary = await screen.findByRole('alert');
      expect(summary).toHaveTextContent('não está em um formato válido');
      expect(summary).not.toHaveTextContent('Nenhuma reserva encontrada');
      await waitFor(() => expect(summary).toHaveFocus());
      expect(field()).toHaveAttribute('aria-invalid', 'true');
      expect(screen.queryByRole('heading', { name: 'Reserva encontrada' })).not.toBeInTheDocument();
    });

    it('404 mostra "não encontrada" em tom neutro, nunca como alert', async () => {
      stubBookingApiUrl();
      server.use(reservationDetailNotFoundHandler());
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await search(user, ID_PENDENTE);

      const feedback = await screen.findByRole('status');
      expect(feedback).toHaveTextContent('Nenhuma reserva encontrada com esse identificador');
      expect(feedback).toHaveAttribute('data-tone', 'neutral');
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(screen.queryByRole('heading', { name: 'Reserva encontrada' })).not.toBeInTheDocument();
    });

    it('500 mostra mensagem genérica com traceId e permite nova busca', async () => {
      stubBookingApiUrl();
      server.use(reservationDetailInternalErrorHandler('trace-500-abc'));
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await search(user, ID_PENDENTE);

      const feedback = await screen.findByRole('alert');
      expect(feedback).toHaveTextContent('Ocorreu um erro interno. Tente novamente mais tarde.');
      expect(feedback).toHaveTextContent('Trace de diagnóstico: trace-500-abc');

      server.use(reservationDetailFoundHandler('pendente'));
      await user.click(searchButton());
      await expectDetailFocused();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('falha de rede mostra mensagem genérica e permite nova tentativa', async () => {
      stubBookingApiUrl();
      server.use(
        http.get(`${RESERVATION_DETAIL_BASE_URL}/:reservationId`, () => HttpResponse.error()),
      );
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await search(user, ID_PENDENTE);

      const feedback = await screen.findByRole('alert');
      expect(feedback).toHaveTextContent('Ocorreu um erro interno. Tente novamente mais tarde.');

      server.use(reservationDetailFoundHandler('autorizado'));
      await user.click(searchButton());
      await expectDetailFocused();
      expect(screen.getByText('autorizado')).toBeInTheDocument();
    });
  });

  describe('substituição de resultado e concorrência', () => {
    it('segunda busca substitui o resultado anterior exibido', async () => {
      stubBookingApiUrl();
      server.use(reservationDetailFoundHandler('pendente'));
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await search(user, ID_PENDENTE);
      await expectDetailFocused();
      expect(screen.getByText('solicitada')).toBeInTheDocument();

      server.use(reservationDetailFoundHandler('autorizado'));
      await search(user, ID_AUTORIZADO);
      await waitFor(() => expect(screen.getByText('confirmada')).toBeInTheDocument());
      expect(screen.queryByText('solicitada')).not.toBeInTheDocument();
      expect(screen.queryByText('pendente')).not.toBeInTheDocument();
    });

    it('loading desabilita o botão e impede busca concorrente', async () => {
      stubBookingApiUrl();
      server.use(
        http.get(`${RESERVATION_DETAIL_BASE_URL}/:reservationId`, async () => {
          await delay(150);
          return HttpResponse.json({ ...RESERVATION_DETAIL_FIXTURES.pendente }, { status: 200 });
        }),
      );
      const user = userEvent.setup();
      render(<ReservationLookupPage />);

      await user.type(field(), ID_PENDENTE);
      const click = user.click(searchButton());

      await waitFor(() => expect(searchButton()).toBeDisabled());
      expect(screen.getByRole('status')).toHaveTextContent('Buscando reserva');
      await click;
      await expectDetailFocused();
      expect(searchButton()).toBeEnabled();
    });
  });
});
