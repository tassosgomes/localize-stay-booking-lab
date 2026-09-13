using System.Net;
using System.Text;
using System.Text.Json;
using LocalizeStay.Booking.Infra.Persistence;
using LocalizeStay.Booking.IntegrationTests.Catalog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

namespace LocalizeStay.Booking.IntegrationTests.Reservations;

// V-03: os 7 cenários do AC de RF-01 (1 sucesso + 6 rejeições) via
// WebApplicationFactory real — Postgres + RabbitMQ Testcontainers da coleção e
// fake Catalog in-memory (helper da task 2.0). Status/code são os exatos do
// api-contract.yaml; nenhuma rejeição persiste nem publica.
[Collection("BookingIntegrationTests")]
public sealed class ReservationEndpointTests(CustomWebApplicationFactory factory) : IAsyncLifetime
{
    private const string ReservationsPath = "/v1/reservations";

    private const string ProblemContentType = "application/problem+json";

    private const string ReservationEventsExchange = "booking.reservation-events";

    private const string ReservationRequestedRoutingKey = "reservation.requested";

    private const string ReservationRequestedCloudEventType = "booking.reservation_requested.v1";

    private readonly CustomWebApplicationFactory _factory = factory;

    private FakeCatalogServerFactory? _fakeCatalog;

    // Fatos hipotéticos de 200 para as rejeições decididas sobre a resposta de Catalog.
    private const string InactiveAccommodationBody =
        """{"accommodationId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","active":false,"maxGuests":4,"availableForPeriod":true,"pricePerNight":"350.00","currency":"BRL"}""";

