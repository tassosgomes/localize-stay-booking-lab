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
    <section aria-labelledby="reservation-detail-title">
      {/* tabIndex={-1} permite mover o foco para o título após o 200 sem
          retirar o controle do teclado (WCAG 2.1 AA), mesmo padrão de
          ReservationResultSummary (F01). */}
      <h2 id="reservation-detail-title" ref={headingRef} tabIndex={-1}>
        Reserva encontrada
      </h2>
      <dl>
        <dt>ID da reserva</dt>
        <dd>{reservation.id}</dd>
        <dt>Status</dt>
        <dd>{reservation.status}</dd>
        <dt>Situação do pagamento</dt>
        <dd>{reservation.sagaStatus}</dd>
        <dt>Identificador de correlação</dt>
        <dd>{reservation.correlationId}</dd>
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
        {reservation.cancellationReason ? (
          <>
            <dt>Motivo do cancelamento</dt>
            <dd>{reservation.cancellationReason}</dd>
          </>
        ) : null}
      </dl>
    </section>
  );
}
