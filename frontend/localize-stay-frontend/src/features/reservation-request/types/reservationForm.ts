// Estado editável do formulário de Solicitação de Reserva — separado dos tipos
// de transporte (CreateReservationRequest) para representar texto ainda
// inválido (ex.: data vazia) antes de existir um payload válido (techspec
// frontend, §Geração de Tipos do API Contract).
export interface ReservationFormValues {
  accommodationId: string;
  guestReference: string;
  checkIn: string;
  checkOut: string;
  guestsCount: string;
}

export type ReservationFormField = keyof ReservationFormValues;

export type ReservationFormErrors = Partial<Record<ReservationFormField, string>>;

export const EMPTY_RESERVATION_FORM_VALUES: ReservationFormValues = {
  accommodationId: '',
  guestReference: '',
  checkIn: '',
  checkOut: '',
  guestsCount: '',
};
