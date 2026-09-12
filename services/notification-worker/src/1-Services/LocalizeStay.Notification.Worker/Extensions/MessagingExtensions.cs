using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rmq.CloudEvents.Configuration;
using Rmq.CloudEvents.Extensions;

namespace LocalizeStay.Notification.Worker.Extensions;

public static class MessagingExtensions
{
    /// <summary>
    /// Vhost dedicado do Localize Stay no broker compartilhado com o ecad-sba.
    /// Nenhum código do projeto declara ou consome recursos fora deste vhost.
    /// </summary>
    public const string ExpectedVirtualHost = "localize-stay";

    /// <summary>
    /// Registra o <c>Rmq.CloudEvents</c> (publisher disponível para os PRDs futuros
    /// e convenções de retry/CloudEvents). O consumo nesta etapa é feito pelo
    /// <c>DiagnosticPingConsumer</c>, que implementa explicitamente o loop
    /// ACK/NACK com retry, backoff e DLQ — o pipeline de consumo da versão 1.1.1
    /// da biblioteca é interno e não expõe hook para um <c>BackgroundService</c>
    /// próprio; este worker não duplica nem contorna a biblioteca, apenas hospeda
    /// o loop com as mesmas convenções de topologia.
    /// </summary>
    public static IServiceCollection AddMessagingConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = RabbitMqSettings.Read(configuration);
        var section = configuration.GetSection("RabbitMQ");

        services.AddRmqCloudEvents(options =>
        {
            options.Connection = new RmqConnectionOptions
            {
                HostName = settings.HostName,
                Port = settings.Port,
                UserName = settings.UserName,
                Password = settings.Password,
                VirtualHost = ExpectedVirtualHost,
                ClientProvidedName = "notification-worker"
            };

            options.DefaultCloudEvents = new CloudEventsOptions
            {
                Source = new Uri(settings.Source, UriKind.Relative),
                DefaultType = settings.DefaultType
            };

            options.DefaultRetry = new RetryOptions
            {
                MaxAttempts = settings.MaxAttempts,
                InitialDelay = TimeSpan.FromSeconds(settings.InitialDelaySeconds),
                BackoffType = BackoffType.Exponential,
                UseJitter = true
            };

            options.Exchanges[Messaging.DiagnosticsTopology.Exchange] = new ExchangeOptions
            {
                Name = Messaging.DiagnosticsTopology.Exchange,
                Durable = true,
                AutoDelete = false
            };
        });

        return services;
    }
}
