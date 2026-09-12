import { useEffect, useRef, useState } from 'react';
import { CreatePropertyForm } from '../components/CreatePropertyForm.tsx';
import { CreatedPropertySummary } from '../components/CreatedPropertySummary.tsx';
import { OperationFeedback } from '../components/OperationFeedback.tsx';
import {
  propertyApi,
  type NormalizedProblem,
  type Property,
} from '../api/propertyApi.ts';
import {
  EMPTY_PROPERTY_FORM_VALUES,
  type PropertyFormErrors,
  type PropertyFormField,
  type PropertyFormValues,
} from '../types/propertyForm.ts';
import {
  hasPropertyFormErrors,
  mapProblemDetailsToFieldErrors,
  validateCreatePropertyForm,
} from '../validation/propertyFormValidation.ts';

type WorkspacePhase = 'idle' | 'creating' | 'created';

function isAbortError(error: unknown): boolean {
  return (
    (error instanceof DOMException && error.name === 'AbortError') ||
    (error instanceof Error && error.name === 'AbortError')
  );
}

export function PropertyWorkspacePage() {
  const [values, setValues] = useState<PropertyFormValues>(EMPTY_PROPERTY_FORM_VALUES);
  const [errors, setErrors] = useState<PropertyFormErrors>({});
  const [phase, setPhase] = useState<WorkspacePhase>('idle');
  const [createdProperty, setCreatedProperty] = useState<Property | null>(null);
  const [generalProblem, setGeneralProblem] = useState<NormalizedProblem | null>(null);
  const [confirmingNewCreate, setConfirmingNewCreate] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const creatingRef = useRef(false);
  const errorSummaryRef = useRef<HTMLDivElement | null>(null);
  const confirmTitleRef = useRef<HTMLHeadingElement | null>(null);
  const shouldFocusErrorsRef = useRef(false);

  useEffect(() => {
    return () => {
      abortRef.current?.abort();
    };
  }, []);

  useEffect(() => {
    if (!shouldFocusErrorsRef.current || !hasPropertyFormErrors(errors)) {
      return;
    }
    errorSummaryRef.current?.focus();
    shouldFocusErrorsRef.current = false;
  }, [errors]);

  useEffect(() => {
    if (confirmingNewCreate) {
      confirmTitleRef.current?.focus();
    }
  }, [confirmingNewCreate]);

  function updateField(field: PropertyFormField, value: string): void {
    setValues((current) => ({ ...current, [field]: value }));
    setErrors((current) => {
      if (current[field] === undefined) {
        return current;
      }
      const next = { ...current };
      delete next[field];
      return next;
    });
  }

  async function submitCreate(): Promise<void> {
    if (creatingRef.current) {
      return;
    }

    const nextErrors = validateCreatePropertyForm(values);
    if (hasPropertyFormErrors(nextErrors)) {
      shouldFocusErrorsRef.current = true;
      setErrors(nextErrors);
      setGeneralProblem(null);
      return;
    }

    creatingRef.current = true;
    setErrors({});
    setGeneralProblem(null);
    setPhase('creating');
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      const result = await propertyApi.create(
        { name: values.name, location: values.location },
        values.hostReferenceId,
        controller.signal,
      );

      if (controller.signal.aborted) {
        return;
      }

      if (result.ok) {
        creatingRef.current = false;
        setCreatedProperty(result.data);
        setPhase('created');
        setConfirmingNewCreate(false);
        return;
      }

      creatingRef.current = false;
      setPhase('idle');
      if (result.problem.status === 400 && result.problem.details.length > 0) {
        shouldFocusErrorsRef.current = true;
        setErrors(mapProblemDetailsToFieldErrors(result.problem.details));
      }
      if (result.problem.status !== 400 || result.problem.details.length === 0) {
        setGeneralProblem(result.problem);
      }
    } catch (error) {
      creatingRef.current = false;
      if (isAbortError(error) || controller.signal.aborted) {
        return;
      }
      throw error;
    }
  }

  function confirmNewCreate(): void {
    creatingRef.current = false;
    setConfirmingNewCreate(false);
    setCreatedProperty(null);
    setPhase('idle');
    setGeneralProblem(null);
    setErrors({});
  }

  return (
    <section className="property-workspace" lang="pt-BR" aria-labelledby="property-workspace-heading">
      <h1 id="property-workspace-heading">Cadastro de hospedagem</h1>
      <p className="property-workspace__lead">
        Informe um UUID fictício de Host, o nome e a localização textual. O cadastro inicia como ativo.
      </p>

      {phase === 'creating' ? (
        <OperationFeedback
          role="status"
          title="Cadastrando hospedagem"
          message="Aguarde. O envio está em andamento e não será repetido automaticamente."
        />
      ) : null}

      {phase === 'created' && createdProperty !== null ? (
        <OperationFeedback
          role="status"
          title="Cadastro confirmado"
          message="A hospedagem foi registrada. O identificador abaixo pode ser copiado."
        />
      ) : null}

      {generalProblem !== null ? (
        <OperationFeedback
          role="alert"
          title="Não foi possível cadastrar a hospedagem"
          message={generalProblem.detail}
          traceId={generalProblem.traceId}
        />
      ) : null}

      {phase === 'created' && createdProperty !== null ? (
        <CreatedPropertySummary
          property={createdProperty}
          onRequestNewCreate={() => setConfirmingNewCreate(true)}
        />
      ) : (
        <CreatePropertyForm
          values={values}
          errors={errors}
          submitting={phase === 'creating'}
          errorSummaryRef={errorSummaryRef}
          onFieldChange={updateField}
          onSubmit={() => {
            void submitCreate();
          }}
        />
      )}

      {confirmingNewCreate ? (
        <div
          className="property-workspace__confirm"
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="replace-create-title"
          aria-describedby="replace-create-description"
        >
          <h2 id="replace-create-title" ref={confirmTitleRef} tabIndex={-1}>
            Substituir o cadastro desta sessão?
          </h2>
          <p id="replace-create-description">
            Uma nova criação substitui o resumo atual nesta tela. Nada é apagado no Catalog.
          </p>
          <div className="property-workspace__confirm-actions">
            <button type="button" onClick={confirmNewCreate}>
              Confirmar nova criação
            </button>
            <button type="button" onClick={() => setConfirmingNewCreate(false)}>
              Manter resumo
            </button>
          </div>
        </div>
      ) : null}
    </section>
  );
}
