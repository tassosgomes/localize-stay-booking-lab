using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Application.Reservations;

public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);

    Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken);

    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Reservation?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken);
}
