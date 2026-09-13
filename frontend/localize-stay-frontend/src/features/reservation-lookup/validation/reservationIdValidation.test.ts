// @vitest-environment jsdom
// Unitários da validação local do identificador (task 4.1): vazio,
// whitespace, UUID inválido, UUID válido.
import { describe, expect, it } from 'vitest';
import {
  RESERVATION_ID_FORMAT_MESSAGE,
  RESERVATION_ID_REQUIRED_MESSAGE,
  validateReservationId,
} from './reservationIdValidation.ts';

describe('validateReservationId', () => {
  it('rejeita string vazia como identificador obrigatório', () => {
    expect(validateReservationId('')).toBe(RESERVATION_ID_REQUIRED_MESSAGE);
  });

  it('rejeita whitespace como identificador obrigatório', () => {
    expect(validateReservationId('   \t  ')).toBe(RESERVATION_ID_REQUIRED_MESSAGE);
  });

  it('rejeita texto fora do formato UUID', () => {
    expect(validateReservationId('nao-e-um-uuid')).toBe(RESERVATION_ID_FORMAT_MESSAGE);
  });

  it('rejeita UUID incompleto', () => {
    expect(validateReservationId('8f14e45f-ceea-467e-a5f0')).toBe(RESERVATION_ID_FORMAT_MESSAGE);
  });

  it('aceita UUID válido', () => {
    expect(validateReservationId('8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e')).toBeUndefined();
  });

  it('aceita UUID válido com espaços ao redor', () => {
    expect(validateReservationId('  8f14e45f-ceea-467e-a5f0-3f9e6d3d0b1e  ')).toBeUndefined();
  });
});
