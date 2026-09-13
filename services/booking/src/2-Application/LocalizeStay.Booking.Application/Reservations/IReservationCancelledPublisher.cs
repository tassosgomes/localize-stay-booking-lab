using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Application.Reservations;

// Porta de publicação do evento de integração booking.reservation_cancelled
// (F04, ADR-002). Implementada pela Infra sobre Rmq.CloudEvents.
public interface IReservationCancelledPublisher
{
    Task PublishAsync(Reservation reservation, CancellationToken cancellationToken);
}
