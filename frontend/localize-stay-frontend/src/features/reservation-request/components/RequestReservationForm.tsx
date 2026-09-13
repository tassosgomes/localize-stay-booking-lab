import type { FormEvent, Ref } from 'react';
import { FormErrorSummary } from '../../../components/FormErrorSummary.tsx';
import type { ReservationFormErrors, ReservationFormField, ReservationFormValues } from '../types/reservationForm.ts';

export interface ReservationErrorSummary {
  title: string;
  fields: ReservationFormErrors;
}

interface RequestReservationFormProps {
  values: ReservationFormValues;
  fieldErrors: ReservationFormErrors;
  errorSummary: ReservationErrorSummary | null;
  errorSummaryRef: Ref<HTMLDivElement>;
  submitting: boolean;
  onFieldChange: (field: ReservationFormField, value: string) => void;
  onSubmit: () => void;
}

// Labels em português, usados tanto no <label htmlFor> quanto no resumo de
// erros — o erro nunca depende apenas de cor nem de posição na tela.
const FIELD_LABELS: Record<ReservationFormField, string> = {
  accommodationId: 'ID da acomodação',
  guestReference: 'Guest de referência',
  checkIn: 'Data de check-in',
  checkOut: 'Data de check-out',
  guestsCount: 'Número de hóspedes',
};

interface FieldConfig {
  field: ReservationFormField;
  type: 'text' | 'date' | 'number';
  autoComplete?: string;
  min?: number;
  step?: number;
}

const FIELDS: readonly FieldConfig[] = [
  { field: 'accommodationId', type: 'text' },
  { field: 'guestReference', type: 'text', autoComplete: 'off' },
  { field: 'checkIn', type: 'date' },
  { field: 'checkOut', type: 'date' },
  { field: 'guestsCount', type: 'number', min: 1, step: 1 },
];

// Agrupa mensagens idênticas de campos distintos em um único item do resumo
// (ex.: check-in e check-out compartilham a mensagem de período inválido).
function buildSummaryItems(fields: ReservationFormErrors) {
  const labelsByMessage = new Map<string, string[]>();
  for (const field of Object.keys(fields) as ReservationFormField[]) {
    const message = fields[field];
    if (!message) continue;
    const labels = labelsByMessage.get(message) ?? [];
    labels.push(FIELD_LABELS[field]);
    labelsByMessage.set(message, labels);
  }
  return [...labelsByMessage.entries()].map(([message, labels]) => ({
    label: labels.length > 1 ? labels.join(' e ') : labels[0],
    message,
  }));
}

// Formulário controlado da solicitação: Accommodation, Guest de referência,
// período (<input type="date">, produz yyyy-MM-dd nativamente) e hóspedes.
// Sem biblioteca de formulário — APIs nativas de React (techspec §Validação).
export function RequestReservationForm({
  values,
  fieldErrors,
  errorSummary,
  errorSummaryRef,
  submitting,
  onFieldChange,
  onSubmit,
}: RequestReservationFormProps) {
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onSubmit();
  }

  return (
    // noValidate: validação custom acessível (aria-invalid/aria-describedby)
    // substitui a nativa, cujos textos não são controláveis nem anunciados
    // no resumo com foco.
    <form
      className="reservation-card reservation-form"
      onSubmit={handleSubmit}
      noValidate
      aria-label="Formulário de solicitação de reserva"
    >
      {errorSummary ? (
        <FormErrorSummary ref={errorSummaryRef} title={errorSummary.title} items={buildSummaryItems(errorSummary.fields)} />
      ) : null}
      <div className="reservation-form__fields">
        {FIELDS.map(({ field, type, autoComplete, min, step }) => {
          const error = fieldErrors[field];
          const isDate = type === 'date';
          return (
            <div
              key={field}
              className={`reservation-form__group ${isDate ? 'reservation-form__group--date' : 'reservation-form__group--full'}`}
            >
              <label className="reservation-form__label" htmlFor={field}>
                {FIELD_LABELS[field]}
              </label>
              <input
                id={field}
                name={field}
                type={type}
                autoComplete={autoComplete}
                min={min}
                step={step}
                value={values[field]}
                onChange={(event) => onFieldChange(field, event.target.value)}
                aria-invalid={error ? true : undefined}
                aria-describedby={error ? `${field}-error` : undefined}
                className={`reservation-form__input ${error ? 'reservation-form__input--error' : ''}`}
              />
              {error ? (
                <p id={`${field}-error`} className="reservation-form__field-error">
                  <strong>{FIELD_LABELS[field]}:</strong> {error}
                </p>
              ) : null}
            </div>
          );
        })}
      </div>
      {/* Apenas o botão é desabilitado durante o envio: a leitura/edição dos
          campos permanece possível (techspec §Acessibilidade). */}
      <div className="reservation-form__actions">
        <button type="submit" className="reservation-button-primary" disabled={submitting}>
          Solicitar reserva
        </button>
      </div>
    </form>
  );
}
