// @vitest-environment jsdom
// Teste de integração RTL + MSW da jornada de Solicitação de Reserva (task
// 5.0 / V-FE-01): prova os 7 desfechos do contrato (201 + 400 + 5×422 + 503)
// no mesmo formulário. O ciclo de vida do servidor MSW
// (listen/resetHandlers/close) é o compartilhado de src/test/setup.ts; este
// arquivo compõe apenas cenários via server.use(...).
import '@testing-library/jest-dom/vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  BOOKING_API_BASE_URL,
  RESERVATION_CREATED_FIXTURE,
  RESERVATIONS_URL,
  catalogUnavailableHandler,
  reservationCreatedHandler,
  reservationInternalErrorHandler,
  reservationMalformedHandler,
  reservationRejectedHandler,
} from '../../../test/mocks/handlers.ts';
import { server } from '../../../test/mocks/server.ts';
import { reservationApi } from '../api/reservationApi.ts';
import ReservationRequestPage from './ReservationRequestPage.tsx';

const VALID_UUID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
const VALID_FORM = {
  accommodationId: VALID_UUID,
  guestReference: 'guest-marina-alves',
  checkIn: '2026-10-10',
  checkOut: '2026-10-13',
  guestsCount: '2',
};

function stubBookingApiUrl(): void {
  vi.stubEnv('VITE_BOOKING_API_URL', BOOKING_API_BASE_URL);
}

type User = ReturnType<typeof userEvent.setup>;

async function fillValidForm(user: User): Promise<void> {
  await user.type(screen.getByLabelText('ID da acomodação'), VALID_FORM.accommodationId);
  await user.type(screen.getByLabelText('Guest de referência'), VALID_FORM.guestReference);
  // <input type="date"> não recebe texto via teclado no jsdom: o valor
  // yyyy-MM-dd é atribuído via change, mesmo formato produzido nativamente.
  fireEvent.change(screen.getByLabelText('Data de check-in'), { target: { value: VALID_FORM.checkIn } });
  fireEvent.change(screen.getByLabelText('Data de check-out'), { target: { value: VALID_FORM.checkOut } });
  await user.type(screen.getByLabelText('Número de hóspedes'), VALID_FORM.guestsCount);
}

function submitButton(): HTMLButtonElement {
  return screen.getByRole('button', { name: 'Solicitar reserva' });
}

function input(label: string): HTMLInputElement {
  return screen.getByLabelText(label);
}

function expectValuesPreserved(): void {
  expect(input('ID da acomodação')).toHaveValue(VALID_FORM.accommodationId);
  expect(input('Guest de referência')).toHaveValue(VALID_FORM.guestReference);
  expect(input('Data de check-in')).toHaveValue(VALID_FORM.checkIn);
  expect(input('Data de check-out')).toHaveValue(VALID_FORM.checkOut);
  expect(input('Número de hóspedes')).toHaveValue(Number(VALID_FORM.guestsCount));
}

const ALL_INPUT_LABELS = [
  'ID da acomodação',
  'Guest de referência',
  'Data de check-in',
  'Data de check-out',
  'Número de hóspedes',
] as const;

function expectNoFieldErrors(): void {
  for (const label of ALL_INPUT_LABELS) {
    expect(input(label)).not.toHaveAttribute('aria-invalid');
  }
}

// Ciclo de vida do servidor MSW: setup compartilhado em src/test/setup.ts.
afterEach(() => {
  server.resetHandlers();
  cleanup();
  vi.unstubAllEnvs();
  vi.restoreAllMocks();
});

