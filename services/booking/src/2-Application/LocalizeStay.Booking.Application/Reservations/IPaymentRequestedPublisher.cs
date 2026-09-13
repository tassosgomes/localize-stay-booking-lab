using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Application.Reservations;

// Porta de publicação do evento de integração booking.payment_requested
// (F03, ADR-002). Implementada pela Infra sobre Rmq.CloudEvents, mesmo padrão
// de IReservationRequestedPublisher.
public interface IPaymentRequestedPublisher
{
    Task PublishAsync(Reservation reservation, CancellationToken cancellationToken);
}
