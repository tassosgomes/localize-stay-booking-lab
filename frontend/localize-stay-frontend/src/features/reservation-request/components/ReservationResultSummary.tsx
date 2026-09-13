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
    <section aria-labelledby="reservation-result-title">
      {/* tabIndex={-1} permite mover o foco para o título após o 201 sem
          retirar o controle do teclado (WCAG 2.1 AA). */}
      <h2 id="reservation-result-title" ref={headingRef} tabIndex={-1}>
        Reserva solicitada
      </h2>
      <dl>
        <dt>ID da reserva</dt>
        <dd>{reservation.id}</dd>
        <dt>Status</dt>
        <dd>{reservation.status}</dd>
        <dt>Acomodação</dt>
        <dd>{reservation.accommodationId}</dd>
        <dt>Guest de referência</dt>
        <dd>{reservation.guestReference}</dd>
        <dt>Período</dt>
        <dd>
          {reservation.checkIn} a {reservation.checkOut}
        </dd>
        <dt>Hóspedes</dt>
        <dd>{reservation.guestsCount}</dd>
        <dt>Diária</dt>
        <dd>
          {reservation.pricePerNight} {reservation.currency}
        </dd>
        <dt>Total</dt>
        <dd>
          {reservation.totalAmount} {reservation.currency}
        </dd>
      </dl>
      <p>O pagamento ainda não foi solicitado.</p>
      <button type="button" onClick={onNewRequest}>
        Nova solicitação
      </button>
    </section>
  );
}
