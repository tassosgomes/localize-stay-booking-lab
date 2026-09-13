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
using Rmq.CloudEvents.Publishing;
using Xunit;

namespace LocalizeStay.Booking.IntegrationTests.Reservations;

// F04 (RF-01/RF-03): publica payment.payment_authorized simulado contra
// RabbitMQ real (Testcontainers) e confere confirmação persistida em Postgres
// real + booking.reservation_confirmed fiel ao contrato; duplicado/tardio e
// não correlacionável não produzem nova mutação/publicação.
[Collection("BookingIntegrationTests")]
public sealed class PaymentAuthorizedConsumptionTests(CustomWebApplicationFactory factory) : IAsyncLifetime
{
    private const string ReservationsPath = "/v1/reservations";

    private const string PaymentAuthorizedExchange = "payment.payment_authorized";

    private const string PaymentAuthorizedRoutingKey = "payment.payment_authorized";

    private const string PaymentAuthorizedCloudEventType = "com.localizestay.payment.payment_authorized.v1";

    private const string ReservationConfirmedExchange = "booking.reservation_confirmed";

    private const string ReservationConfirmedRoutingKey = "booking.reservation_confirmed";

    private const string ReservationConfirmedCloudEventType = "com.localizestay.booking.reservation_confirmed.v1";

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
    public async Task Payment_authorized_with_known_correlation_confirms_reservation_and_publishes_confirmed_event()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync();
        await using var eventQueue = await BindReservationConfirmedQueueAsync();

        var created = await CreateReservationAsync(client);
        await PublishPaymentAuthorizedAsync(created.CorrelationId);

        var persisted = await WaitForTerminalStatusAsync(created.ReservationId);
        Assert.NotNull(persisted);
        Assert.Equal("Confirmada", persisted.Status);
        Assert.Equal("Authorized", persisted.SagaState);
        Assert.Null(persisted.CancellationReason);

