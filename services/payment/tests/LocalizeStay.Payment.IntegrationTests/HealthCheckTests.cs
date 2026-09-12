using System.Net;
using LocalizeStay.Payment.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LocalizeStay.Payment.IntegrationTests;

[Collection("PaymentIntegrationTests")]
public sealed class HealthCheckTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Migration_applies_bootstrap_check_table_in_payment_schema()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        // Act
        var matchingTables = await dbContext.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM information_schema.tables WHERE table_schema = 'payment' AND table_name = '__bootstrap_check'").SingleAsync();

        // Assert
        Assert.Equal(1, matchingTables);
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
