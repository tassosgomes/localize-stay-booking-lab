import { useId, type ReactNode } from 'react';

export interface OperationFeedbackProps {
  role: 'status' | 'alert';
  title: string;
  message: string;
  traceId?: string | null;
  children?: ReactNode;
}

export function OperationFeedback({
  role,
  title,
  message,
  traceId = null,
  children,
}: OperationFeedbackProps) {
  const titleId = useId();

  return (
    <div
      className={`operation-feedback operation-feedback--${role}`}
      role={role}
      aria-labelledby={titleId}
    >
      <p id={titleId} className="operation-feedback__title">
        {title}
      </p>
      <p>{message}</p>
      {traceId !== null && traceId !== '' ? (
        <p>
          Identificador de correlação: <code>{traceId}</code>
        </p>
      ) : null}
      {children}
    </div>
  );
}
