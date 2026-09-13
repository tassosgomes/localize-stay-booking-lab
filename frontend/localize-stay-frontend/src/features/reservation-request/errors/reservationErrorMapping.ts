// Mapeamento puro de falhas do contrato (reservationApi) para o resultado de
// UI: decide SOMENTE pelo `code` do ProblemDetails — nunca por title/detail.
// 422 vira erro de formulário associado a campo(s); 503 vira falha temporária
// (nunca erro de campo); 400/500/desconhecido viram mensagem genérica.
import type { ReservationApiResult, RejectionCode } from '../api/reservationApi.ts';
import type { ReservationFormErrors } from '../types/reservationForm.ts';

export type ReservationFailure =
  | { kind: 'form'; summaryTitle: string; fields: ReservationFormErrors }
  | { kind: 'temporary'; message: string }
  | { kind: 'fatal'; message: string; traceId?: string };

const REJECTION_SUMMARY_TITLE = 'Seu pedido não foi aceito. Corrija os dados e envie novamente.';
const MALFORMED_REQUEST_SUMMARY_TITLE =
  'Não foi possível entender a requisição. Confira os dados e tente novamente.';
const TEMPORARY_VALIDATION_MESSAGE =
  'A validação da solicitação não pôde ser concluída agora porque o serviço de acomodações está indisponível. Tente novamente em instantes.';
const UNEXPECTED_FAILURE_MESSAGE = 'Erro inesperado ao solicitar a reserva. Tente novamente mais tarde.';

const CHECKOUT_AFTER_CHECKIN_MESSAGE = 'O check-out deve ser posterior ao check-in.';
const PERIOD_UNAVAILABLE_MESSAGE = 'A acomodação não está disponível em todo o período solicitado.';

const REJECTION_FIELDS: Record<RejectionCode, ReservationFormErrors> = {
  PERIODO_INVALIDO: { checkIn: CHECKOUT_AFTER_CHECKIN_MESSAGE, checkOut: CHECKOUT_AFTER_CHECKIN_MESSAGE },
  QUANTIDADE_HOSPEDES_INVALIDA: { guestsCount: 'Informe um número de hóspedes maior que zero.' },
  ACOMODACAO_INDISPONIVEL: {
    accommodationId: 'A acomodação informada não existe ou não está ativa. Confira o identificador.',
  },
  CAPACIDADE_EXCEDIDA: { guestsCount: 'O número de hóspedes excede a capacidade máxima da acomodação informada.' },
  PERIODO_INDISPONIVEL: { checkIn: PERIOD_UNAVAILABLE_MESSAGE, checkOut: PERIOD_UNAVAILABLE_MESSAGE },
};

export function mapReservationFailure(
  result: Exclude<ReservationApiResult, { kind: 'success' }>,
): ReservationFailure {
  switch (result.kind) {
    case 'rejected':
      return { kind: 'form', summaryTitle: REJECTION_SUMMARY_TITLE, fields: REJECTION_FIELDS[result.code] };
    case 'malformed':
      return { kind: 'form', summaryTitle: MALFORMED_REQUEST_SUMMARY_TITLE, fields: {} };
    case 'unavailable':
      return { kind: 'temporary', message: TEMPORARY_VALIDATION_MESSAGE };
    case 'failed':
      return { kind: 'fatal', message: UNEXPECTED_FAILURE_MESSAGE, traceId: result.traceId };
  }
}
