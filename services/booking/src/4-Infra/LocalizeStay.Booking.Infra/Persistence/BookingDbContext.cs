using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Booking.Infra.Persistence;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<ReservationSaga> ReservationSagas => Set<ReservationSaga>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("booking");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
    }
}
