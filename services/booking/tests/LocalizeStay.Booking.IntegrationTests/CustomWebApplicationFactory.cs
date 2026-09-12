using System.Net.Http.Headers;
using System.Text;
using LocalizeStay.Booking.Infra.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace LocalizeStay.Booking.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("localize_stay")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    // Broker real (efêmero) para o publisher de diagnóstico e o health check de
    // RabbitMQ: mesma imagem do broker compartilhado do homelab.
    private readonly RabbitMqContainer _rabbitContainer = new RabbitMqBuilder("rabbitmq:4.3.5-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .WithPortBinding(15672, true)
        .Build();

    public string ConnectionString => _dbContainer.GetConnectionString();

    public CustomWebApplicationFactory()
    {
        // Placeholder só para o boot do host em teste: AddPersistenceConfiguration
        // exige uma ConnectionStrings:Booking não vazia, e ConfigureTestServices
        // substitui o DbContext pela string real do container abaixo.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Booking",
            "Host=localhost;Port=5432;Database=localize_stay;Username=postgres;Password=postgres");
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "booking"))
            .Options;

        await using var dbContext = new BookingDbContext(options);
        await dbContext.Database.MigrateAsync();

        await _rabbitContainer.StartAsync();
        await CreateLocalizeStayVhostAsync();

        // Boot do host lê de variáveis de ambiente (ConfigureAppConfiguration não
        // alcança o WebApplication.CreateBuilder); o host só boota no primeiro uso,
        // após este InitializeAsync, então os valores do container valem.
        Environment.SetEnvironmentVariable("RabbitMQ__HostName", _rabbitContainer.Hostname);
        Environment.SetEnvironmentVariable(
            "RabbitMQ__Port",
            _rabbitContainer.GetMappedPublicPort(5672).ToString());
        Environment.SetEnvironmentVariable("RabbitMQ__UserName", "guest");
        Environment.SetEnvironmentVariable("RabbitMQ__Password", "guest");
        Environment.SetEnvironmentVariable("RabbitMQ__VirtualHost", "/localize-stay");
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await _rabbitContainer.StopAsync();
        await base.DisposeAsync();
    }

    public Task StopDatabaseAsync() => _dbContainer.StopAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                service => service.ServiceType == typeof(DbContextOptions<BookingDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<BookingDbContext>(options =>
                options.UseNpgsql(ConnectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "booking")));
        });
    }

    private async Task CreateLocalizeStayVhostAsync()
    {
        using var management = new HttpClient
        {
            BaseAddress = new Uri($"http://{_rabbitContainer.Hostname}:{_rabbitContainer.GetMappedPublicPort(15672)}/")
        };

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("guest:guest"));
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var vhostResponse = await management.PutAsync(
            "api/vhosts/%2Flocalize-stay",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        vhostResponse.EnsureSuccessStatusCode();

        using var permissionsResponse = await management.PutAsync(
            "api/permissions/%2Flocalize-stay/guest",
            new StringContent(
                """{"configure":".*","write":".*","read":".*"}""",
                Encoding.UTF8,
                "application/json"));
        permissionsResponse.EnsureSuccessStatusCode();
    }
}
