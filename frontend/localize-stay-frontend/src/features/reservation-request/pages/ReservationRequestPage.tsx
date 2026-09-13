import { useEffect, useRef, useState } from 'react';
import { OperationFeedback } from '../../../components/OperationFeedback.tsx';
import {
  reservationApi,
  type CreateReservationRequest,
  type Reservation,
  type ReservationApiResult,
} from '../api/reservationApi.ts';
import { RequestReservationForm } from '../components/RequestReservationForm.tsx';
import { ReservationResultSummary } from '../components/ReservationResultSummary.tsx';
import { mapReservationFailure, type ReservationFailure } from '../errors/reservationErrorMapping.ts';
import {
  EMPTY_RESERVATION_FORM_VALUES,
  type ReservationFormErrors,
  type ReservationFormField,
  type ReservationFormValues,
} from '../types/reservationForm.ts';
import { validateReservationForm } from '../validation/reservationFormValidation.ts';

// Máquina de estados da página (techspec §Hierarquia de Componentes e Estados):
// idle → submitting → requested (201)
//                    → rejected (422/400 — volta ao formulário com erro)
//                    → unavailable (503 — falha temporária, reenvio imediato)
//                    → failed (500/rede — mensagem genérica)
// Nenhum estado de falha limpa o formulário: os valores digitados são
// preservados para correção/reenvio. Em `requested` o formulário é substituído
// pelo resumo congelado; "Nova solicitação" volta a idle com formulário vazio.
type ReservationRequestStatus = 'idle' | 'submitting' | 'requested' | 'rejected' | 'unavailable' | 'failed';

const LOCAL_VALIDATION_SUMMARY_TITLE = 'Corrija os campos destacados antes de enviar.';

function hasErrors(errors: ReservationFormErrors): boolean {
  return Object.values(errors).some(Boolean);
}

function toRequest(values: ReservationFormValues): CreateReservationRequest {
  return {
    accommodationId: values.accommodationId,
    guestReference: values.guestReference,
    checkIn: values.checkIn,
    checkOut: values.checkOut,
    guestsCount: Number(values.guestsCount),
  };
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

export default function ReservationRequestPage() {
  const [values, setValues] = useState<ReservationFormValues>(EMPTY_RESERVATION_FORM_VALUES);
  const [status, setStatus] = useState<ReservationRequestStatus>('idle');
  const [failure, setFailure] = useState<ReservationFailure | null>(null);
  const [reservation, setReservation] = useState<Reservation | null>(null);
  const [errorFocusTick, setErrorFocusTick] = useState(0);
  const [resultFocusTick, setResultFocusTick] = useState(0);
  const errorSummaryRef = useRef<HTMLDivElement>(null);
  const resultHeadingRef = useRef<HTMLHeadingElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  // Foco após falha de formulário (local, 400 ou 422): o resumo recebe foco.
  useEffect(() => {
    if (errorFocusTick > 0) errorSummaryRef.current?.focus();
  }, [errorFocusTick]);

  // Foco após 201: título do resumo, sem retirar o controle do teclado.
  useEffect(() => {
    if (resultFocusTick > 0) resultHeadingRef.current?.focus();
  }, [resultFocusTick]);

  // Desmontagem da página cancela a requisição pendente (AbortController por
  // submit — techspec §Estratégia de Fetching).
  useEffect(() => () => abortRef.current?.abort(), []);

  function handleFieldChange(field: ReservationFormField, value: string) {
    setValues((previous) => ({ ...previous, [field]: value }));
  }

  function applyResult(result: ReservationApiResult) {
    switch (result.kind) {
      case 'success':
        setReservation(result.reservation);
        setStatus('requested');
        setResultFocusTick((tick) => tick + 1);
        return;
      case 'rejected':
      case 'malformed':
        setFailure(mapReservationFailure(result));
        setStatus('rejected');
        setErrorFocusTick((tick) => tick + 1);
        return;
      case 'unavailable':
        setFailure(mapReservationFailure(result));
        setStatus('unavailable');
        return;
      case 'failed':
        setFailure(mapReservationFailure(result));
        setStatus('failed');
    }
  }

  async function handleSubmit() {
    // Validação local antecipa RN-02/RN-03 e shape — sem disparar request.
    const localErrors = validateReservationForm(values);
    if (hasErrors(localErrors)) {
      setFailure({ kind: 'form', summaryTitle: LOCAL_VALIDATION_SUMMARY_TITLE, fields: localErrors });
      setStatus('idle');
      setErrorFocusTick((tick) => tick + 1);
      return;
    }

    setFailure(null);
    setStatus('submitting');
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    try {
      const result = await reservationApi.request(toRequest(values), controller.signal);
      applyResult(result);
    } catch (error) {
      if (isAbortError(error)) return;
      setFailure(mapReservationFailure({ kind: 'failed' }));
      setStatus('failed');
    } finally {
      if (abortRef.current === controller) abortRef.current = null;
    }
  }

  function handleNewRequest() {
    abortRef.current?.abort();
    setValues(EMPTY_RESERVATION_FORM_VALUES);
    setReservation(null);
    setFailure(null);
    setStatus('idle');
  }

  const submitting = status === 'submitting';
  const formFailure = failure?.kind === 'form' ? failure : null;

  return (
    <section className="reservation-workspace" aria-labelledby="reservation-request-title">
      <header className="reservation-workspace__header">
        <h1 id="reservation-request-title" className="reservation-workspace__title">
          Solicitar reserva
        </h1>
        <p className="reservation-workspace__lead">
          Preencha as informações para registrar seu pedido de reserva. Os dados e valores serão congelados no envio.
        </p>
      </header>
      {status === 'requested' && reservation ? (
        <ReservationResultSummary
          reservation={reservation}
          headingRef={resultHeadingRef}
          onNewRequest={handleNewRequest}
        />
      ) : (
        <>
          {submitting ? <OperationFeedback tone="neutral">Enviando solicitação…</OperationFeedback> : null}
          {failure?.kind === 'temporary' ? (
            <OperationFeedback tone="temporary">{failure.message}</OperationFeedback>
          ) : null}
          {failure?.kind === 'fatal' ? (
            <OperationFeedback tone="error">
              {failure.message}
              {failure.traceId ? <> Trace de diagnóstico: {failure.traceId}</> : null}
            </OperationFeedback>
          ) : null}
          <RequestReservationForm
            values={values}
            fieldErrors={formFailure?.fields ?? {}}
            errorSummary={formFailure ? { title: formFailure.summaryTitle, fields: formFailure.fields } : null}
            errorSummaryRef={errorSummaryRef}
            submitting={submitting}
            onFieldChange={handleFieldChange}
            onSubmit={() => void handleSubmit()}
          />
        </>
      )}
    </section>
  );
}
