using System.Text.Json;
using LocalizeStay.Notification.Worker.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LocalizeStay.Notification.Worker.Messaging;

/// <summary>
/// Consome <c>DiagnosticPing</c> da fila <c>notification.diagnostics</c> (V-03).
/// Sem camadas Domain/Application nesta etapa: não há regra de negócio a isolar,
/// só transporte + correlação — evita design antecipado.
/// Convenções aplicadas (espelhando <c>Rmq.CloudEvents</c>):
/// ACK em sucesso; retry em processo com backoff exponencial + jitter;
/// NACK sem requeue após a falha final (a mensagem cai na DLQ via DLX);
/// NACK com requeue quando o desligamento interrompe o processamento.
/// </summary>
public sealed class DiagnosticPingConsumer : BackgroundService
{
    private const string CorrelationHeader = "x-correlation-id";
    private const string CausationHeader = "x-causation-id";
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConfiguration _configuration;
    private readonly ILogger<DiagnosticPingConsumer> _logger;

    public DiagnosticPingConsumer(IConfiguration configuration, ILogger<DiagnosticPingConsumer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = RabbitMqSettings.Read(_configuration);

        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = MessagingExtensions.ExpectedVirtualHost,
            ClientProvidedName = "notification-worker-consumer",
            AutomaticRecoveryEnabled = true
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken).ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken).ConfigureAwait(false);

        await DeclareTopologyAsync(channel, settings, stoppingToken).ConfigureAwait(false);

        if (settings.PrefetchCount > 0)
        {
            await channel.BasicQosAsync(0, settings.PrefetchCount, false, stoppingToken).ConfigureAwait(false);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) =>
            HandleDeliveryAsync(channel, delivery, settings, stoppingToken);

        await channel.BasicConsumeAsync(
            DiagnosticsTopology.Queue,
            autoAck: false,
            consumer,
            cancellationToken: stoppingToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Consumindo a fila {Queue} na exchange {Exchange} (vhost {VirtualHost})",
            DiagnosticsTopology.Queue,
            DiagnosticsTopology.Exchange,
            MessagingExtensions.ExpectedVirtualHost);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Desligamento: entregas em voo recebem NACK com requeue no próprio handler.
        }
    }

    private static async Task DeclareTopologyAsync(IChannel channel, RabbitMqSettings settings, CancellationToken cancellationToken)
    {
        // Mesmos argumentos de Rmq.CloudEvents.Infrastructure.QueueManager:
        // redeclaração idempotente, sem conflito com o publisher da biblioteca.
        await channel.ExchangeDeclareAsync(
            DiagnosticsTopology.DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueDeclareAsync(
            DiagnosticsTopology.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueBindAsync(
            DiagnosticsTopology.DeadLetterQueue,
            DiagnosticsTopology.DeadLetterExchange,
            routingKey: DiagnosticsTopology.Queue,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.ExchangeDeclareAsync(
            DiagnosticsTopology.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueDeclareAsync(
            DiagnosticsTopology.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = settings.DeliveryLimit,
                ["x-dead-letter-exchange"] = DiagnosticsTopology.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = DiagnosticsTopology.Queue
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueBindAsync(
            DiagnosticsTopology.Queue,
            DiagnosticsTopology.Exchange,
            routingKey: DiagnosticsTopology.RoutingKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        RabbitMqSettings settings,
        CancellationToken stoppingToken)
    {
        if (!TryUnwrap(delivery, out var ping))
        {
            _logger.LogError(
                "Envelope inválido na fila {Queue} (exchange={Exchange}, routingKey={RoutingKey}): " +
                "descartado sem requeue para a DLQ",
                DiagnosticsTopology.Queue,
                delivery.Exchange,
                delivery.RoutingKey);
            await TryNackAsync(channel, delivery.DeliveryTag, requeue: false).ConfigureAwait(false);
            return;
        }

        var attempt = 0;
        while (true)
        {
            attempt++;
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation(
                    "DiagnosticPing {PingId} consumido da fila {Queue} " +
                    "(correlationId={CorrelationId}, causationId={CausationId}, attempt={Attempt})",
                    ping.PingId,
                    DiagnosticsTopology.Queue,
                    ping.CorrelationId,
                    ping.CausationId,
                    attempt);

                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                await TryNackAsync(channel, delivery.DeliveryTag, requeue: true).ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                if (attempt >= settings.MaxAttempts)
                {
                    _logger.LogError(
                        exception,
                        "DiagnosticPing {PingId} falhou após {MaxAttempts} tentativas " +
                        "(correlationId={CorrelationId}, causationId={CausationId}): NACK sem requeue para a DLQ",
                        ping.PingId,
                        settings.MaxAttempts,
                        ping.CorrelationId,
                        ping.CausationId);
                    await TryNackAsync(channel, delivery.DeliveryTag, requeue: false).ConfigureAwait(false);
                    return;
                }

                var delay = ComputeBackoff(attempt, settings.InitialDelaySeconds);

                _logger.LogWarning(
                    exception,
                    "Falha ao processar DiagnosticPing {PingId} (tentativa {Attempt}/{MaxAttempts}, " +
                    "correlationId={CorrelationId}): novo retry em {DelayMs}ms",
                    ping.PingId,
                    attempt,
                    settings.MaxAttempts,
                    ping.CorrelationId,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private static bool TryUnwrap(BasicDeliverEventArgs delivery, out DiagnosticPing ping)
    {
        ping = new DiagnosticPing(string.Empty, string.Empty, string.Empty, DateTimeOffset.UtcNow);

        JsonDocument envelope;
        try
        {
            envelope = JsonDocument.Parse(delivery.Body);
        }
        catch (JsonException)
        {
            return false;
        }

        using (envelope)
        {
            if (!envelope.RootElement.TryGetProperty("data", out var data))
            {
                return false;
            }

            DiagnosticPing? candidate;
            try
            {
                candidate = data.Deserialize<DiagnosticPing>(JsonOptions);
            }
            catch (JsonException)
            {
                return false;
            }

            if (candidate is null || string.IsNullOrWhiteSpace(candidate.PingId))
            {
                return false;
            }

            var correlationId = string.IsNullOrWhiteSpace(candidate.CorrelationId)
                ? ReadHeader(delivery, CorrelationHeader)
                : candidate.CorrelationId;
            var causationId = string.IsNullOrWhiteSpace(candidate.CausationId)
                ? ReadHeader(delivery, CausationHeader)
                : candidate.CausationId;

            if (string.IsNullOrWhiteSpace(correlationId) || string.IsNullOrWhiteSpace(causationId))
            {
                return false;
            }

            ping = candidate with { CorrelationId = correlationId, CausationId = causationId };
            return true;
        }
    }

    private static string ReadHeader(BasicDeliverEventArgs delivery, string key)
    {
        var headers = delivery.BasicProperties?.Headers;
        if (headers is null || !headers.TryGetValue(key, out var value))
        {
            return string.Empty;
        }

        return value switch
        {
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => value?.ToString() ?? string.Empty
        };
    }

    private static TimeSpan ComputeBackoff(int attempt, int initialDelaySeconds)
    {
        var exponential = TimeSpan.FromSeconds(initialDelaySeconds) * Math.Pow(2, attempt - 1);
        var capped = exponential > MaxBackoff ? MaxBackoff : exponential;
        var jitter = capped * (0.9 + Random.Shared.NextDouble() * 0.2);
        return jitter;
    }

    private static async Task TryNackAsync(IChannel channel, ulong deliveryTag, bool requeue)
    {
        try
        {
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: requeue).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Canal em teardown durante o desligamento: nada a fazer.
        }
    }
}
