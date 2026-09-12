using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalizeStay.Payment.Infra.Persistence.HealthChecks;

public sealed class PaymentReadinessCheck(PaymentDbContext dbContext) : IHealthCheck
{
    private readonly PaymentDbContext _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        _ = context;

        try
        {
            if (!await _dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return HealthCheckResult.Unhealthy("Não foi possível conectar ao Postgres do Payment.");
            }

            _ = await _dbContext.BootstrapChecks.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Postgres do Payment indisponível.", exception);
        }
    }
}
