using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Infra.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rmq.CloudEvents.Configuration;
using Rmq.CloudEvents.Extensions;

namespace LocalizeStay.Booking.Api.Extensions;

public static class MessagingExtensions
{
    /// <summary>
    /// Vhost dedicado do Localize Stay no broker compartilhado com o ecad-sba.
    /// Nenhum código do projeto declara ou consome recursos fora deste vhost.
    /// </summary>
    public const string ExpectedVirtualHost = "localize-stay";

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
                ClientProvidedName = "booking-api"
            };

            options.DefaultCloudEvents = new CloudEventsOptions
            {
                Source = new Uri(section.GetValue("Source", "/booking") ?? "/booking", UriKind.Relative),
                DefaultType = section.GetValue("DefaultType", "com.localizestay.diagnostics")
                    ?? "com.localizestay.diagnostics"
            };

            options.DefaultRetry = new RetryOptions
            {
                MaxAttempts = section.GetValue("MaxAttempts", 5),
                InitialDelay = TimeSpan.FromSeconds(section.GetValue("InitialDelaySeconds", 1)),
                BackoffType = BackoffType.Exponential,
                UseJitter = true
            };

            options.Exchanges[Messaging.DiagnosticsTopology.Exchange] = new ExchangeOptions
            {
                Name = Messaging.DiagnosticsTopology.Exchange,
                Durable = true,
                AutoDelete = false
            };

            // Evento de negócio booking.reservation_requested (RF-01, ADR-002):
            // topologia declarada de forma idempotente no boot, ao lado do diagnóstico.
            options.Exchanges[ReservationRequestedTopology.Exchange] = new ExchangeOptions
            {
                Name = ReservationRequestedTopology.Exchange,
                Durable = true,
                AutoDelete = false
            };

            // Evento de negócio booking.payment_requested (F03, ADR-002):
            // topologia declarada de forma idempotente no boot, ao lado das demais.
            options.Exchanges[PaymentRequestedTopology.Exchange] = new ExchangeOptions
            {
                Name = PaymentRequestedTopology.Exchange,
                Durable = true,
                AutoDelete = false
            };
        });

        services.AddScoped<IReservationRequestedPublisher, ReservationRequestedRmqPublisher>();
        services.AddScoped<IPaymentRequestedPublisher, PaymentRequestedRmqPublisher>();

        return services;
    }
}
