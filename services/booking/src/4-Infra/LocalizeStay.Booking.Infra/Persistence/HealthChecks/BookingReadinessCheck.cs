using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalizeStay.Booking.Infra.Persistence.HealthChecks;

public sealed class BookingReadinessCheck(BookingDbContext dbContext) : IHealthCheck
{
    private readonly BookingDbContext _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        _ = context;

        try
        {
            if (!await _dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return HealthCheckResult.Unhealthy("Não foi possível conectar ao Postgres do Booking.");
            }

            _ = await _dbContext.BootstrapChecks.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Postgres do Booking indisponível.", exception);
        }
    }
}
