using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Application.Reservations;

// Porta de publicação do evento de integração booking.reservation_requested
// (ADR-002). Implementada pela Infra sobre Rmq.CloudEvents.
public interface IReservationRequestedPublisher
{
    Task PublishAsync(Reservation reservation, CancellationToken cancellationToken);
}
