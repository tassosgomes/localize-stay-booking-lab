using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Infra.Catalog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LocalizeStay.Booking.IntegrationTests.Catalog;

// V-02: prova somente o mecanismo de comunicação com Catalog (200/404/falha) —
// nenhuma regra de negócio de Booking é aplicada nesta camada.
public sealed class CatalogAvailabilityClientTests : IAsyncLifetime
{
    private const string AccommodationId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

    private FakeCatalogServerFactory? _server;
    private ServiceProvider? _provider;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }

    [Fact]
    public async Task CheckAvailabilityAsync_catalog_responde_200_retorna_availability_facts()
    {
        var client = await CreateClientAsync(FakeCatalogBehavior.Ok);

        var facts = await client.CheckAvailabilityAsync(
            Guid.Parse(AccommodationId),
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 13),
            2,
            CancellationToken.None);

        Assert.NotNull(facts);
        Assert.True(facts.Active);
        Assert.Equal(4, facts.MaxGuests);
        Assert.True(facts.AvailableForPeriod);
        Assert.Equal(350.00m, facts.PricePerNight);
        Assert.Equal("BRL", facts.Currency);

        // Prova o formato exato do request contratado com Catalog.
        Assert.Equal(1, _server!.RequestsReceived);
        Assert.Equal(
            $"/v1/accommodations/{AccommodationId}/availability-check" +
            "?checkIn=2026-10-10&checkOut=2026-10-13&guestsCount=2",
            _server.LastRequestPathAndQuery);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_catalog_responde_404_retorna_null()
    {
        var client = await CreateClientAsync(FakeCatalogBehavior.NotFound);

        var facts = await client.CheckAvailabilityAsync(
            Guid.Parse(AccommodationId),
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 13),
            2,
            CancellationToken.None);

        Assert.Null(facts);
        Assert.Equal(1, _server!.RequestsReceived);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_catalog_responde_500_lanca_catalog_unavailable_exception()
    {
        var client = await CreateClientAsync(FakeCatalogBehavior.ServerError);

        var exception = await Assert.ThrowsAsync<CatalogUnavailableException>(() =>
            client.CheckAvailabilityAsync(
                Guid.Parse(AccommodationId),
                new DateOnly(2026, 10, 10),
                new DateOnly(2026, 10, 13),
                2,
                CancellationToken.None));

        Assert.Contains("500", exception.Message);
        Assert.Equal(1, _server!.RequestsReceived);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_catalog_expira_timeout_lanca_catalog_unavailable_exception_sem_retry()
    {
        var client = await CreateClientAsync(FakeCatalogBehavior.Timeout);

        await Assert.ThrowsAsync<CatalogUnavailableException>(() =>
            client.CheckAvailabilityAsync(
                Guid.Parse(AccommodationId),
                new DateOnly(2026, 10, 10),
                new DateOnly(2026, 10, 13),
                2,
                CancellationToken.None));

        // Sem retry: exatamente uma chamada HTTP mesmo em cenário de falha.
        Assert.Equal(1, _server!.RequestsReceived);
    }

    private async Task<ICatalogAvailabilityClient> CreateClientAsync(FakeCatalogBehavior behavior)
    {
        _server = await FakeCatalogServerFactory.StartAsync(behavior);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CatalogClient:BaseUrl"] = new Uri(_server.BaseAddress, "v1").ToString(),
                ["CatalogClient:TimeoutSeconds"] = "1"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddCatalogAvailabilityClient(configuration);

        var server = _server;
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => server.Handler));

        _provider = services.BuildServiceProvider();

        return _provider.GetRequiredService<ICatalogAvailabilityClient>();
    }
}
