using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalizeStay.Booking.Infra.Persistence;
using LocalizeStay.Booking.IntegrationTests.Catalog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Rmq.CloudEvents.Publishing;
using Xunit;

namespace LocalizeStay.Booking.IntegrationTests.Reservations;

// F04 (RF-02/RF-03/DP-01): publica payment.payment_rejected simulado contra
// RabbitMQ real (Testcontainers) e confere cancelamento persistido em Postgres
// real (com motivo fixo de negócio) + booking.reservation_cancelled fiel ao
// contrato; duplicado/tardio/conflitante e não correlacionável não produzem
// nova mutação/publicação.
[Collection("BookingIntegrationTests")]
public sealed class PaymentRejectedConsumptionTests(CustomWebApplicationFactory factory) : IAsyncLifetime
{
    private const string ExpectedReason = "Pagamento rejeitado pela simulação de Payment.";

    private const string ReservationsPath = "/v1/reservations";

    private const string PaymentRejectedExchange = "payment.payment_rejected";

    private const string PaymentRejectedRoutingKey = "payment.payment_rejected";

    private const string PaymentRejectedCloudEventType = "com.localizestay.payment.payment_rejected.v1";

    private const string PaymentAuthorizedExchange = "payment.payment_authorized";

    private const string PaymentAuthorizedRoutingKey = "payment.payment_authorized";

    private const string PaymentAuthorizedCloudEventType = "com.localizestay.payment.payment_authorized.v1";

    private const string ReservationCancelledExchange = "booking.reservation_cancelled";

    private const string ReservationCancelledRoutingKey = "booking.reservation_cancelled";

    private const string ReservationCancelledCloudEventType = "com.localizestay.booking.reservation_cancelled.v1";

    private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(20);

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
    public async Task Payment_rejected_with_known_correlation_cancels_reservation_and_publishes_cancelled_event()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync();
        await using var eventQueue = await BindReservationCancelledQueueAsync();

        var created = await CreateReservationAsync(client);
        await PublishPaymentRejectedAsync(created.CorrelationId);

        var persisted = await WaitForTerminalStatusAsync(created.ReservationId);
        Assert.NotNull(persisted);
        Assert.Equal("Cancelada", persisted.Status);
        Assert.Equal("Rejected", persisted.SagaState);
        Assert.Equal(ExpectedReason, persisted.CancellationReason);