    private const string UnavailablePeriodBody =
        """{"accommodationId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","active":true,"maxGuests":4,"availableForPeriod":false,"pricePerNight":"350.00","currency":"BRL"}""";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_fakeCatalog is not null)
        {
            await _fakeCatalog.DisposeAsync();
        }
    }

    [Fact]
    public async Task Post_reservations_with_valid_request_returns_201_location_response_and_publishes_event()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync(FakeCatalogBehavior.Ok);
        await using var eventQueue = await BindReservationEventsQueueAsync();

        var accommodationId = Guid.NewGuid();
        using var response = await client.PostAsync(
            ReservationsPath,
            ToJsonContent(RequestBody(accommodationId, "2026-10-10", "2026-10-13", 2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        using var document = await ReadJsonAsync(response);

        var reservationId = Guid.Parse(document.RootElement.GetProperty("id").GetString()!);
        Assert.Equal($"/v1/reservations/{reservationId}", response.Headers.Location.ToString());
        Assert.Equal(accommodationId, Guid.Parse(document.RootElement.GetProperty("accommodationId").GetString()!));
        Assert.Equal("guest-integration", document.RootElement.GetProperty("guestReference").GetString());
        Assert.Equal("2026-10-10", document.RootElement.GetProperty("checkIn").GetString());
        Assert.Equal("2026-10-13", document.RootElement.GetProperty("checkOut").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("guestsCount").GetInt32());
        Assert.Equal("solicitada", document.RootElement.GetProperty("status").GetString());

        // Valores monetários como string decimal de 2 casas (premissa do contrato).
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("pricePerNight").ValueKind);
        Assert.Equal("350.00", document.RootElement.GetProperty("pricePerNight").GetString());
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("totalAmount").ValueKind);
        Assert.Equal("1050.00", document.RootElement.GetProperty("totalAmount").GetString());
        Assert.Equal("BRL", document.RootElement.GetProperty("currency").GetString());
        Assert.Equal(1, _fakeCatalog!.RequestsReceived);

        // Persistiu exatamente a Reservation devolvida.
        Assert.Equal(1, await CountPersistedReservationsAsync(accommodationId));

        // Publicou booking.reservation_requested com correlationId = causationId = reservation.id.
        var consumed = await BasicGetOneAsync(eventQueue);
        Assert.NotNull(consumed);
        var reservationIdText = reservationId.ToString();
        Assert.Equal(ReservationRequestedCloudEventType, consumed.EnvelopeType);
        Assert.Equal(reservationIdText, consumed.HeaderCorrelationId);
        Assert.Equal(reservationIdText, consumed.HeaderCausationId);
        Assert.Equal(reservationId, consumed.DataReservationId);
        Assert.Equal("solicitada", consumed.DataStatus);
        Assert.Equal(1050m, consumed.DataTotalAmount);

        // 3.4: o endpoint técnico de diagnóstico fica fora do documento público de Swagger.
        using var swaggerResponse = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swaggerResponse.StatusCode);
        var swaggerJson = await swaggerResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("internal/diagnostics", swaggerJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_reservations_with_checkOut_equal_to_checkIn_returns_422_PERIODO_INVALIDO_without_calling_catalog()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync(FakeCatalogBehavior.Ok);
        await using var eventQueue = await BindReservationEventsQueueAsync();

        var accommodationId = Guid.NewGuid();
        using var response = await client.PostAsync(
            ReservationsPath,
            ToJsonContent(RequestBody(accommodationId, "2026-10-10", "2026-10-10", 2)));

        await AssertRejectionAsync(
            response, HttpStatusCode.UnprocessableEntity, "PERIODO_INVALIDO", "Período inválido");

        // RN-02 rejeita localmente: nenhuma chamada de negócio a Catalog.
        Assert.Equal(0, _fakeCatalog!.RequestsReceived);
        Assert.Equal(0, await CountPersistedReservationsAsync(accommodationId));
        Assert.Null(await BasicGetOneAsync(eventQueue));
    }

    [Fact]
    public async Task Post_reservations_with_zero_guests_returns_422_QUANTIDADE_HOSPEDES_INVALIDA_without_calling_catalog()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync(FakeCatalogBehavior.Ok);
        await using var eventQueue = await BindReservationEventsQueueAsync();

        var accommodationId = Guid.NewGuid();
        using var response = await client.PostAsync(
            ReservationsPath,
            ToJsonContent(RequestBody(accommodationId, "2026-10-10", "2026-10-13", 0)));

        await AssertRejectionAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            "QUANTIDADE_HOSPEDES_INVALIDA",
            "Quantidade de hóspedes inválida");

        // RN-03 rejeita localmente: nenhuma chamada de negócio a Catalog.
        Assert.Equal(0, _fakeCatalog!.RequestsReceived);
        Assert.Equal(0, await CountPersistedReservationsAsync(accommodationId));
        Assert.Null(await BasicGetOneAsync(eventQueue));
    }

    [Fact]
    public async Task Post_reservations_when_catalog_reports_missing_or_inactive_returns_422_ACOMODACAO_INDISPONIVEL()
    {
        // AC única no PRD: "não existe OU não está ativa" → mesmo code 422.
        using (var client = await CreateClientWiredToFakeCatalogAsync(FakeCatalogBehavior.NotFound))
        {
            var accommodationId = Guid.NewGuid();
            using var response = await client.PostAsync(
                ReservationsPath,
                ToJsonContent(RequestBody(accommodationId, "2026-10-10", "2026-10-13", 2)));

            await AssertRejectionAsync(
                response, HttpStatusCode.UnprocessableEntity, "ACOMODACAO_INDISPONIVEL", "Acomodação indisponível");

            Assert.Equal(1, _fakeCatalog!.RequestsReceived);
            Assert.Equal(0, await CountPersistedReservationsAsync(accommodationId));
        }

        await DisposeFakeCatalogAsync();

        using (var client = await CreateClientWiredToFakeCatalogAsync(
                   FakeCatalogBehavior.Ok, InactiveAccommodationBody))
        {
            var accommodationId = Guid.NewGuid();
            using var response = await client.PostAsync(
                ReservationsPath,
                ToJsonContent(RequestBody(accommodationId, "2026-10-10", "2026-10-13", 2)));

            await AssertRejectionAsync(
                response, HttpStatusCode.UnprocessableEntity, "ACOMODACAO_INDISPONIVEL", "Acomodação indisponível");

            Assert.Equal(1, _fakeCatalog!.RequestsReceived);
            Assert.Equal(0, await CountPersistedReservationsAsync(accommodationId));
        }
    }

    [Fact]
    public async Task Post_reservations_with_guests_above_maxGuests_returns_422_CAPACIDADE_EXCEDIDA()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync(FakeCatalogBehavior.Ok);
        await using var eventQueue = await BindReservationEventsQueueAsync();

        var accommodationId = Guid.NewGuid();
        using var response = await client.PostAsync(
            ReservationsPath,
            ToJsonContent(RequestBody(accommodationId, "2026-10-10", "2026-10-13", 5)));

        await AssertRejectionAsync(
            response, HttpStatusCode.UnprocessableEntity, "CAPACIDADE_EXCEDIDA", "Capacidade excedida");

        Assert.Equal(1, _fakeCatalog!.RequestsReceived);
        Assert.Equal(0, await CountPersistedReservationsAsync(accommodationId));
        Assert.Null(await BasicGetOneAsync(eventQueue));
    }

    [Fact]
    public async Task Post_reservations_with_unavailable_period_returns_422_PERIODO_INDISPONIVEL()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync(
            FakeCatalogBehavior.Ok, UnavailablePeriodBody);
        await using var eventQueue = await BindReservationEventsQueueAsync();

        var accommodationId = Guid.NewGuid();
        using var response = await client.PostAsync(
            ReservationsPath,
            ToJsonContent(RequestBody(accommodationId, "2026-10-10", "2026-10-13", 2)));

        await AssertRejectionAsync(
            response, HttpStatusCode.UnprocessableEntity, "PERIODO_INDISPONIVEL", "Período indisponível");

        Assert.Equal(1, _fakeCatalog!.RequestsReceived);
        Assert.Equal(0, await CountPersistedReservationsAsync(accommodationId));
        Assert.Null(await BasicGetOneAsync(eventQueue));
    }

    [Fact]
    public async Task Post_reservations_when_catalog_fails_returns_503_CATALOG_INDISPONIVEL_with_traceId()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync(FakeCatalogBehavior.ServerError);
        await using var eventQueue = await BindReservationEventsQueueAsync();

        var accommodationId = Guid.NewGuid();
        using var response = await client.PostAsync(
            ReservationsPath,
            ToJsonContent(RequestBody(accommodationId, "2026-10-10", "2026-10-13", 2)));

        await AssertRejectionAsync(
            response,
            HttpStatusCode.ServiceUnavailable,
            "CATALOG_INDISPONIVEL",
            "Não foi possível validar a solicitação no momento");

        Assert.Equal(1, _fakeCatalog!.RequestsReceived);

        using var document = await ReadJsonAsync(response);
        var traceId = document.RootElement.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(traceId));

        // Falha de Catalog não é rejeição de negócio, mas também não cria nada.
        Assert.Equal(0, await CountPersistedReservationsAsync(accommodationId));
        Assert.Null(await BasicGetOneAsync(eventQueue));
    }

    private async Task<HttpClient> CreateClientWiredToFakeCatalogAsync(
        FakeCatalogBehavior behavior, string? okBody = null)
    {
        await DisposeFakeCatalogAsync();
        _fakeCatalog = await FakeCatalogServerFactory.StartAsync(behavior, okBody);
        var fakeCatalog = _fakeCatalog;

        var wiredFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.ConfigureHttpClientDefaults(httpClientBuilder =>
                    httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => fakeCatalog.Handler))));

        return wiredFactory.CreateClient();
    }

    private async ValueTask DisposeFakeCatalogAsync()
    {
        if (_fakeCatalog is not null)
        {
            await _fakeCatalog.DisposeAsync();
            _fakeCatalog = null;
        }
    }

    private static StringContent ToJsonContent(string body) =>
        new(body, Encoding.UTF8, "application/json");

    private static string RequestBody(Guid accommodationId, string checkIn, string checkOut, int guestsCount) =>
        $$"""{"accommodationId":"{{accommodationId}}","guestReference":"guest-integration","checkIn":"{{checkIn}}","checkOut":"{{checkOut}}","guestsCount":{{guestsCount}}}""";

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    private static async Task AssertRejectionAsync(
        HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedCode, string expectedTitle)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            ProblemContentType,
            response.Content.Headers.ContentType?.MediaType);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal((int)expectedStatus, root.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, root.GetProperty("code").GetString());
        Assert.Equal(expectedTitle, root.GetProperty("title").GetString());
        Assert.Equal(ReservationsPath, root.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
    }

    private async Task<long> CountPersistedReservationsAsync(Guid accommodationId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await dbContext.Reservations
            .AsNoTracking()
            .LongCountAsync(reservation => reservation.AccommodationId == accommodationId);
    }

    private async Task<ReservationEventsQueue> BindReservationEventsQueueAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RabbitMQ__HostName") ?? "localhost",
            Port = int.Parse(Environment.GetEnvironmentVariable("RabbitMQ__Port") ?? "5672"),
            UserName = "guest",
            Password = "guest",
            VirtualHost = "localize-stay",
            ClientProvidedName = "reservation-endpoint-tests"
        };

        var connection = await factory.CreateConnectionAsync(CancellationToken.None);
        var channel = await connection.CreateChannelAsync(cancellationToken: CancellationToken.None);

        await channel.ExchangeDeclareAsync(
            ReservationEventsExchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: CancellationToken.None);

        var queue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: CancellationToken.None);

        await channel.QueueBindAsync(
            queue.QueueName,
            ReservationEventsExchange,
            routingKey: ReservationRequestedRoutingKey,
            cancellationToken: CancellationToken.None);

        return new ReservationEventsQueue(connection, channel, queue.QueueName);
    }

    private static async Task<ConsumedReservationEvent?> BasicGetOneAsync(ReservationEventsQueue eventQueue)
    {
        var result = await eventQueue.Channel.BasicGetAsync(
            eventQueue.QueueName, autoAck: false, CancellationToken.None);

        if (result is null)
        {
            return null;
        }

        try
        {
            // Envelope CloudEvents structured: campos em minúsculas; o payload
            // `data` sai com os nomes declarados em C# (PascalCase).
            using var envelope = JsonDocument.Parse(result.Body);
            var root = envelope.RootElement;
            var data = root.GetProperty("data");

            return new ConsumedReservationEvent(
                EnvelopeType: root.GetProperty("type").GetString(),
                HeaderCorrelationId: ReadHeader(result.BasicProperties?.Headers, "x-correlation-id"),
                HeaderCausationId: ReadHeader(result.BasicProperties?.Headers, "x-causation-id"),
                DataReservationId: Guid.Parse(data.GetProperty("ReservationId").GetString()!),
                DataStatus: data.GetProperty("Status").GetString(),
                DataTotalAmount: data.GetProperty("TotalAmount").GetDecimal());
        }
        finally
        {
            await eventQueue.Channel.BasicAckAsync(
                result.DeliveryTag, multiple: false, CancellationToken.None);
        }
    }

    private static string? ReadHeader(IDictionary<string, object?>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => value?.ToString()
        };
    }

    private sealed record ConsumedReservationEvent(
        string? EnvelopeType,
        string? HeaderCorrelationId,
        string? HeaderCausationId,
        Guid DataReservationId,
        string? DataStatus,
        decimal DataTotalAmount);

    private sealed record ReservationEventsQueue(
        IConnection Connection,
        IChannel Channel,
        string QueueName) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Channel.CloseAsync(CancellationToken.None);
            await Channel.DisposeAsync();
            await Connection.CloseAsync(CancellationToken.None);
            await Connection.DisposeAsync();
        }
    }
}
