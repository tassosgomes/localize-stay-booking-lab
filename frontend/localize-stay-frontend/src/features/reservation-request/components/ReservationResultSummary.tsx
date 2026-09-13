import type { Ref } from 'react';
import type { Reservation } from '../api/reservationApi.ts';

interface ReservationResultSummaryProps {
  reservation: Reservation;
  headingRef: Ref<HTMLHeadingElement>;
  onNewRequest: () => void;
}

// Snapshot congelado da Reservation criada (201): exibe exatamente os valores
// retornados pelo contrato — preço/moeda/total são garantidos no momento da
// solicitação e não mudam até o pagamento (F03, fora do escopo desta feature).
export function ReservationResultSummary({ reservation, headingRef, onNewRequest }: ReservationResultSummaryProps) {
  return (
    <section className="reservation-card reservation-result-card" aria-labelledby="reservation-result-title">
      {/* tabIndex={-1} permite mover o foco para o título após o 201 sem
          retirar o controle do teclado (WCAG 2.1 AA). */}
      <div className="reservation-result-card__header">
        <h2 id="reservation-result-title" className="reservation-result-card__title" ref={headingRef} tabIndex={-1}>
          Reserva solicitada
        </h2>
        <p className="reservation-result-card__subtitle">
          Os parâmetros e valores abaixo foram congelados com sucesso no momento da solicitação.
        </p>
      </div>
      <dl className="reservation-detail-dl">
        <dt>ID da reserva</dt>
        <dd><code className="reservation-code-pill">{reservation.id}</code></dd>
        <dt>Status</dt>
        <dd><span className="reservation-status-tag reservation-status-tag--solicitada">{reservation.status}</span></dd>
        <dt>Acomodação</dt>
        <dd><code className="reservation-code-pill">{reservation.accommodationId}</code></dd>
        <dt>Guest de referência</dt>
        <dd>{reservation.guestReference}</dd>
        <dt>Período</dt>
        <dd>
          {reservation.checkIn} a {reservation.checkOut}
        </dd>
        <dt>Hóspedes</dt>
        <dd>{reservation.guestsCount}</dd>
        <dt className="reservation-detail-dl__price-term">Diária</dt>
        <dd className="reservation-detail-dl__price-value">
          {reservation.pricePerNight} {reservation.currency}
        </dd>
        <dt className="reservation-detail-dl__total-term">Total</dt>
        <dd className="reservation-detail-dl__total-value">
          {reservation.totalAmount} {reservation.currency}
        </dd>
      </dl>
      <div className="reservation-result-card__notice">
        <p>O pagamento ainda não foi solicitado.</p>
      </div>
      <div className="reservation-result-card__actions">
        <button type="button" className="reservation-button-secondary" onClick={onNewRequest}>
          Nova solicitação
        </button>
      </div>
    </section>
  );
}
