using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Infra.Persistence;

public sealed class ReservationRepository(BookingDbContext dbContext) : IReservationRepository
{
    public async Task AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        await dbContext.Reservations.AddAsync(reservation, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
