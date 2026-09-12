using System.Net;
using LocalizeStay.Booking.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LocalizeStay.Booking.IntegrationTests;

[Collection("BookingIntegrationTests")]
public sealed class HealthCheckTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Migration_creates_reservation_tables_and_removes_bootstrap_sentinel()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        // Act
        var reservationTables = await dbContext.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM information_schema.tables WHERE table_schema = 'booking' AND table_name IN ('reservations', 'reservation_sagas')").SingleAsync();
        var sentinelTables = await dbContext.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM information_schema.tables WHERE table_schema = 'booking' AND table_name = '__bootstrap_check'").SingleAsync();

        // Assert
        Assert.Equal(2, reservationTables);
        Assert.Equal(0, sentinelTables);
    }

    [Fact]
    public async Task Ready_returns_healthy_when_postgres_is_available()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Live_returns_healthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_returns_error_and_live_stays_healthy_when_postgres_is_unavailable()
    {
        // Arrange: fábrica própria para derrubar o Postgres sem afetar os demais testes.
        var isolatedFactory = new CustomWebApplicationFactory();
        await isolatedFactory.InitializeAsync();
        try
        {
            var client = isolatedFactory.CreateClient();

            // Sanidade: com o banco no ar, ready é 200.
            var healthyResponse = await client.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.OK, healthyResponse.StatusCode);

            // Act: derruba o Postgres do container.
            await isolatedFactory.StopDatabaseAsync();
            var readyResponse = await client.GetAsync("/health/ready");
            var liveResponse = await client.GetAsync("/health/live");

            // Assert
            Assert.NotEqual(HttpStatusCode.OK, readyResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        }
        finally
        {
            await isolatedFactory.DisposeAsync();
        }
    }
}
