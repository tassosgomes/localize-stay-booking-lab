// @vitest-environment node
import { describe, expect, it } from 'vitest';
import {
  PROPERTY_LOCATION_MAX_LENGTH,
  PROPERTY_NAME_MAX_LENGTH,
} from '../types/propertyForm.ts';
import {
  hasNonWhitespace,
  isValidUuid,
  mapProblemDetailsToFieldErrors,
  validateCreatePropertyForm,
} from './propertyFormValidation.ts';

const validHost = '421ec5d4-3ba2-4d8e-8958-a51dd0711650';

describe('propertyFormValidation', () => {
  it('accepts a valid host UUID, name and location without trimming values', () => {
    const name = ' Pousada Dunas do Sol ';
    const location = ' Cumbuco, Caucaia - CE ';

    expect(
      validateCreatePropertyForm({
        hostReferenceId: validHost,
        name,
        location,
      }),
    ).toEqual({});
    expect(name.startsWith(' ')).toBe(true);
    expect(location.endsWith(' ')).toBe(true);
  });

  it('rejects an empty host, name and location', () => {
    expect(
      validateCreatePropertyForm({
        hostReferenceId: '',
        name: '',
        location: '',
      }),
    ).toEqual({
      hostReferenceId: 'Informe o identificador fictício do Host.',
      name: 'Informe o nome da hospedagem.',
      location: 'Informe a localização.',
    });
  });

  it('rejects whitespace-only name and location without trimming them into valid values', () => {
    expect(
      validateCreatePropertyForm({
        hostReferenceId: validHost,
        name: '   ',
        location: '\t\n',
      }),
    ).toEqual({
      name: 'O nome não pode conter apenas espaços.',
      location: 'A localização não pode conter apenas espaços.',
    });
    expect(hasNonWhitespace('   ')).toBe(false);
  });

  it('rejects names longer than 120 characters and locations longer than 500', () => {
    expect(
      validateCreatePropertyForm({
        hostReferenceId: validHost,
        name: 'P'.repeat(PROPERTY_NAME_MAX_LENGTH + 1),
        location: 'L'.repeat(PROPERTY_LOCATION_MAX_LENGTH + 1),
      }),
    ).toEqual({
      name: 'O nome deve ter no máximo 120 caracteres.',
      location: 'A localização deve ter no máximo 500 caracteres.',
    });
  });

  it('rejects a host that is not a UUID, including surrounding whitespace', () => {
    expect(isValidUuid(validHost)).toBe(true);
    expect(isValidUuid(` ${validHost} `)).toBe(false);
    expect(
      validateCreatePropertyForm({
        hostReferenceId: ` ${validHost} `,
        name: 'Pousada Dunas do Sol',
        location: 'Cumbuco, Caucaia - CE',
      }),
    ).toEqual({
      hostReferenceId: 'Informe um UUID válido.',
    });
  });

  it('maps problem details onto name, location and Host fields', () => {
    expect(
      mapProblemDetailsToFieldErrors([
        { field: 'name', message: 'O nome não pode conter apenas espaços.' },
        { field: 'location', message: 'A localização deve ter no máximo 500 caracteres.' },
        { field: 'X-Host-Reference-Id', message: 'Informe um UUID válido.' },
      ]),
    ).toEqual({
      name: 'O nome não pode conter apenas espaços.',
      location: 'A localização deve ter no máximo 500 caracteres.',
      hostReferenceId: 'Informe um UUID válido.',
    });
  });
});
