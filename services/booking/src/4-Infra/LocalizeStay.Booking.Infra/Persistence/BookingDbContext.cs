using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Booking.Infra.Persistence;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    internal DbSet<BootstrapCheck> BootstrapChecks => Set<BootstrapCheck>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("booking");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
    }
}
