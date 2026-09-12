import { useEffect, useRef, useState } from 'react';
import { CreatePropertyForm } from '../components/CreatePropertyForm.tsx';
import { CreatedPropertySummary } from '../components/CreatedPropertySummary.tsx';
import { EditPropertyForm } from '../components/EditPropertyForm.tsx';
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
  EDIT_REQUIRES_CHANGE_MESSAGE,
  buildPropertyUpdateRequest,
  hasPropertyFormErrors,
  mapProblemDetailsToFieldErrors,
  validateCreatePropertyForm,
  validateEditPropertyForm,
} from '../validation/propertyFormValidation.ts';

type WorkspacePhase = 'idle' | 'creating' | 'created' | 'editing' | 'updating';
type ConfirmationKind = 'created' | 'updated';

function isAbortError(error: unknown): boolean {
  return (
    (error instanceof DOMException && error.name === 'AbortError') ||
    (error instanceof Error && error.name === 'AbortError')
  );
}

function problemTitle(problem: NormalizedProblem, action: 'create' | 'update'): string {
  if (problem.status === 403) {
    return 'Operação não permitida';
  }
  if (problem.status === 404) {
    return 'Hospedagem não encontrada';
  }
  if (action === 'update') {
    return 'Não foi possível editar a hospedagem';
  }
  return 'Não foi possível cadastrar a hospedagem';
}

function emptyProblem(detail: string): NormalizedProblem {
  return {
    kind: 'unexpected',
    status: null,
    code: null,
    type: null,
    title: null,
    detail,
    instance: null,
    details: [],
    traceId: null,
  };
}

