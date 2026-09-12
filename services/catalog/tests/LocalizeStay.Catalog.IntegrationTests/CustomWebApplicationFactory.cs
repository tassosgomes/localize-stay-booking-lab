using LocalizeStay.Catalog.Application.Abstractions.Persistence;
using LocalizeStay.Catalog.Infra.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    private readonly List<string> _logMessages = [];
    private readonly object _logSync = new();

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

    public async Task ResetCatalogDataAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await dbContext.Properties.ExecuteDeleteAsync();
        ClearLogs();
    }

    public IReadOnlyList<string> SnapshotLogs()
    {
        lock (_logSync)
        {
            return [.. _logMessages];
        }
    }

    public WebApplicationFactory<Program> WithThrowingUnitOfWork() =>
        WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IUnitOfWork)).ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddScoped<IUnitOfWork, ThrowingUnitOfWork>();
            });
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(new CollectingLoggerProvider(AddLog));
        });

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

    private void AddLog(string message)
    {
        lock (_logSync)
        {
            _logMessages.Add(message);
        }
    }

    private void ClearLogs()
    {
        lock (_logSync)
        {
            _logMessages.Clear();
        }
    }

    private sealed class ThrowingUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Controlled failure for 500 test.");
    }

    private sealed class CollectingLoggerProvider(Action<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, sink);

        public void Dispose()
        {
        }
    }

    private sealed class CollectingLogger(string categoryName, Action<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            sink($"{categoryName}: {formatter(state, exception)}");
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
