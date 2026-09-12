import { describe, expect, it } from 'vitest';
import type { ReservationFormValues } from '../types/reservationForm.ts';
import { validateReservationForm } from './reservationFormValidation.ts';

const VALID: ReservationFormValues = {
  accommodationId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  guestReference: 'guest-marina-alves',
  checkIn: '2026-10-10',
  checkOut: '2026-10-13',
  guestsCount: '2',
};

describe('validateReservationForm', () => {
  it('retorna objeto vazio para um formulário válido', () => {
    expect(validateReservationForm(VALID)).toEqual({});
  });

  describe('accommodationId', () => {
    it('rejeita vazio com mensagem de UUID obrigatório', () => {
      const errors = validateReservationForm({ ...VALID, accommodationId: '' });
      expect(errors.accommodationId).toBe('Informe o identificador (UUID) da acomodação.');
    });

    it('rejeita formato que não é UUID', () => {
      const errors = validateReservationForm({ ...VALID, accommodationId: 'acomodacao-123' });
      expect(errors.accommodationId).toBeDefined();
    });

    it('aceita UUID em maiúsculas', () => {
      const errors = validateReservationForm({
        ...VALID,
        accommodationId: '3FA85F64-5717-4562-B3FC-2C963F66AFA6',
      });
      expect(errors.accommodationId).toBeUndefined();
    });
  });

  describe('guestReference', () => {
    it('rejeita vazio', () => {
      expect(validateReservationForm({ ...VALID, guestReference: '' }).guestReference).toBe(
        'Informe a referência do guest.',
      );
    });

    it('rejeita string composta somente de espaços em branco', () => {
      const errors = validateReservationForm({ ...VALID, guestReference: '   \t ' });
      expect(errors.guestReference).toBe('Informe a referência do guest.');
    });

    it('rejeita mais de 255 caracteres', () => {
      const errors = validateReservationForm({ ...VALID, guestReference: 'a'.repeat(256) });
      expect(errors.guestReference).toBe('A referência do guest deve ter no máximo 255 caracteres.');
    });

    it('aceita exatamente 255 caracteres', () => {
      expect(validateReservationForm({ ...VALID, guestReference: 'a'.repeat(255) }).guestReference).toBeUndefined();
    });

    it('não faz trim silencioso: valor com espaços nas pontas permanece válido', () => {
      expect(validateReservationForm({ ...VALID, guestReference: ' guest-marina ' }).guestReference).toBeUndefined();
    });
  });

  describe('período', () => {
    it('rejeita check-in vazio', () => {
      expect(validateReservationForm({ ...VALID, checkIn: '' }).checkIn).toBe('Informe a data de check-in.');
    });

    it('rejeita check-out vazio', () => {
      expect(validateReservationForm({ ...VALID, checkOut: '' }).checkOut).toBe('Informe a data de check-out.');
    });

    it('rejeita check-out igual ao check-in associando o erro às duas datas', () => {
      const errors = validateReservationForm({ ...VALID, checkIn: '2026-10-10', checkOut: '2026-10-10' });
      expect(errors).toEqual({
        checkIn: 'O check-out deve ser posterior ao check-in.',
        checkOut: 'O check-out deve ser posterior ao check-in.',
      });
    });

    it('rejeita check-out anterior ao check-in', () => {
      const errors = validateReservationForm({ ...VALID, checkIn: '2026-10-13', checkOut: '2026-10-10' });
      expect(errors.checkIn).toBe('O check-out deve ser posterior ao check-in.');
      expect(errors.checkOut).toBe('O check-out deve ser posterior ao check-in.');
    });

    it('não compara período quando as datas estão vazias (erro é de obrigatório)', () => {
      const errors = validateReservationForm({ ...VALID, checkIn: '', checkOut: '' });
      expect(errors.checkIn).toBe('Informe a data de check-in.');
      expect(errors.checkOut).toBe('Informe a data de check-out.');
    });
  });

  describe('guestsCount', () => {
    it('rejeita vazio', () => {
      expect(validateReservationForm({ ...VALID, guestsCount: '' }).guestsCount).toBe(
        'Informe o número de hóspedes.',
      );
    });

    it.each(['0', '-1', '2.5', 'abc'])('rejeita valor não inteiro positivo: %s', (guestsCount) => {
      expect(validateReservationForm({ ...VALID, guestsCount }).guestsCount).toBe(
        'Informe um número inteiro de hóspedes (mínimo 1).',
      );
    });

    it('aceita mínimo 1', () => {
      expect(validateReservationForm({ ...VALID, guestsCount: '1' }).guestsCount).toBeUndefined();
    });
  });
});
