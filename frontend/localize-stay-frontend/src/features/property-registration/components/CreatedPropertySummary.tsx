import { useState } from 'react';
import type { Property } from '../api/propertyApi.ts';

export interface CreatedPropertySummaryProps {
  property: Property;
  showEditAction: boolean;
  onStartEditing: () => void;
  onRequestNewCreate: () => void;
}

export function CreatedPropertySummary({
  property,
  showEditAction,
  onStartEditing,
  onRequestNewCreate,
}: CreatedPropertySummaryProps) {
  const [copyFeedback, setCopyFeedback] = useState<string | null>(null);

  async function copyId(): Promise<void> {
    const input = document.getElementById('created-property-id');
    if (input instanceof HTMLInputElement) {
      input.focus();
      input.select();
    }

    try {
      const clipboard = navigator.clipboard;
      if (clipboard !== undefined && typeof clipboard.writeText === 'function') {
        await clipboard.writeText(property.id);
        setCopyFeedback('Identificador copiado.');
        return;
      }
    } catch {
      // The readonly field remains selectable when the Clipboard API is unavailable.
    }

    setCopyFeedback('Identificador selecionado. Copie com o teclado.');
  }

  return (
    <section className="created-property-summary" aria-labelledby="created-property-heading">
      <h2 id="created-property-heading">Hospedagem cadastrada</h2>
      <dl className="created-property-summary__list">
        <div>
          <dt>
            <label htmlFor="created-property-id">Identificador</label>
          </dt>
          <dd className="created-property-summary__id">
            <input id="created-property-id" readOnly value={property.id} />
            <button type="button" onClick={() => void copyId()}>
              Copiar identificador
            </button>
          </dd>
        </div>
        <div>
          <dt>Host responsável</dt>
          <dd>{property.hostReferenceId}</dd>
        </div>
        <div>
          <dt>Nome da hospedagem</dt>
          <dd>{property.name}</dd>
        </div>
        <div>
          <dt>Localização</dt>
          <dd>{property.location}</dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd>{property.status}</dd>
        </div>
      </dl>
      {copyFeedback !== null ? (
        <p role="status">{copyFeedback}</p>
      ) : null}
      <div className="created-property-summary__actions">
        {showEditAction ? (
          <button type="button" className="created-property-summary__edit" onClick={onStartEditing}>
            Editar hospedagem
          </button>
        ) : null}
        <button type="button" className="created-property-summary__new" onClick={onRequestNewCreate}>
          Cadastrar outra hospedagem
        </button>
      </div>
    </section>
  );
}
