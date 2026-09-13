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

    // Update() explícito (decisão fechada da TechSpec), não apenas
    // SaveChangesAsync sobre a entidade já rastreada — a Reservation pode
    // chegar aqui vinda de um DbContext diferente do que a persistiu (mesmo
    // request, mas rastreamento não garantido entre os dois blocos best-effort
    // do handler).
    public async Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        dbContext.Update(reservation);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
