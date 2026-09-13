import type { Ref } from 'react';
import type { ReservationDetail } from '../api/reservationDetailApi.ts';

interface ReservationDetailViewProps {
  reservation: ReservationDetail;
  headingRef: Ref<HTMLHeadingElement>;
}

// Snapshot congelado da Reservation consultada (200): exibe exatamente os
// valores retornados pelo contrato — dados congelados, estado, situação da
// saga, correlação e motivo de cancelamento só quando presente (nunca um
// `dt`/`dd` vazio para os demais estados — techspec §Acessibilidade).
export function ReservationDetailView({ reservation, headingRef }: ReservationDetailViewProps) {
  return (
    <section className="reservation-card reservation-detail-card" aria-labelledby="reservation-detail-title">
      {/* tabIndex={-1} permite mover o foco para o título após o 200 sem
          retirar o controle do teclado (WCAG 2.1 AA), mesmo padrão de
          ReservationResultSummary (F01). */}
      <div className="reservation-detail-card__header">
        <h2 id="reservation-detail-title" className="reservation-detail-card__title" ref={headingRef} tabIndex={-1}>
          Reserva encontrada
        </h2>
        <p className="reservation-detail-card__subtitle">
          Detalhes completos e situação atual da reserva no sistema.
        </p>
      </div>
      <dl className="reservation-detail-dl">
        <dt>ID da reserva</dt>
        <dd><code className="reservation-code-pill">{reservation.id}</code></dd>
        <dt>Status</dt>
        <dd>
          <span className={`reservation-status-tag reservation-status-tag--${reservation.status}`}>
            {reservation.status}
          </span>
        </dd>
        <dt>Situação do pagamento</dt>
        <dd>
          <span className={`reservation-saga-tag reservation-saga-tag--${reservation.sagaStatus}`}>
            {reservation.sagaStatus}
          </span>
        </dd>
        <dt>Identificador de correlação</dt>
        <dd><code className="reservation-code-pill">{reservation.correlationId}</code></dd>
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
        {reservation.cancellationReason ? (
          <>
            <dt className="reservation-detail-dl__cancellation-term">Motivo do cancelamento</dt>
            <dd className="reservation-detail-dl__cancellation-value">
              {reservation.cancellationReason}
            </dd>
          </>
        ) : null}
      </dl>
    </section>
  );
}
