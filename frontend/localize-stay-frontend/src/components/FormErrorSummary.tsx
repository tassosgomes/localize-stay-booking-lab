import { useId, type Ref } from 'react';

export interface FormErrorSummaryItem {
  label?: string;
  message: string;
}

interface FormErrorSummaryProps {
  title: string;
  items: FormErrorSummaryItem[];
  ref?: Ref<HTMLDivElement>;
}

// Resumo acessível de erros de formulário (WCAG 2.1 AA): role="alert" anuncia
// a falha imediatamente; tabIndex={-1} permite mover o foco programaticamente
// após um submit inválido. O erro não depende apenas de cor — cada item carrega
// label do campo e mensagem em texto.
export function FormErrorSummary({ title, items, ref }: FormErrorSummaryProps) {
  const titleId = useId();
  return (
    <div
      ref={ref}
      className="form-error-summary"
      role="alert"
      tabIndex={-1}
      aria-labelledby={titleId}
    >
      <p id={titleId} className="form-error-summary__title">
        <strong>{title}</strong>
      </p>
      {items.length > 0 ? (
        <ul className="form-error-summary__list">
          {items.map((item) => (
            <li key={`${item.label ?? ''}-${item.message}`}>
              {item.label ? (
                <>
                  <strong>{item.label}</strong>: {item.message}
                </>
              ) : (
                item.message
              )}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
