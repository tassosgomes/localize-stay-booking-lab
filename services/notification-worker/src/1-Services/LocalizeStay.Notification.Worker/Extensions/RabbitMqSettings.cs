using Microsoft.Extensions.Configuration;

namespace LocalizeStay.Notification.Worker.Extensions;

/// <summary>
/// Leitura validada da seção RabbitMQ. Falha rápido quando a conexão está
/// incompleta ou aponta para outro vhost (regra de isolamento do broker
/// compartilhado com o ecad-sba).
/// </summary>
internal sealed record RabbitMqSettings(
    string HostName,
    int Port,
    string UserName,
    string Password,
    string Source,
    string DefaultType,
    int MaxAttempts,
    int InitialDelaySeconds,
    int DeliveryLimit,
    ushort PrefetchCount)
{
    public static bool TryRead(IConfiguration configuration, out RabbitMqSettings? settings)
    {
        var section = configuration.GetSection("RabbitMQ");

        var hostName = section.GetValue<string>("HostName");
        var userName = section.GetValue<string>("UserName");
        var password = section.GetValue<string>("Password");
        var virtualHost = section.GetValue<string>("VirtualHost");

        if (string.IsNullOrWhiteSpace(hostName)
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(password)
            || !string.Equals(virtualHost, MessagingExtensions.ExpectedVirtualHost, StringComparison.Ordinal))
        {
            settings = null;
            return false;
        }

        settings = new RabbitMqSettings(
            HostName: hostName,
            Port: section.GetValue("Port", 5672),
            UserName: userName,
            Password: password,
            Source: section.GetValue("Source", "/notification-worker") ?? "/notification-worker",
            DefaultType: section.GetValue("DefaultType", "com.localizestay.diagnostics")
                ?? "com.localizestay.diagnostics",
            MaxAttempts: Math.Max(1, section.GetValue("MaxAttempts", 5)),
            InitialDelaySeconds: Math.Max(0, section.GetValue("InitialDelaySeconds", 1)),
            DeliveryLimit: Math.Max(1, section.GetValue("DeliveryLimit", 5)),
            PrefetchCount: (ushort)Math.Max(0, section.GetValue("PrefetchCount", 0)));
        return true;
    }

    public static RabbitMqSettings Read(IConfiguration configuration)
    {
        if (TryRead(configuration, out var settings) && settings is not null)
        {
            return settings;
        }

        throw new InvalidOperationException(
            "RabbitMQ incompleto: defina RabbitMQ:HostName, RabbitMQ:UserName e RabbitMQ:Password " +
            $"com RabbitMQ:VirtualHost='{MessagingExtensions.ExpectedVirtualHost}'. " +
            "A senha vai em 'dotnet user-secrets set \"RabbitMQ:Password\" \"<senha>\"', nunca versionada.");
    }
}
