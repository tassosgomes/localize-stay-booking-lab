import { describe, expect, it } from 'vitest';
import type { ReservationApiResult, RejectionCode } from '../api/reservationApi.ts';
import { mapReservationFailure } from './reservationErrorMapping.ts';

const REJECTION_CODES: RejectionCode[] = [
  'PERIODO_INVALIDO',
  'QUANTIDADE_HOSPEDES_INVALIDA',
  'ACOMODACAO_INDISPONIVEL',
  'CAPACIDADE_EXCEDIDA',
  'PERIODO_INDISPONIVEL',
];

describe('mapReservationFailure', () => {
  describe('422 — rejeições de negócio mapeadas por code', () => {
    it('PERIODO_INVALIDO associa o erro a checkIn e checkOut pedindo correção do período', () => {
      const failure = mapReservationFailure({ kind: 'rejected', code: 'PERIODO_INVALIDO', status: 422 });
      expect(failure).toEqual({
        kind: 'form',
        summaryTitle: 'Seu pedido não foi aceito. Corrija os dados e envie novamente.',
        fields: {
          checkIn: 'O check-out deve ser posterior ao check-in.',
          checkOut: 'O check-out deve ser posterior ao check-in.',
        },
      });
    });

    it('QUANTIDADE_HOSPEDES_INVALIDA associa o erro a guestsCount pedindo valor positivo', () => {
      const failure = mapReservationFailure({ kind: 'rejected', code: 'QUANTIDADE_HOSPEDES_INVALIDA', status: 422 });
      expect(failure.kind).toBe('form');
      if (failure.kind === 'form') {
        expect(Object.keys(failure.fields)).toEqual(['guestsCount']);
        expect(failure.fields.guestsCount).toBe('Informe um número de hóspedes maior que zero.');
      }
    });

    it('ACOMODACAO_INDISPONIVEL associa o erro a accommodationId indicando inexistência/inativa', () => {
      const failure = mapReservationFailure({ kind: 'rejected', code: 'ACOMODACAO_INDISPONIVEL', status: 422 });
      expect(failure.kind).toBe('form');
      if (failure.kind === 'form') {
        expect(Object.keys(failure.fields)).toEqual(['accommodationId']);
        expect(failure.fields.accommodationId).toBe(
          'A acomodação informada não existe ou não está ativa. Confira o identificador.',
        );
      }
    });

    it('CAPACIDADE_EXCEDIDA associa o erro a guestsCount com referência à acomodação', () => {
      const failure = mapReservationFailure({ kind: 'rejected', code: 'CAPACIDADE_EXCEDIDA', status: 422 });
      expect(failure.kind).toBe('form');
      if (failure.kind === 'form') {
        expect(Object.keys(failure.fields)).toEqual(['guestsCount']);
        expect(failure.fields.guestsCount).toBe(
          'O número de hóspedes excede a capacidade máxima da acomodação informada.',
        );
      }
    });

    it('PERIODO_INDISPONIVEL associa o erro a checkIn e checkOut indicando indisponibilidade', () => {
      const failure = mapReservationFailure({ kind: 'rejected', code: 'PERIODO_INDISPONIVEL', status: 422 });
      expect(failure.kind).toBe('form');
      if (failure.kind === 'form') {
        expect(Object.keys(failure.fields)).toEqual(['checkIn', 'checkOut']);
        expect(failure.fields.checkIn).toBe('A acomodação não está disponível em todo o período solicitado.');
      }
    });

    it('cobre exatamente os 5 codes de rejeição do contrato', () => {
      for (const code of REJECTION_CODES) {
        const failure = mapReservationFailure({ kind: 'rejected', code, status: 422 });
        expect(failure.kind).toBe('form');
      }
    });
  });

  it('400 VALIDATION_ERROR vira mensagem genérica sem campo específico', () => {
    const failure = mapReservationFailure({ kind: 'malformed', status: 400 });
    expect(failure).toEqual({
      kind: 'form',
      summaryTitle: 'Não foi possível entender a requisição. Confira os dados e tente novamente.',
      fields: {},
    });
  });

  it('503 CATALOG_INDISPONIVEL vira falha temporária, nunca erro de campo', () => {
    const failure = mapReservationFailure({ kind: 'unavailable', status: 503, traceId: 'abc123' });
    expect(failure).toEqual({
      kind: 'temporary',
      message:
        'A validação da solicitação não pôde ser concluída agora porque o serviço de acomodações está indisponível. Tente novamente em instantes.',
    });
  });

  it('500/falha mapeia a mensagem genérica preservando o traceId quando presente', () => {
    const failure: ReservationApiResult = { kind: 'failed', status: 500, traceId: 'trace-1' };
    expect(mapReservationFailure(failure)).toEqual({
      kind: 'fatal',
      message: 'Erro inesperado ao solicitar a reserva. Tente novamente mais tarde.',
      traceId: 'trace-1',
    });
  });

  it('falha de rede/sem traceId mapeia a mensagem genérica sem traceId', () => {
    expect(mapReservationFailure({ kind: 'failed' })).toEqual({
      kind: 'fatal',
      message: 'Erro inesperado ao solicitar a reserva. Tente novamente mais tarde.',
    });
  });
});
