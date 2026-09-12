using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalizeStay.Catalog.Infra.Persistence.HealthChecks;

public sealed class CatalogReadinessCheck(CatalogDbContext dbContext) : IHealthCheck
{
    private readonly CatalogDbContext _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        _ = context;

        try
        {
            if (!await _dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return HealthCheckResult.Unhealthy("Não foi possível conectar ao Postgres do Catalog.");
            }

            _ = await _dbContext.Properties.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Postgres do Catalog indisponível.", exception);
        }
    }
}