        var correlationIdText = created.CorrelationId.ToString();
        var reservationIdText = created.ReservationId.ToString();
        var consumed = await WaitForEventAsync(eventQueue, correlationIdText, ConsumeTimeout);
        Assert.NotNull(consumed);
        Assert.Equal(ReservationConfirmedCloudEventType, consumed.EnvelopeType);
        Assert.Equal(correlationIdText, consumed.HeaderCorrelationId);
        Assert.Equal(correlationIdText, consumed.HeaderCausationId);
        Assert.Equal(correlationIdText, consumed.DataCorrelationId);
        Assert.Equal(correlationIdText, consumed.DataCausationId);
        Assert.Equal(reservationIdText, consumed.DataReservationId);
        Assert.Equal(created.AccommodationId.ToString(), consumed.DataAccommodationId);
        Assert.Equal("guest-integration", consumed.DataGuestReference);
        Assert.Equal("2026-10-10", consumed.DataCheckIn);
        Assert.Equal("2026-10-13", consumed.DataCheckOut);
        Assert.False(string.IsNullOrWhiteSpace(consumed.DataConfirmedAt));
    }

    [Fact]
    public async Task Duplicate_payment_authorized_after_confirmation_publishes_nothing_and_keeps_state()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync();
        await using var eventQueue = await BindReservationConfirmedQueueAsync();

        var created = await CreateReservationAsync(client);
        var correlationIdText = created.CorrelationId.ToString();
        await PublishPaymentAuthorizedAsync(created.CorrelationId);
        Assert.NotNull(await WaitForTerminalStatusAsync(created.ReservationId));

        // Consome (e remove) o único evento final esperado.
        Assert.NotNull(await WaitForEventAsync(eventQueue, correlationIdText, ConsumeTimeout));

        // Duplicado/tardio: nenhuma segunda mutação/publicação para esta
        // Reservation (o exchange é compartilhado com outros testes da mesma
        // suíte, então filtramos por correlationId em vez de exigir silêncio
        // total da fila).
        await PublishPaymentAuthorizedAsync(created.CorrelationId);

        Assert.Null(await WaitForEventAsync(eventQueue, correlationIdText, TimeSpan.FromSeconds(4)));
        var persisted = await ReadPersistedStateAsync(created.ReservationId);
        Assert.NotNull(persisted);
        Assert.Equal("Confirmada", persisted.Status);
        Assert.Equal("Authorized", persisted.SagaState);
    }

    [Fact]
    public async Task Payment_authorized_with_unknown_correlation_publishes_nothing()
    {
        using var client = await CreateClientWiredToFakeCatalogAsync();
        await using var eventQueue = await BindReservationConfirmedQueueAsync();

        var created = await CreateReservationAsync(client);
        await PublishPaymentAuthorizedAsync(Guid.NewGuid());

        // Nada publicado para a Reservation desta run em decorrência da
        // correlação desconhecida (o exchange é compartilhado com outros
        // testes da mesma suíte, então filtramos por correlationId em vez de
        // exigir silêncio total da fila).
        Assert.Null(await WaitForEventAsync(eventQueue, created.CorrelationId.ToString(), TimeSpan.FromSeconds(4)));

        // ...e a Reservation existente permanece solicitada/pendente.
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
                $$"""{"accommodationId":"{{accommodationId}}","guestReference":"guest-integration","checkIn":"2026-10-10","checkOut":"2026-10-13","guestsCount":2}"""));

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

    private sealed record PaymentAuthorizedSimulated(
        [property: System.Text.Json.Serialization.JsonPropertyName("correlationId")] Guid CorrelationId,
        [property: System.Text.Json.Serialization.JsonPropertyName("authorizedAt")] DateTimeOffset AuthorizedAt);

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

    private async Task<ConfirmedEventsQueue> BindReservationConfirmedQueueAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RabbitMQ__HostName") ?? "localhost",
            Port = int.Parse(Environment.GetEnvironmentVariable("RabbitMQ__Port") ?? "5672"),
            UserName = "guest",
            Password = "guest",
            VirtualHost = "localize-stay",
            ClientProvidedName = "payment-authorized-consumption-tests"
        };

        var connection = await factory.CreateConnectionAsync(CancellationToken.None);
        var channel = await connection.CreateChannelAsync(cancellationToken: CancellationToken.None);

        await channel.ExchangeDeclareAsync(
            ReservationConfirmedExchange,
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
            ReservationConfirmedExchange,
            routingKey: ReservationConfirmedRoutingKey,
            cancellationToken: CancellationToken.None);

        return new ConfirmedEventsQueue(connection, channel, queue.QueueName);
    }

    // Aguarda, até o timeout, uma mensagem cujo correlationId case com o
    // esperado — descartando (drenando) qualquer mensagem de outra
    // correlação encontrada no caminho. O exchange booking.reservation_confirmed
    // é compartilhado por toda a suíte de testes de integração: uma
    // publicação de outro teste pode chegar de forma assíncrona/retriada
    // enquanto esta fila exclusiva está com o bind ativo, então "nada
    // publicado" é verificado por ausência do correlationId em questão, não
    // pelo silêncio total da fila.
    private static async Task<ConsumedConfirmedEvent?> WaitForEventAsync(
        ConfirmedEventsQueue eventQueue, string correlationId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            var consumed = await BasicGetOneAsync(eventQueue);
            if (consumed is not null)
            {
                if (consumed.DataCorrelationId == correlationId)
                {
                    return consumed;
                }

                continue;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return null;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }
    }

    private static async Task<ConsumedConfirmedEvent?> BasicGetOneAsync(ConfirmedEventsQueue eventQueue)
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

            return new ConsumedConfirmedEvent(
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
                DataConfirmedAt: data.GetProperty("confirmedAt").GetString());
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

    private sealed record ConsumedConfirmedEvent(
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
        string? DataConfirmedAt);

    private sealed record ConfirmedEventsQueue(
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
