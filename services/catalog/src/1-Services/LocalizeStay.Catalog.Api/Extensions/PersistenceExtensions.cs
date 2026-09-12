using LocalizeStay.Catalog.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Catalog.Api.Extensions;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistenceConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Catalog");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Catalog não configurada. Defina via 'dotnet user-secrets set \"ConnectionStrings:Catalog\" \"<connection-string>\"'.");
        }

        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "catalog")));

        return services;
    }
}
