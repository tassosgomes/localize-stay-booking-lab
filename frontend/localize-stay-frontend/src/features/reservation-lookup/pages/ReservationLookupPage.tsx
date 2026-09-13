import { useEffect, useRef, useState, type FormEvent } from 'react';
import { FormErrorSummary } from '../../../components/FormErrorSummary.tsx';
import { OperationFeedback } from '../../../components/OperationFeedback.tsx';
import {
  reservationDetailApi,
  type ReservationDetail,
} from '../api/reservationDetailApi.ts';
import { ReservationDetailView } from '../components/ReservationDetailView.tsx';
import { validateReservationId } from '../validation/reservationIdValidation.ts';

// Máquina de estados da página (frontend-techspec.md §Hierarquia de
// Componentes e Estados):
// idle → searching → found (200)
//                    → notFound (404 — resposta esperada, tom neutro)
//                    → invalid (400 local ou backend — erro de formato)
//                    → failed (500/rede — erro real com traceId)
// Diferente de F01, aqui NÃO há preservação de resultado anterior: cada nova
// busca substitui o resultado exibido (ou limpa, se a nova busca falhar).
type ReservationLookupStatus = 'idle' | 'searching' | 'found' | 'notFound' | 'invalid' | 'failed';

const FIELD_LABEL = 'Identificador da reserva';

// 400 vindo do backend (uma resposta malformada que escapou da validação
// local): texto deliberadamente distinto de "não encontrada" (techspec
// §Tratamento Centralizado de Erros).
const BACKEND_MALFORMED_MESSAGE =
  'O identificador informado não está em um formato válido. Confira o identificador recebido.';

const NOT_FOUND_MESSAGE =
  'Nenhuma reserva encontrada com esse identificador. Confira o identificador recebido.';

const FAILED_MESSAGE = 'Ocorreu um erro interno. Tente novamente mais tarde.';

const SEARCHING_MESSAGE = 'Buscando reserva…';

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

export default function ReservationLookupPage() {
  const [reservationId, setReservationId] = useState('');
  const [status, setStatus] = useState<ReservationLookupStatus>('idle');
  const [reservation, setReservation] = useState<ReservationDetail | null>(null);
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [traceId, setTraceId] = useState<string | null>(null);
  const [errorFocusTick, setErrorFocusTick] = useState(0);
  const [resultFocusTick, setResultFocusTick] = useState(0);
  const errorSummaryRef = useRef<HTMLDivElement>(null);
  const resultHeadingRef = useRef<HTMLHeadingElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  // Foco após erro de formato (local ou 400 do backend): o resumo recebe foco.
  useEffect(() => {
    if (errorFocusTick > 0) errorSummaryRef.current?.focus();
  }, [errorFocusTick]);

  // Foco após 200: título do detalhe, sem retirar o controle do teclado.
  useEffect(() => {
    if (resultFocusTick > 0) resultHeadingRef.current?.focus();
  }, [resultFocusTick]);

  // Desmontagem da página cancela a busca pendente (AbortController por
  // busca — techspec §Estratégia de Fetching).
  useEffect(() => () => abortRef.current?.abort(), []);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (status === 'searching') return;

    // Validação local antecipa o 400 do backend — sem disparar request.
    const localError = validateReservationId(reservationId);
    if (localError !== undefined) {
      abortRef.current?.abort();
      setReservation(null);
      setTraceId(null);
      setFieldError(localError);
      setStatus('invalid');
      setErrorFocusTick((tick) => tick + 1);
      return;
    }

    // Nova busca substitui o resultado anterior (sem preservá-lo).
    setReservation(null);
    setFieldError(null);
    setTraceId(null);
    setStatus('searching');
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    try {
      const result = await reservationDetailApi.getById(reservationId.trim(), controller.signal);
      switch (result.kind) {
        case 'found':
          setReservation(result.reservation);
          setStatus('found');
          setResultFocusTick((tick) => tick + 1);
          return;
        case 'notFound':
          setStatus('notFound');
          return;
        case 'malformed':
          setFieldError(BACKEND_MALFORMED_MESSAGE);
          setStatus('invalid');
          setErrorFocusTick((tick) => tick + 1);
          return;
        case 'failed':
          setTraceId(result.traceId ?? null);
          setStatus('failed');
      }
    } catch (error) {
      if (isAbortError(error)) return;
      setTraceId(null);
      setStatus('failed');
    } finally {
      if (abortRef.current === controller) abortRef.current = null;
    }
  }

  const searching = status === 'searching';
  const invalid = status === 'invalid' && fieldError !== null;

  return (
    <section aria-labelledby="reservation-lookup-title">
      <h1 id="reservation-lookup-title">Consultar reserva</h1>
      {/* noValidate: validação custom acessível (aria-invalid/aria-describedby)
          substitui a nativa, cujos textos não são controláveis nem anunciados
          no resumo com foco. */}
      <form onSubmit={(event) => void handleSubmit(event)} noValidate aria-label="Formulário de consulta de reserva">
        {invalid ? (
          <FormErrorSummary
            ref={errorSummaryRef}
            title="Corrija o campo destacado antes de buscar."
            items={[{ label: FIELD_LABEL, message: fieldError }]}
          />
        ) : null}
        <div>
          <label htmlFor="reservationId">{FIELD_LABEL}</label>
          <input
            id="reservationId"
            name="reservationId"
            type="text"
            autoComplete="off"
            value={reservationId}
            onChange={(event) => setReservationId(event.target.value)}
            aria-invalid={invalid ? true : undefined}
            aria-describedby={invalid ? 'reservationId-error' : undefined}
          />
          {invalid ? (
            <p id="reservationId-error">
              <strong>{FIELD_LABEL}:</strong> {fieldError}
            </p>
          ) : null}
        </div>
        {/* Apenas o botão é desabilitado durante a busca: a leitura/edição do
            campo permanece possível. */}
        <button type="submit" disabled={searching}>
          Buscar reserva
        </button>
      </form>
      {searching ? <OperationFeedback tone="neutral">{SEARCHING_MESSAGE}</OperationFeedback> : null}
      {status === 'notFound' ? <OperationFeedback tone="neutral">{NOT_FOUND_MESSAGE}</OperationFeedback> : null}
      {status === 'failed' ? (
        <OperationFeedback tone="error">
          {FAILED_MESSAGE}
          {traceId ? <> Trace de diagnóstico: {traceId}</> : null}
        </OperationFeedback>
      ) : null}
      {status === 'found' && reservation ? (
        <ReservationDetailView reservation={reservation} headingRef={resultHeadingRef} />
      ) : null}
    </section>
  );
}
