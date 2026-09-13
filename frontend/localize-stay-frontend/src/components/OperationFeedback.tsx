import type { ReactNode } from 'react';

export type OperationFeedbackTone = 'neutral' | 'temporary' | 'error';

interface OperationFeedbackProps {
  tone: OperationFeedbackTone;
  children: ReactNode;
}

// Feedback de operação (WCAG 2.1 AA): role="status" (polite) para loading,
// sucesso e falha temporária (503 — não é erro); role="alert" (assertive)
// apenas para erro real (500/rede). O tom textual diferencia "corrija seu
// pedido" de "tente novamente mais tarde" — distinção central de RF-01.
export function OperationFeedback({ tone, children }: OperationFeedbackProps) {
  return (
    <p
      className={`operation-feedback operation-feedback--${tone}`}
      role={tone === 'error' ? 'alert' : 'status'}
      data-tone={tone}
    >
      {children}
    </p>
  );
}