describe('ReservationRequestPage', () => {
  it('renderiza o formulário com label para cada um dos cinco campos', () => {
    stubBookingApiUrl();
    render(<ReservationRequestPage />);

    expect(screen.getByRole('heading', { name: 'Solicitar reserva', level: 1 })).toBeInTheDocument();
    for (const label of ALL_INPUT_LABELS) {
      expect(screen.getByLabelText(label)).toBeInTheDocument();
    }
    expect(submitButton()).toBeEnabled();
  });

  describe('validação local (sem request)', () => {
    it('rejeita período com check-out igual ao check-in sem chamar o adapter e foca o resumo', async () => {
      stubBookingApiUrl();
      const requestSpy = vi.spyOn(reservationApi, 'request');
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await fillValidForm(user);
      fireEvent.change(screen.getByLabelText('Data de check-out'), { target: { value: '2026-10-10' } });
      await user.click(submitButton());

      expect(requestSpy).not.toHaveBeenCalled();
      const summary = screen.getByRole('alert');
      await waitFor(() => expect(summary).toHaveFocus());
      expect(summary).toHaveTextContent('Corrija os campos destacados antes de enviar.');
      expect(document.getElementById('checkIn-error')).toHaveTextContent(
        'O check-out deve ser posterior ao check-in.',
      );
      expect(document.getElementById('checkOut-error')).toHaveTextContent(
        'O check-out deve ser posterior ao check-in.',
      );
      expect(input('Data de check-in')).toHaveAttribute('aria-invalid', 'true');
      expect(input('Data de check-in')).toHaveAttribute('aria-describedby', 'checkIn-error');
      expect(input('Data de check-out')).toHaveAttribute('aria-invalid', 'true');
      expect(input('Data de check-out')).toHaveAttribute('aria-describedby', 'checkOut-error');
      // O valor digitado (incluindo o check-out inválido) permanece editável.
      expect(input('ID da acomodação')).toHaveValue(VALID_FORM.accommodationId);
      expect(input('Guest de referência')).toHaveValue(VALID_FORM.guestReference);
      expect(input('Data de check-in')).toHaveValue(VALID_FORM.checkIn);
      expect(input('Data de check-out')).toHaveValue('2026-10-10');
      expect(input('Número de hóspedes')).toHaveValue(Number(VALID_FORM.guestsCount));
    });

    it('rejeita zero hóspedes sem chamar o adapter e associa o erro a guestsCount', async () => {
      stubBookingApiUrl();
      const requestSpy = vi.spyOn(reservationApi, 'request');
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await fillValidForm(user);
      await user.clear(input('Número de hóspedes'));
      await user.type(input('Número de hóspedes'), '0');
      await user.click(submitButton());

      expect(requestSpy).not.toHaveBeenCalled();
      await waitFor(() => expect(screen.getByRole('alert')).toHaveFocus());
      expect(input('Número de hóspedes')).toHaveAttribute('aria-invalid', 'true');
      expect(input('Número de hóspedes')).toHaveAttribute('aria-describedby', 'guestsCount-error');
      expect(screen.getByText('Informe um número inteiro de hóspedes (mínimo 1).')).toBeInTheDocument();
    });

    it('submit com formulário vazio mostra resumo com os erros de todos os campos', async () => {
      stubBookingApiUrl();
      const requestSpy = vi.spyOn(reservationApi, 'request');
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await user.click(submitButton());

      expect(requestSpy).not.toHaveBeenCalled();
      const summary = screen.getByRole('alert');
      await waitFor(() => expect(summary).toHaveFocus());
      for (const label of ALL_INPUT_LABELS) {
        expect(input(label)).toHaveAttribute('aria-invalid', 'true');
      }
    });
  });

  describe('201 — sucesso com resumo congelado', () => {
    it('substitui o formulário pelo resumo, move o foco para o título e congela os valores', async () => {
      stubBookingApiUrl();
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await fillValidForm(user);
      await user.click(submitButton());

      const heading = await screen.findByRole('heading', { name: 'Reserva solicitada' });
      await waitFor(() => expect(heading).toHaveFocus());
      expect(screen.getByText(RESERVATION_CREATED_FIXTURE.id)).toBeInTheDocument();
      expect(screen.getByText('solicitada')).toBeInTheDocument();
      expect(screen.getByText(`${RESERVATION_CREATED_FIXTURE.pricePerNight} BRL`)).toBeInTheDocument();
      expect(screen.getByText(`${RESERVATION_CREATED_FIXTURE.totalAmount} BRL`)).toBeInTheDocument();
      expect(screen.getByText('O pagamento ainda não foi solicitado.')).toBeInTheDocument();
      // O formulário sai de cena: não há como editar/reenviar a mesma jornada.
      expect(screen.queryByRole('button', { name: 'Solicitar reserva' })).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Data de check-in')).not.toBeInTheDocument();
    });

    it('“Nova solicitação” reinicia a jornada com formulário vazio', async () => {
      stubBookingApiUrl();
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await fillValidForm(user);
      await user.click(submitButton());
      await screen.findByRole('heading', { name: 'Reserva solicitada' });
      await user.click(screen.getByRole('button', { name: 'Nova solicitação' }));

      expect(screen.getByRole('button', { name: 'Solicitar reserva' })).toBeEnabled();
      expect(input('ID da acomodação')).toHaveValue('');
      expect(input('Guest de referência')).toHaveValue('');
      expect(input('Data de check-in')).toHaveValue('');
      expect(input('Data de check-out')).toHaveValue('');
      expect(input('Número de hóspedes').value).toBe('');
      expect(screen.queryByRole('heading', { name: 'Reserva solicitada' })).not.toBeInTheDocument();
    });
  });

  describe('400 — requisição malformada', () => {
    it('exibe mensagem genérica no resumo focado, sem campo específico, e preserva os valores', async () => {
      stubBookingApiUrl();
      server.use(reservationMalformedHandler());
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await fillValidForm(user);
      await user.click(submitButton());

      const summary = await screen.findByRole('alert');
      await waitFor(() => expect(summary).toHaveFocus());
      expect(summary).toHaveTextContent('Não foi possível entender a requisição. Confira os dados e tente novamente.');
      expectNoFieldErrors();
      expectValuesPreserved();
      expect(submitButton()).toBeEnabled();
    });
  });

  describe('422 — rejeições de negócio por code', () => {
    it.each([
      {
        code: 'PERIODO_INVALIDO' as const,
        fields: ['Data de check-in', 'Data de check-out'],
        fragment: 'O check-out deve ser posterior ao check-in.',
      },
      {
        code: 'QUANTIDADE_HOSPEDES_INVALIDA' as const,
        fields: ['Número de hóspedes'],
        fragment: 'Informe um número de hóspedes maior que zero.',
      },
      {
        code: 'ACOMODACAO_INDISPONIVEL' as const,
        fields: ['ID da acomodação'],
        fragment: 'A acomodação informada não existe ou não está ativa.',
      },
      {
        code: 'CAPACIDADE_EXCEDIDA' as const,
        fields: ['Número de hóspedes'],
        fragment: 'capacidade máxima da acomodação',
      },
      {
        code: 'PERIODO_INDISPONIVEL' as const,
        fields: ['Data de check-in', 'Data de check-out'],
        fragment: 'não está disponível em todo o período solicitado',
      },
    ])('422 $code associa o erro ao(s) campo(s) correto(s) e preserva os valores', async ({ code, fields, fragment }) => {
      stubBookingApiUrl();
      server.use(reservationRejectedHandler(code));
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await fillValidForm(user);
      await user.click(submitButton());

      const summary = await screen.findByRole('alert');
      await waitFor(() => expect(summary).toHaveFocus());
      expect(summary).toHaveTextContent('Seu pedido não foi aceito. Corrija os dados e envie novamente.');
      expect(summary).toHaveTextContent(fragment);
      for (const label of ALL_INPUT_LABELS) {
        if (fields.includes(label)) {
          expect(input(label)).toHaveAttribute('aria-invalid', 'true');
          const describedBy = input(label).getAttribute('aria-describedby');
          expect(describedBy).toBeTruthy();
          expect(document.getElementById(describedBy ?? '')).toHaveTextContent(fragment);
        } else {
          expect(input(label)).not.toHaveAttribute('aria-invalid');
        }
      }
      expectValuesPreserved();
      expect(submitButton()).toBeEnabled();
    });
  });

  describe('503 — Catalog indisponível (falha temporária, nunca rejeição)', () => {
    it('exibe role="status" com tom de falha temporária, preserva valores e permite reenvio imediato', async () => {
      stubBookingApiUrl();
      server.use(catalogUnavailableHandler());
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await fillValidForm(user);
      await user.click(submitButton());

      const feedback = await screen.findByRole('status');
      expect(feedback).toHaveTextContent(
        'A validação da solicitação não pôde ser concluída agora porque o serviço de acomodações está indisponível. Tente novamente em instantes.',
      );
      // Não é erro de formulário: sem alert, sem aria-invalid.
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expectNoFieldErrors();
      expectValuesPreserved();
      expect(submitButton()).toBeEnabled();

      // Reenvio imediato: o mesmo formulário pode ser submetido de novo e
      // desta vez obter o 201 (handlers MSW em LIFO).
      server.use(reservationCreatedHandler);
      await user.click(submitButton());
      expect(await screen.findByRole('heading', { name: 'Reserva solicitada' })).toBeInTheDocument();
    });
  });

  describe('500 e falha de rede', () => {
    it('500 exibe mensagem genérica com traceId em role="alert" e preserva os valores', async () => {
      stubBookingApiUrl();
      server.use(reservationInternalErrorHandler('trace-9f3a'));
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await fillValidForm(user);
      await user.click(submitButton());

      const feedback = await screen.findByRole('alert');
      expect(feedback).toHaveTextContent('Erro inesperado ao solicitar a reserva. Tente novamente mais tarde.');
      expect(feedback).toHaveTextContent('trace-9f3a');
      expectNoFieldErrors();
      expectValuesPreserved();
      expect(submitButton()).toBeEnabled();
    });

    it('falha de rede exibe mensagem genérica sem traceId e preserva os valores', async () => {
      stubBookingApiUrl();
      server.use(
        http.post(RESERVATIONS_URL, () => HttpResponse.error()),
      );
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await fillValidForm(user);
      await user.click(submitButton());

      const feedback = await screen.findByRole('alert');
      expect(feedback).toHaveTextContent('Erro inesperado ao solicitar a reserva. Tente novamente mais tarde.');
      expect(feedback).not.toHaveTextContent('Trace de diagnóstico');
      expectValuesPreserved();
    });
  });

  describe('loading e ciclo de vida do submit', () => {
    it('anuncia o envio em role="status", desabilita apenas o botão e impede duplo submit', async () => {
      stubBookingApiUrl();
      let calls = 0;
      server.use(
        http.post(RESERVATIONS_URL, async () => {
          calls += 1;
          await delay(150);
          return HttpResponse.json(RESERVATION_CREATED_FIXTURE, { status: 201 });
        }),
      );
      const user = userEvent.setup();
      render(<ReservationRequestPage />);

      await fillValidForm(user);
      await user.click(submitButton());

      expect(screen.getByRole('status')).toHaveTextContent('Enviando solicitação…');
      expect(submitButton()).toBeDisabled();
      // Leitura/edição do formulário permanece possível durante o loading.
      for (const label of ALL_INPUT_LABELS) {
        expect(input(label)).toBeEnabled();
      }
      // Segundo clique no botão desabilitado não dispara nova requisição.
      await user.click(submitButton());

      expect(await screen.findByRole('heading', { name: 'Reserva solicitada' })).toBeInTheDocument();
      expect(calls).toBe(1);
    });

    it('cancela a requisição pendente ao desmontar a página (AbortController)', async () => {
      stubBookingApiUrl();
      let capturedSignal: AbortSignal | undefined;
      vi.spyOn(reservationApi, 'request').mockImplementation((_input, signal) => {
        capturedSignal = signal;
        return new Promise(() => {});
      });
      const user = userEvent.setup();
      const { unmount } = render(<ReservationRequestPage />);

      await fillValidForm(user);
      await user.click(submitButton());

      expect(capturedSignal).toBeDefined();
      unmount();
      expect(capturedSignal?.aborted).toBe(true);
    });
  });
});
