// Validação local pura (sem I/O) — antecipa apenas RN-02 (período) e RN-03
// (hóspedes) mais shape (UUID, obrigatórios, limites do contrato). Capacidade,
// disponibilidade e existência/status da Accommodation dependem de Catalog e
// só são conhecidas na resposta do submit: não são simuladas no cliente.
import type { ReservationFormErrors, ReservationFormValues } from '../types/reservationForm.ts';

const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
const GUEST_REFERENCE_MAX_LENGTH = 255;
const INTEGER_PATTERN = /^\d+$/;
const CHECKOUT_AFTER_CHECKIN_MESSAGE = 'O check-out deve ser posterior ao check-in.';

export function validateReservationForm(values: ReservationFormValues): ReservationFormErrors {
  const errors: ReservationFormErrors = {};

  if (!UUID_PATTERN.test(values.accommodationId)) {
    errors.accommodationId = 'Informe o identificador (UUID) da acomodação.';
  }

  // Sem trim silencioso: ' Marina ' é válido e viaja exatamente assim; só
  // strings sem nenhum caractere visível são rejeitadas (mesma disciplina do
  // contrato, que não declara normalização de valores válidos).
  if (!/\S/.test(values.guestReference)) {
    errors.guestReference = 'Informe a referência do guest.';
  } else if (values.guestReference.length > GUEST_REFERENCE_MAX_LENGTH) {
    errors.guestReference = `A referência do guest deve ter no máximo ${GUEST_REFERENCE_MAX_LENGTH} caracteres.`;
  }

  if (values.checkIn === '') {
    errors.checkIn = 'Informe a data de check-in.';
  }
  if (values.checkOut === '') {
    errors.checkOut = 'Informe a data de check-out.';
  }
  if (errors.checkIn === undefined && errors.checkOut === undefined && values.checkOut <= values.checkIn) {
    errors.checkIn = CHECKOUT_AFTER_CHECKIN_MESSAGE;
    errors.checkOut = CHECKOUT_AFTER_CHECKIN_MESSAGE;
  }

  if (values.guestsCount === '') {
    errors.guestsCount = 'Informe o número de hóspedes.';
  } else if (!INTEGER_PATTERN.test(values.guestsCount) || Number(values.guestsCount) < 1) {
    errors.guestsCount = 'Informe um número inteiro de hóspedes (mínimo 1).';
  }

  return errors;
}
