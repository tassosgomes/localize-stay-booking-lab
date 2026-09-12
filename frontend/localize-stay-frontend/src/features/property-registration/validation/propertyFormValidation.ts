import {
  PROPERTY_FIELD_INPUT_IDS,
  PROPERTY_FORM_FIELDS,
  PROPERTY_LOCATION_MAX_LENGTH,
  PROPERTY_NAME_MAX_LENGTH,
  type FieldProblem,
  type PropertyFormErrors,
  type PropertyFormField,
  type PropertyFormValues,
} from '../types/propertyForm.ts';

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const PROBLEM_FIELD_ALIASES: Record<string, PropertyFormField> = {
  name: 'name',
  location: 'location',
  hostReferenceId: 'hostReferenceId',
  host_reference_id: 'hostReferenceId',
  'X-Host-Reference-Id': 'hostReferenceId',
  'x-host-reference-id': 'hostReferenceId',
};

export function hasNonWhitespace(value: string): boolean {
  return /\S/.test(value);
}

export function isValidUuid(value: string): boolean {
  return UUID_PATTERN.test(value);
}

export function hasPropertyFormErrors(errors: PropertyFormErrors): boolean {
  return PROPERTY_FORM_FIELDS.some((field) => errors[field] !== undefined);
}

export type PropertyUpdateChanges = {
  name?: string;
  location?: string;
};

export const EDIT_REQUIRES_CHANGE_MESSAGE =
  'Altere o nome ou a localização antes de confirmar.';

export function buildPropertyUpdateRequest(
  baseline: Pick<PropertyFormValues, 'name' | 'location'>,
  draft: Pick<PropertyFormValues, 'name' | 'location'>,
): PropertyUpdateChanges | null {
  const changes: PropertyUpdateChanges = {};

  if (draft.name !== baseline.name) {
    changes.name = draft.name;
  }
  if (draft.location !== baseline.location) {
    changes.location = draft.location;
  }

  return changes.name === undefined && changes.location === undefined ? null : changes;
}

export function validateEditPropertyForm(
  values: PropertyFormValues,
  baseline: Pick<PropertyFormValues, 'name' | 'location'>,
): PropertyFormErrors {
  const errors: PropertyFormErrors = {};

  const hostError = validateHostReferenceId(values.hostReferenceId);
  if (hostError !== undefined) {
    errors.hostReferenceId = hostError;
  }

  if (values.name !== baseline.name) {
    const nameError = validateBoundedText(
      values.name,
      PROPERTY_NAME_MAX_LENGTH,
      'Informe o nome da hospedagem.',
      'O nome não pode conter apenas espaços.',
      'O nome deve ter no máximo 120 caracteres.',
    );
    if (nameError !== undefined) {
      errors.name = nameError;
    }
  }

  if (values.location !== baseline.location) {
    const locationError = validateBoundedText(
      values.location,
      PROPERTY_LOCATION_MAX_LENGTH,
      'Informe a localização.',
      'A localização não pode conter apenas espaços.',
      'A localização deve ter no máximo 500 caracteres.',
    );
    if (locationError !== undefined) {
      errors.location = locationError;
    }
  }

  return errors;
}

export function validateCreatePropertyForm(values: PropertyFormValues): PropertyFormErrors {
  const errors: PropertyFormErrors = {};

  const hostError = validateHostReferenceId(values.hostReferenceId);
  if (hostError !== undefined) {
    errors.hostReferenceId = hostError;
  }

  const nameError = validateBoundedText(
    values.name,
    PROPERTY_NAME_MAX_LENGTH,
    'Informe o nome da hospedagem.',
    'O nome não pode conter apenas espaços.',
    'O nome deve ter no máximo 120 caracteres.',
  );
  if (nameError !== undefined) {
    errors.name = nameError;
  }

  const locationError = validateBoundedText(
    values.location,
    PROPERTY_LOCATION_MAX_LENGTH,
    'Informe a localização.',
    'A localização não pode conter apenas espaços.',
    'A localização deve ter no máximo 500 caracteres.',
  );
  if (locationError !== undefined) {
    errors.location = locationError;
  }

  return errors;
}

export function mapProblemDetailsToFieldErrors(details: FieldProblem[]): PropertyFormErrors {
  const errors: PropertyFormErrors = {};
  for (const item of details) {
    const field = PROBLEM_FIELD_ALIASES[item.field];
    if (field !== undefined && errors[field] === undefined) {
      errors[field] = item.message;
    }
  }
  return errors;
}

export function propertyFieldInputId(field: PropertyFormField): string {
  return PROPERTY_FIELD_INPUT_IDS[field];
}

function validateHostReferenceId(value: string): string | undefined {
  if (value.length === 0) {
    return 'Informe o identificador fictício do Host.';
  }
  if (!isValidUuid(value)) {
    return 'Informe um UUID válido.';
  }
  return undefined;
}

function validateBoundedText(
  value: string,
  maxLength: number,
  emptyMessage: string,
  whitespaceMessage: string,
  maxLengthMessage: string,
): string | undefined {
  if (value.length === 0) {
    return emptyMessage;
  }
  if (!hasNonWhitespace(value)) {
    return whitespaceMessage;
  }
  if (value.length > maxLength) {
    return maxLengthMessage;
  }
  return undefined;
}
