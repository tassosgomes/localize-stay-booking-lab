using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Payment.Infra.Persistence;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    internal DbSet<BootstrapCheck> BootstrapChecks => Set<BootstrapCheck>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payment");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