export function PropertyWorkspacePage() {
  const [values, setValues] = useState<PropertyFormValues>(EMPTY_PROPERTY_FORM_VALUES);
  const [errors, setErrors] = useState<PropertyFormErrors>({});
  const [phase, setPhase] = useState<WorkspacePhase>('idle');
  const [createdProperty, setCreatedProperty] = useState<Property | null>(null);
  const [generalProblem, setGeneralProblem] = useState<NormalizedProblem | null>(null);
  const [confirmation, setConfirmation] = useState<ConfirmationKind | null>(null);
  const [confirmingNewCreate, setConfirmingNewCreate] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const inFlightRef = useRef(false);
  const errorSummaryRef = useRef<HTMLDivElement | null>(null);
  const confirmTitleRef = useRef<HTMLHeadingElement | null>(null);
  const editTitleRef = useRef<HTMLHeadingElement | null>(null);
  const shouldFocusErrorsRef = useRef(false);
  const shouldFocusEditTitleRef = useRef(false);

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

  useEffect(() => {
    if (phase === 'editing' && shouldFocusEditTitleRef.current) {
      editTitleRef.current?.focus();
      shouldFocusEditTitleRef.current = false;
    }
  }, [phase]);

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
    setGeneralProblem(null);
  }

  async function submitCreate(): Promise<void> {
    if (inFlightRef.current) {
      return;
    }

    const nextErrors = validateCreatePropertyForm(values);
    if (hasPropertyFormErrors(nextErrors)) {
      shouldFocusErrorsRef.current = true;
      setErrors(nextErrors);
      setGeneralProblem(null);
      return;
    }

    inFlightRef.current = true;
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
        inFlightRef.current = false;
        setCreatedProperty(result.data);
        setPhase('created');
        setConfirmation('created');
        setConfirmingNewCreate(false);
        return;
      }

      inFlightRef.current = false;
      setPhase('idle');
      if (result.problem.status === 400 && result.problem.details.length > 0) {
        shouldFocusErrorsRef.current = true;
        setErrors(mapProblemDetailsToFieldErrors(result.problem.details));
      }
      if (result.problem.status !== 400 || result.problem.details.length === 0) {
        setGeneralProblem(result.problem);
      }
    } catch (error) {
      inFlightRef.current = false;
      if (isAbortError(error) || controller.signal.aborted) {
        return;
      }
      throw error;
    }
  }

  function startEditing(): void {
    if (createdProperty === null || inFlightRef.current) {
      return;
    }

    setErrors({});
    setGeneralProblem(null);
    setConfirmation(null);
    setValues({
      hostReferenceId: createdProperty.hostReferenceId,
      name: createdProperty.name,
      location: createdProperty.location,
    });
    shouldFocusEditTitleRef.current = true;
    setPhase('editing');
  }

  function cancelEditing(): void {
    if (inFlightRef.current) {
      return;
    }
    setErrors({});
    setGeneralProblem(null);
    setPhase('created');
  }

  async function submitUpdate(): Promise<void> {
    if (inFlightRef.current || createdProperty === null) {
      return;
    }

    const nextErrors = validateEditPropertyForm(values, createdProperty);
    if (hasPropertyFormErrors(nextErrors)) {
      shouldFocusErrorsRef.current = true;
      setErrors(nextErrors);
      setGeneralProblem(null);
      return;
    }

    const changes = buildPropertyUpdateRequest(createdProperty, values);
    if (changes === null) {
      setErrors({});
      setGeneralProblem(emptyProblem(EDIT_REQUIRES_CHANGE_MESSAGE));
      return;
    }

    inFlightRef.current = true;
    setErrors({});
    setGeneralProblem(null);
    setPhase('updating');
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      const result = await propertyApi.update(
        createdProperty.id,
        changes,
        values.hostReferenceId,
        controller.signal,
      );

      if (controller.signal.aborted) {
        return;
      }

      if (result.ok) {
        inFlightRef.current = false;
        setCreatedProperty(result.data);
        setPhase('created');
        setConfirmation('updated');
        return;
      }

      inFlightRef.current = false;
      setPhase('editing');
      if (result.problem.status === 400 && result.problem.details.length > 0) {
        shouldFocusErrorsRef.current = true;
        setErrors(mapProblemDetailsToFieldErrors(result.problem.details));
      }
      if (result.problem.status !== 400 || result.problem.details.length === 0) {
        setGeneralProblem(result.problem);
      }
    } catch (error) {
      inFlightRef.current = false;
      if (isAbortError(error) || controller.signal.aborted) {
        return;
      }
      throw error;
    }
  }

  function confirmNewCreate(): void {
    abortRef.current?.abort();
    inFlightRef.current = false;
    setConfirmingNewCreate(false);
    setCreatedProperty(null);
    setPhase('idle');
    setConfirmation(null);
    setGeneralProblem(null);
    setErrors({});
  }

  const showSummary = createdProperty !== null && phase !== 'idle' && phase !== 'creating';
  const showEditForm = createdProperty !== null && (phase === 'editing' || phase === 'updating');
  const updateAction = generalProblem !== null && (phase === 'editing' || phase === 'updating');

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

      {phase === 'updating' ? (
        <OperationFeedback
          role="status"
          title="Atualizando hospedagem"
          message="Aguarde. O envio está em andamento e não será repetido automaticamente."
        />
      ) : null}

      {phase === 'created' && confirmation === 'created' ? (
        <OperationFeedback
          role="status"
          title="Cadastro confirmado"
          message="A hospedagem foi registrada. O identificador abaixo pode ser copiado."
        />
      ) : null}

      {phase === 'created' && confirmation === 'updated' ? (
        <OperationFeedback
          role="status"
          title="Alteração confirmada"
          message="Os dados cadastrais foram atualizados. Identificador, Host responsável e status permaneceram os mesmos."
        />
      ) : null}

      {generalProblem !== null ? (
        <OperationFeedback
          role="alert"
          title={problemTitle(generalProblem, updateAction ? 'update' : 'create')}
          message={generalProblem.detail}
          traceId={generalProblem.traceId}
        />
      ) : null}

      {showSummary && createdProperty !== null ? (
        <CreatedPropertySummary
          property={createdProperty}
          showEditAction={phase === 'created'}
          onStartEditing={startEditing}
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

      {showEditForm ? (
        <EditPropertyForm
          values={values}
          errors={errors}
          submitting={phase === 'updating'}
          titleRef={editTitleRef}
          errorSummaryRef={errorSummaryRef}
          onFieldChange={updateField}
          onSubmit={() => {
            void submitUpdate();
          }}
          onCancel={cancelEditing}
        />
      ) : null}

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
