using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Rmq.CloudEvents.Publishing;
using Xunit;

namespace LocalizeStay.Messaging.IntegrationTests;

/// <summary>
/// Fluxo de diagnóstico ponta a ponta (V-03): Booking publica
/// <c>DiagnosticPing</c> na exchange <c>diagnostics.topic</c> do vhost
/// <c>localize-stay</c>; o Notification Worker consome da fila
/// <c>notification.diagnostics</c>, faz ACK e loga com os mesmos
/// <c>correlationId</c>/<c>causationId</c>.
/// </summary>
public sealed class DiagnosticPingFlowTests(DiagnosticsFixture fixture)
    : IClassFixture<DiagnosticsFixture>, IDisposable
{
    private const string Exchange = "diagnostics.topic";
    private const string RoutingKey = "diagnostics.ping";
    private const string Queue = "notification.diagnostics";
    private const string DeadLetterQueue = "notification.diagnostics.dlq";
    private const string CloudEventType = "com.localizestay.diagnostics.ping.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DiagnosticsFixture _fixture = fixture;
    private readonly BookingDiagnosticsFactory _booking = new(fixture);
    private readonly WorkerDiagnosticsFactory _worker = new(fixture);

    public void Dispose()
    {
        _booking.Dispose();
        _worker.Dispose();
    }

    [Fact]
    public async Task Ping_endpoint_publishes_diagnostic_ping_to_exchange()
    {
        // Arrange: liga a fila antes do POST — o publisher usa mandatory:true e o
        // broker devolveria a mensagem sem binding (falha de publish).
        await DeclareDiagnosticsTopologyAsync();
        await _booking.EnsureDatabaseMigratedAsync();
        using var client = _booking.CreateClient();

        // Act
        using var response = await client.PostAsync("/internal/diagnostics/ping", null);

        // Assert: endpoint aceita e o health de readiness cobre Postgres + RabbitMQ.
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = await response.Content
            .ReadFromJsonAsync<PingAcceptedResponse>()
            ;
        Assert.NotNull(accepted);
        Assert.False(string.IsNullOrWhiteSpace(accepted.CorrelationId));
        Assert.Equal(accepted.CorrelationId, accepted.CausationId);

        using var readyResponse = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        var readyBody = await readyResponse.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", readyBody, StringComparison.OrdinalIgnoreCase);

        // Assert: mensagem roteada com envelope CloudEvents + IDs de correlação.
        var consumed = await BasicGetAsync();
        Assert.NotNull(consumed);
        Assert.Equal(CloudEventType, consumed.EnvelopeType);
        Assert.Equal(accepted.CorrelationId, consumed.CorrelationId);
        Assert.Equal(accepted.CausationId, consumed.CausationId);
        Assert.Equal(accepted.PingId, consumed.PingId);
        Assert.Equal(accepted.CorrelationId, consumed.HeaderCorrelationId);
    }

    [Fact]
    public async Task Worker_consumes_and_acks_with_same_correlation_ids()
    {
        // Arrange: worker real no ar (consumer declara a topologia no boot).
        using var workerClient = _worker.CreateClient();
        var publisher = _worker.Services.GetRequiredService<IRmqPublisher>();

        var ping = new WorkerTestPing(
            PingId: Guid.NewGuid().ToString("N"),
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: Guid.NewGuid().ToString("N"),
            SentAt: DateTimeOffset.UtcNow);

        // Act
        await publisher.PublishToTopicAsync(
            Exchange,
            RoutingKey,
            ping,
            new Dictionary<string, object>
            {
                ["x-correlation-id"] = ping.CorrelationId,
                ["x-causation-id"] = ping.CausationId
            },
            CloudEventType);

        // Assert: log estruturado do worker contém os mesmos IDs publicados.
        await WaitUntilAsync(
            () => _fixture.Capture.Messages.Any(message =>
                message.Contains(ping.CorrelationId, StringComparison.Ordinal)),
            TimeSpan.FromSeconds(30));

        var matchingLogs = _fixture.Capture.Messages
            .Where(message => message.Contains(ping.CorrelationId, StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(matchingLogs);
        Assert.Contains(
            matchingLogs,
            message => message.Contains(ping.CausationId, StringComparison.Ordinal));

        // Assert: ACK — a mensagem não reaparece na fila (declare passivo mostra
        // profundidade zero com o consumer do worker ligado); a DLQ existe e
        // está vazia no caminho feliz (nada foi dead-lettered).
        var mainStats = await GetQueueStatsAsync(Queue);
        Assert.Equal(0u, mainStats.Messages);
        Assert.True(mainStats.Consumers >= 1);

        var dlqStats = await GetQueueStatsAsync(DeadLetterQueue);
        Assert.Equal(0u, dlqStats.Messages);

        // Assert: /health/ready do worker reporta o RabbitMQ.
        using var readyResponse = await workerClient.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        var readyBody = await readyResponse.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", readyBody, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(uint Messages, uint Consumers)> GetQueueStatsAsync(string queue)
    {
        var factory = new ConnectionFactory
        {
            HostName = _fixture.AmqpHost,
            Port = _fixture.AmqpPort,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "localize-stay",
            ClientProvidedName = "diagnostic-ping-flow-tests"
        };

        await using var connection = await factory
            .CreateConnectionAsync(CancellationToken.None)
            ;
        await using var channel = await connection
            .CreateChannelAsync(cancellationToken: CancellationToken.None)
            ;

        // Declare passivo: falha com 404 se a fila não existir; senão devolve
        // profundidade e número de consumers sem alterar a topologia.
        var status = await channel
            .QueueDeclarePassiveAsync(queue, CancellationToken.None)
            ;
        return (status.MessageCount, status.ConsumerCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
    }

    private async Task DeclareDiagnosticsTopologyAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = _fixture.AmqpHost,
            Port = _fixture.AmqpPort,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "localize-stay",
            ClientProvidedName = "diagnostic-ping-flow-tests"
        };

        await using var connection = await factory
            .CreateConnectionAsync(CancellationToken.None)
            ;
        await using var channel = await connection
            .CreateChannelAsync(cancellationToken: CancellationToken.None)
            ;

        await channel.ExchangeDeclareAsync(
            "notification.diagnostics.dlx",
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: CancellationToken.None);

        await channel.QueueDeclareAsync(
            DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: CancellationToken.None);

        await channel.QueueBindAsync(
            DeadLetterQueue,
            "notification.diagnostics.dlx",
            routingKey: Queue,
            cancellationToken: CancellationToken.None);

        await channel.ExchangeDeclareAsync(
            Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: CancellationToken.None);

        await channel.QueueDeclareAsync(
            Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = 5,
                ["x-dead-letter-exchange"] = "notification.diagnostics.dlx",
                ["x-dead-letter-routing-key"] = Queue
            },
            cancellationToken: CancellationToken.None);

        await channel.QueueBindAsync(
            Queue,
            Exchange,
            routingKey: RoutingKey,
            cancellationToken: CancellationToken.None);
    }

    private async Task<ConsumedPing?> BasicGetAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = _fixture.AmqpHost,
            Port = _fixture.AmqpPort,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "localize-stay",
            ClientProvidedName = "diagnostic-ping-flow-tests"
        };

        await using var connection = await factory
            .CreateConnectionAsync(CancellationToken.None)
            ;
        await using var channel = await connection
            .CreateChannelAsync(cancellationToken: CancellationToken.None)
            ;

        var result = await channel
            .BasicGetAsync(Queue, autoAck: false, CancellationToken.None)
            ;

        if (result is null)
        {
            return null;
        }

        try
        {
            // O envelope é CloudEvents structured (campos do envelope em minúsculas);
            // o payload `data` sai com os nomes declarados em C# (PascalCase) —
            // desserialização case-insensitive cobre ambos.
            using var envelope = JsonDocument.Parse(result.Body);
            var root = envelope.RootElement;
            var data = root.GetProperty("data").Deserialize<ConsumedPingData>(JsonOptions);

            string? Get(JsonElement element, string name) =>
                element.TryGetProperty(name, out var value)
                    ? value.GetString()
                    : throw new InvalidOperationException(
                        $"Sem '{name}' em: {element.GetRawText()[..Math.Min(500, element.GetRawText().Length)]}");

            return new ConsumedPing(
                EnvelopeType: Get(root, "type"),
                PingId: data?.PingId,
                CorrelationId: data?.CorrelationId,
                CausationId: data?.CausationId,
                HeaderCorrelationId: ReadHeader(result.BasicProperties?.Headers, "x-correlation-id"));
        }
        finally
        {
            await channel
                .BasicAckAsync(result.DeliveryTag, multiple: false, CancellationToken.None)
                ;
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
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => value?.ToString()
        };
    }

    private sealed record PingAcceptedResponse(
        string PingId,
        string CorrelationId,
        string CausationId,
        DateTimeOffset SentAt);

    private sealed record WorkerTestPing(
        string PingId,
        string CorrelationId,
        string CausationId,
        DateTimeOffset SentAt);

    private sealed record ConsumedPingData(
        string PingId,
        string CorrelationId,
        string CausationId,
        DateTimeOffset SentAt);

    private sealed record ConsumedPing(
        string? EnvelopeType,
        string? PingId,
        string? CorrelationId,
        string? CausationId,
        string? HeaderCorrelationId);
}
