// Validação local pura (sem I/O) do identificador da reserva — antecipa o
// 400 do backend (formato UUID) sem se tornar autoridade: o caminho 400 do
// backend continua testado, para cobrir uma resposta malformada que escape da
// validação local (frontend-techspec.md §Validação de Formulários).
const UUID_PATTERN = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export const RESERVATION_ID_REQUIRED_MESSAGE = 'Informe o identificador da reserva.';

export const RESERVATION_ID_FORMAT_MESSAGE =
  'Informe um identificador (UUID) válido para a reserva.';

// Retorna a mensagem de erro quando o valor é vazio ou malformado, ou
// `undefined` quando o valor é um UUID bem formado. Espaços ao redor são
// ignorados só para decidir vazio/inválido; o chamador usa o valor aparado
// na busca.
export function validateReservationId(value: string): string | undefined {
  const trimmed = value.trim();
  if (trimmed === '') {
    return RESERVATION_ID_REQUIRED_MESSAGE;
  }
  if (!UUID_PATTERN.test(trimmed)) {
    return RESERVATION_ID_FORMAT_MESSAGE;
  }
  return undefined;
}
