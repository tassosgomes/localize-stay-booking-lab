using LocalizeStay.Catalog.Infra.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace LocalizeStay.Catalog.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("localize_stay")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _dbContainer.GetConnectionString();

    public CustomWebApplicationFactory()
    {
        // Placeholder só para o boot do host em teste: AddPersistenceConfiguration
        // exige uma ConnectionStrings:Catalog não vazia, e ConfigureTestServices
        // substitui o DbContext pela string real do container abaixo.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Catalog",
            "Host=localhost;Port=5432;Database=localize_stay;Username=postgres;Password=postgres");
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "catalog"))
            .Options;

        await using var dbContext = new CatalogDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await base.DisposeAsync();
    }

    public Task StopDatabaseAsync() => _dbContainer.StopAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                service => service.ServiceType == typeof(DbContextOptions<CatalogDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<CatalogDbContext>(options =>
                options.UseNpgsql(ConnectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "catalog")));
        });
    }
}
