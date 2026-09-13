using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Application.Reservations;

// Porta de publicação do evento de integração booking.reservation_confirmed
// (F04, ADR-002). Implementada pela Infra sobre Rmq.CloudEvents.
public interface IReservationConfirmedPublisher
{
    Task PublishAsync(Reservation reservation, CancellationToken cancellationToken);
}