        var consumed = await BasicGetOneAsync(eventQueue);
        Assert.NotNull(consumed);
        var correlationIdText = created.CorrelationId.ToString();
        var reservationIdText = created.ReservationId.ToString();
        Assert.Equal(ReservationCancelledCloudEventType, consumed.EnvelopeType);
        Assert.Equal(correlationIdText, consumed.HeaderCorrelationId);
        Assert.Equal(correlationIdText, consumed.HeaderCausationId);
        Assert.Equal(correlationIdText, consumed.DataCorrelationId);
        Assert.Equal(correlationIdText, consumed.DataCausationId);
        Assert.Equal(reservationIdText, consumed.DataReservationId);
        Assert.Equal(created.AccommodationId.ToString(), consumed.DataAccommodationId);
        Assert.Equal("guest-integration", consumed.DataGuestReference);
        Assert.Equal("2026-11-01", consumed.DataCheckIn);
        Assert.Equal("2026-11-05", consumed.DataCheckOut);
        Assert.False(string.IsNullOrWhiteSpace(consumed.DataCancelledAt));
        Assert.Equal(ExpectedReason, consumed.DataCancellationReason);
    }

    [Fact]
    public async Task Conflicting_payment_rejected_after_confirmation_publishes_nothing_and_keeps_confirmed_state()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync();

        var created = await CreateReservationAsync(client);

        // Primeiro resultado (autorização) fecha a saga em confirmada...
        await PublishPaymentAuthorizedAsync(created.CorrelationId);
        var confirmed = await WaitForTerminalStatusAsync(created.ReservationId);
        Assert.NotNull(confirmed);
        Assert.Equal("Confirmada", confirmed.Status);

        // ...o resultado conflitante (rejeição tardia) é ignorado: drena o
        // evento de confirmação já publicado e confere que nenhum
        // cancelamento é publicado.
        await using var cancelledQueue = await BindReservationCancelledQueueAsync();
        await PublishPaymentRejectedAsync(created.CorrelationId);
        await Task.Delay(TimeSpan.FromSeconds(4));

        Assert.Null(await BasicGetOneAsync(cancelledQueue));
        var persisted = await ReadPersistedStateAsync(created.ReservationId);
        Assert.NotNull(persisted);
        Assert.Equal("Confirmada", persisted.Status);
        Assert.Equal("Authorized", persisted.SagaState);
    }

    [Fact]
    public async Task Payment_rejected_with_unknown_correlation_publishes_nothing()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync();
        await using var eventQueue = await BindReservationCancelledQueueAsync();

        var created = await CreateReservationAsync(client);
        await PublishPaymentRejectedAsync(Guid.NewGuid());
        await Task.Delay(TimeSpan.FromSeconds(4));

        Assert.Null(await BasicGetOneAsync(eventQueue));

        var persisted = await ReadPersistedStateAsync(created.ReservationId);
        Assert.NotNull(persisted);
        Assert.Equal("Solicitada", persisted.Status);
        Assert.Equal("PaymentPending", persisted.SagaState);
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

    private sealed record CreatedReservation(Guid ReservationId, Guid CorrelationId, Guid AccommodationId);

    private async Task<CreatedReservation> CreateReservationAsync(HttpClient client)
    {
        var accommodationId = Guid.NewGuid();
        using var response = await client.PostAsync(
            ReservationsPath,
            ToJsonContent(
                $$"""{"accommodationId":"{{accommodationId}}","guestReference":"guest-integration","checkIn":"2026-11-01","checkOut":"2026-11-05","guestsCount":2}"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var reservationId = Guid.Parse(document.RootElement.GetProperty("id").GetString()!);
        var correlationId = await ReadSagaCorrelationIdAsync(reservationId);
        return new CreatedReservation(reservationId, correlationId, accommodationId);
    }

    private async Task<Guid> ReadSagaCorrelationIdAsync(Guid reservationId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await dbContext.Database.SqlQueryRaw<Guid>(
            "SELECT correlation_id AS \"Value\" FROM booking.reservation_sagas WHERE reservation_id = {0}",
            reservationId).SingleAsync();
    }

    private async Task PublishPaymentRejectedAsync(Guid correlationId)
    {
        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IRmqPublisher>();
        await publisher.PublishToTopicAsync(
            PaymentRejectedExchange,
            PaymentRejectedRoutingKey,
            new PaymentRejectedSimulated(correlationId, DateTimeOffset.UtcNow),
            new Dictionary<string, object>
            {
                ["x-correlation-id"] = correlationId.ToString(),
                ["x-causation-id"] = correlationId.ToString()
            },
            PaymentRejectedCloudEventType);
    }

    private async Task PublishPaymentAuthorizedAsync(Guid correlationId)
    {
        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IRmqPublisher>();
        await publisher.PublishToTopicAsync(
            PaymentAuthorizedExchange,
            PaymentAuthorizedRoutingKey,
            new PaymentAuthorizedSimulated(correlationId, DateTimeOffset.UtcNow),
            new Dictionary<string, object>
            {
                ["x-correlation-id"] = correlationId.ToString(),
                ["x-causation-id"] = correlationId.ToString()
            },
            PaymentAuthorizedCloudEventType);
    }

    private sealed record PaymentRejectedSimulated(
        [property: JsonPropertyName("correlationId")] Guid CorrelationId,
        [property: JsonPropertyName("rejectedAt")] DateTimeOffset RejectedAt);

    private sealed record PaymentAuthorizedSimulated(
        [property: JsonPropertyName("correlationId")] Guid CorrelationId,
        [property: JsonPropertyName("authorizedAt")] DateTimeOffset AuthorizedAt);

    private sealed record PersistedState(string Status, string SagaState, string? CancellationReason);

    private async Task<PersistedState?> ReadPersistedStateAsync(Guid reservationId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var reservation = await dbContext.Reservations
            .AsNoTracking()
            .Include(r => r.Saga)
            .FirstOrDefaultAsync(r => r.Id == reservationId);
        return reservation is null
            ? null
            : new PersistedState(
                reservation.Status.ToString(), reservation.Saga.State.ToString(), reservation.Saga.CancellationReason);
    }

    private async Task<PersistedState?> WaitForTerminalStatusAsync(Guid reservationId)
    {
        var deadline = DateTimeOffset.UtcNow + ConsumeTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var persisted = await ReadPersistedStateAsync(reservationId);
            if (persisted is not null && persisted.Status is "Confirmada" or "Cancelada")
            {
                return persisted;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return await ReadPersistedStateAsync(reservationId);
    }

    private async Task<CancelledEventsQueue> BindReservationCancelledQueueAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RabbitMQ__HostName") ?? "localhost",
            Port = int.Parse(Environment.GetEnvironmentVariable("RabbitMQ__Port") ?? "5672"),
            UserName = "guest",
            Password = "guest",
            VirtualHost = "localize-stay",
            ClientProvidedName = "payment-rejected-consumption-tests"
        };

        var connection = await factory.CreateConnectionAsync(CancellationToken.None);
        var channel = await connection.CreateChannelAsync(cancellationToken: CancellationToken.None);

        await channel.ExchangeDeclareAsync(
            ReservationCancelledExchange,
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
            ReservationCancelledExchange,
            routingKey: ReservationCancelledRoutingKey,
            cancellationToken: CancellationToken.None);

        return new CancelledEventsQueue(connection, channel, queue.QueueName);
    }

    private static async Task<ConsumedCancelledEvent?> BasicGetOneAsync(CancelledEventsQueue eventQueue)
    {
        var result = await eventQueue.Channel.BasicGetAsync(
            eventQueue.QueueName, autoAck: false, CancellationToken.None);

        if (result is null)
        {
            return null;
        }

        try
        {
            using var envelope = JsonDocument.Parse(result.Body);
            var root = envelope.RootElement;
            var data = root.GetProperty("data");

            return new ConsumedCancelledEvent(
                EnvelopeType: root.GetProperty("type").GetString(),
                HeaderCorrelationId: ReadHeader(result.BasicProperties?.Headers, "x-correlation-id"),
                HeaderCausationId: ReadHeader(result.BasicProperties?.Headers, "x-causation-id"),
                DataCorrelationId: data.GetProperty("correlationId").GetString(),
                DataCausationId: data.GetProperty("causationId").GetString(),
                DataReservationId: data.GetProperty("reservationId").GetString(),
                DataAccommodationId: data.GetProperty("accommodationId").GetString(),
                DataGuestReference: data.GetProperty("guestReference").GetString(),
                DataCheckIn: data.GetProperty("checkIn").GetString(),
                DataCheckOut: data.GetProperty("checkOut").GetString(),
                DataCancelledAt: data.GetProperty("cancelledAt").GetString(),
                DataCancellationReason: data.GetProperty("cancellationReason").GetString());
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

    private sealed record ConsumedCancelledEvent(
        string? EnvelopeType,
        string? HeaderCorrelationId,
        string? HeaderCausationId,
        string? DataCorrelationId,
        string? DataCausationId,
        string? DataReservationId,
        string? DataAccommodationId,
        string? DataGuestReference,
        string? DataCheckIn,
        string? DataCheckOut,
        string? DataCancelledAt,
        string? DataCancellationReason);

    private sealed record CancelledEventsQueue(
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
