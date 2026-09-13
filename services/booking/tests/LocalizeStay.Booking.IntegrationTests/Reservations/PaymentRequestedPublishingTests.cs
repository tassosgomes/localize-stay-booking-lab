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

// F03 (RF-01): logo após reservation_requested (F01), o handler publica
// booking.payment_requested numa exchange real (Testcontainers RabbitMQ) e
// registra ReservationSaga.PaymentRequestSentAt (Testcontainers Postgres).
[Collection("BookingIntegrationTests")]
public sealed class PaymentRequestedPublishingTests(CustomWebApplicationFactory factory) : IAsyncLifetime
{
    private const string ReservationsPath = "/v1/reservations";

    private const string PaymentRequestedExchange = "booking.payment_requested";

    private const string PaymentRequestedRoutingKey = "booking.payment_requested";

    private const string PaymentRequestedCloudEventType = "com.localizestay.booking.payment_requested.v1";

    private readonly CustomWebApplicationFactory _factory = factory;

    private FakeCatalogServerFactory? _fakeCatalog;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_fakeCatalog is not null)
        {
            await _fakeCatalog.DisposeAsync();
        }
    }

    [Fact]
    public async Task Post_reservations_with_valid_request_publishes_payment_requested_and_marks_saga()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync();
        await using var eventQueue = await BindPaymentRequestedQueueAsync();

        var accommodationId = Guid.NewGuid();
        using var response = await client.PostAsync(
            ReservationsPath,
            ToJsonContent(RequestBody(accommodationId, "2026-10-10", "2026-10-13", 2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var reservationId = Guid.Parse(document.RootElement.GetProperty("id").GetString()!);
        var expectedTotalAmount = document.RootElement.GetProperty("totalAmount").GetString();
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("totalAmount").ValueKind);
        Assert.Equal("BRL", document.RootElement.GetProperty("currency").GetString());

        var correlationId = await ReadSagaCorrelationIdAsync(reservationId);

        // Publicou booking.payment_requested com correlationId/causationId =
        // Saga.CorrelationId e totalAmount/currency fiéis ao congelado.
        var consumed = await BasicGetOneAsync(eventQueue);
        Assert.NotNull(consumed);
        var correlationIdText = correlationId.ToString();
        Assert.Equal(PaymentRequestedCloudEventType, consumed.EnvelopeType);
        Assert.Equal(correlationIdText, consumed.HeaderCorrelationId);
        Assert.Equal(correlationIdText, consumed.HeaderCausationId);
        Assert.Equal(correlationIdText, consumed.DataCorrelationId);
        Assert.Equal(correlationIdText, consumed.DataCausationId);
        Assert.Equal(expectedTotalAmount, consumed.DataTotalAmount);
        Assert.Equal("BRL", consumed.DataCurrency);
        Assert.False(string.IsNullOrWhiteSpace(consumed.DataRequestedAt));

        // A publicação bem-sucedida registra PaymentRequestSentAt na Saga.
        Assert.True(await ReadPaymentRequestSentAtIsFilledAsync(reservationId));
    }

    private async Task<HttpClient> CreateClientWiredToFakeCatalogAsync()
    {
        _fakeCatalog = await FakeCatalogServerFactory.StartAsync(FakeCatalogBehavior.Ok);
        var fakeCatalog = _fakeCatalog;

        var wiredFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.ConfigureHttpClientDefaults(httpClientBuilder =>
                    httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => fakeCatalog.Handler))));

        return wiredFactory.CreateClient();
    }

    private static StringContent ToJsonContent(string body) =>
        new(body, Encoding.UTF8, "application/json");

    private static string RequestBody(Guid accommodationId, string checkIn, string checkOut, int guestsCount) =>
        $$"""{"accommodationId":"{{accommodationId}}","guestReference":"guest-payment-requested","checkIn":"{{checkIn}}","checkOut":"{{checkOut}}","guestsCount":{{guestsCount}}}""";

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    private async Task<Guid> ReadSagaCorrelationIdAsync(Guid reservationId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await dbContext.Database.SqlQueryRaw<Guid>(
            "SELECT correlation_id AS \"Value\" FROM booking.reservation_sagas WHERE reservation_id = {0}",
            reservationId).SingleAsync();
    }

    private async Task<bool> ReadPaymentRequestSentAtIsFilledAsync(Guid reservationId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await dbContext.Database.SqlQueryRaw<bool>(
            "SELECT (payment_request_sent_at IS NOT NULL) AS \"Value\" " +
            "FROM booking.reservation_sagas WHERE reservation_id = {0}",
            reservationId).SingleAsync();
    }

    private async Task<PaymentRequestedEventsQueue> BindPaymentRequestedQueueAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RabbitMQ__HostName") ?? "localhost",
            Port = int.Parse(Environment.GetEnvironmentVariable("RabbitMQ__Port") ?? "5672"),
            UserName = "guest",
            Password = "guest",
            VirtualHost = "localize-stay",
            ClientProvidedName = "payment-requested-publishing-tests"
        };

        var connection = await factory.CreateConnectionAsync(CancellationToken.None);
        var channel = await connection.CreateChannelAsync(cancellationToken: CancellationToken.None);

        await channel.ExchangeDeclareAsync(
            PaymentRequestedExchange,
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
            PaymentRequestedExchange,
            routingKey: PaymentRequestedRoutingKey,
            cancellationToken: CancellationToken.None);

        return new PaymentRequestedEventsQueue(connection, channel, queue.QueueName);
    }

    private static async Task<ConsumedPaymentRequestedEvent?> BasicGetOneAsync(
        PaymentRequestedEventsQueue eventQueue)
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
            // `data` sai em camelCase (JsonPropertyName), conforme api-contract.yaml.
            using var envelope = JsonDocument.Parse(result.Body);
            var root = envelope.RootElement;
            var data = root.GetProperty("data");

            return new ConsumedPaymentRequestedEvent(
                EnvelopeType: root.GetProperty("type").GetString(),
                HeaderCorrelationId: ReadHeader(result.BasicProperties?.Headers, "x-correlation-id"),
                HeaderCausationId: ReadHeader(result.BasicProperties?.Headers, "x-causation-id"),
                DataCorrelationId: data.GetProperty("correlationId").GetString(),
                DataCausationId: data.GetProperty("causationId").GetString(),
                DataTotalAmount: data.GetProperty("totalAmount").GetString(),
                DataCurrency: data.GetProperty("currency").GetString(),
                DataRequestedAt: data.GetProperty("requestedAt").GetString());
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

    private sealed record ConsumedPaymentRequestedEvent(
        string? EnvelopeType,
        string? HeaderCorrelationId,
        string? HeaderCausationId,
        string? DataCorrelationId,
        string? DataCausationId,
        string? DataTotalAmount,
        string? DataCurrency,
        string? DataRequestedAt);

    private sealed record PaymentRequestedEventsQueue(
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
