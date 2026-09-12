import type { Ref } from 'react';
import {
  PROPERTY_FORM_FIELDS,
  type PropertyFormErrors,
  type PropertyFormField,
} from '../types/propertyForm.ts';
import { propertyFieldInputId } from '../validation/propertyFormValidation.ts';

const FIELD_LINK_LABELS: Record<PropertyFormField, string> = {
  hostReferenceId: 'Identificador fictício do Host',
  name: 'Nome da hospedagem',
  location: 'Localização',
};

export interface FormErrorSummaryProps {
  errors: PropertyFormErrors;
  summaryRef: Ref<HTMLDivElement>;
}

export function FormErrorSummary({ errors, summaryRef }: FormErrorSummaryProps) {
  const items = PROPERTY_FORM_FIELDS.flatMap((field) => {
    const message = errors[field];
    return message === undefined ? [] : [{ field, message }];
  });

  if (items.length === 0) {
    return null;
  }

  return (
    <div
      ref={summaryRef}
      className="form-error-summary"
      role="alert"
      tabIndex={-1}
      aria-labelledby="property-form-error-heading"
    >
      <h2 id="property-form-error-heading">Há erros no formulário</h2>
      <ul>
        {items.map((item) => {
          const inputId = propertyFieldInputId(item.field);
          return (
            <li key={item.field}>
              <a
                href={`#${inputId}`}
                onClick={(event) => {
                  event.preventDefault();
                  document.getElementById(inputId)?.focus();
                }}
              >
                {FIELD_LINK_LABELS[item.field]}: {item.message}
              </a>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
