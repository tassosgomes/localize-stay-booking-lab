import type { FormEvent, Ref } from 'react';
import { FormErrorSummary } from './FormErrorSummary.tsx';
import {
  PROPERTY_FIELD_ERROR_IDS,
  PROPERTY_FIELD_HINT_IDS,
  PROPERTY_FIELD_INPUT_IDS,
  PROPERTY_LOCATION_MAX_LENGTH,
  PROPERTY_NAME_MAX_LENGTH,
  type PropertyFormErrors,
  type PropertyFormField,
  type PropertyFormValues,
} from '../types/propertyForm.ts';

export interface CreatePropertyFormProps {
  values: PropertyFormValues;
  errors: PropertyFormErrors;
  submitting: boolean;
  errorSummaryRef: Ref<HTMLDivElement>;
  onFieldChange: (field: PropertyFormField, value: string) => void;
  onSubmit: () => void;
}

function describedBy(field: PropertyFormField, errors: PropertyFormErrors): string {
  const ids: string[] = [];
  if (field === 'hostReferenceId') {
    ids.push(PROPERTY_FIELD_HINT_IDS.hostReferenceId);
  }
  if (errors[field] !== undefined) {
    ids.push(PROPERTY_FIELD_ERROR_IDS[field]);
  }
  return ids.join(' ');
}

export function CreatePropertyForm({
  values,
  errors,
  submitting,
  errorSummaryRef,
  onFieldChange,
  onSubmit,
}: CreatePropertyFormProps) {
  function handleSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    event.stopPropagation();
    onSubmit();
  }

  const hostInvalid = errors.hostReferenceId !== undefined;
  const nameInvalid = errors.name !== undefined;
  const locationInvalid = errors.location !== undefined;

  return (
    <form
      className="property-form"
      noValidate
      method="post"
      action="#"
      onSubmit={handleSubmit}
      aria-busy={submitting}
    >
      <FormErrorSummary errors={errors} summaryRef={errorSummaryRef} />

      <div className="property-form__field">
        <label htmlFor={PROPERTY_FIELD_INPUT_IDS.hostReferenceId}>Identificador fictício do Host</label>
        <input
          id={PROPERTY_FIELD_INPUT_IDS.hostReferenceId}
          name="hostReferenceId"
          type="text"
          autoComplete="off"
          spellCheck={false}
          value={values.hostReferenceId}
          onChange={(event) => onFieldChange('hostReferenceId', event.target.value)}
          aria-invalid={hostInvalid}
          aria-describedby={describedBy('hostReferenceId', errors)}
          aria-required="true"
        />
        <p id={PROPERTY_FIELD_HINT_IDS.hostReferenceId} className="property-form__hint">
          Informe um UUID de laboratório. Isto não é login nem sessão autenticada.
        </p>
        {hostInvalid ? (
          <p id={PROPERTY_FIELD_ERROR_IDS.hostReferenceId} className="property-form__error">
            {errors.hostReferenceId}
          </p>
        ) : null}
      </div>

      <div className="property-form__field">
        <label htmlFor={PROPERTY_FIELD_INPUT_IDS.name}>Nome da hospedagem</label>
        <input
          id={PROPERTY_FIELD_INPUT_IDS.name}
          name="name"
          type="text"
          maxLength={PROPERTY_NAME_MAX_LENGTH}
          value={values.name}
          onChange={(event) => onFieldChange('name', event.target.value)}
          aria-invalid={nameInvalid}
          aria-describedby={describedBy('name', errors) || undefined}
          aria-required="true"
        />
        {nameInvalid ? (
          <p id={PROPERTY_FIELD_ERROR_IDS.name} className="property-form__error">
            {errors.name}
          </p>
        ) : null}
      </div>

      <div className="property-form__field">
        <label htmlFor={PROPERTY_FIELD_INPUT_IDS.location}>Localização</label>
        <textarea
          id={PROPERTY_FIELD_INPUT_IDS.location}
          name="location"
          maxLength={PROPERTY_LOCATION_MAX_LENGTH}
          rows={3}
          value={values.location}
          onChange={(event) => onFieldChange('location', event.target.value)}
          aria-invalid={locationInvalid}
          aria-describedby={describedBy('location', errors) || undefined}
          aria-required="true"
        />
        {locationInvalid ? (
          <p id={PROPERTY_FIELD_ERROR_IDS.location} className="property-form__error">
            {errors.location}
          </p>
        ) : null}
      </div>

      <button type="submit" className="property-form__submit" disabled={submitting}>
        Cadastrar hospedagem
      </button>
    </form>
  );
}
