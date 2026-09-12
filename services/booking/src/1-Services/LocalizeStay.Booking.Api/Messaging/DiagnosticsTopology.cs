namespace LocalizeStay.Booking.Api.Messaging;

/// <summary>
/// Nomes da topologia de diagnóstico (V-03) dentro do vhost <c>localize-stay</c>.
/// Espelhados pelo Notification Worker e cobertos pelo AsyncAPI da task 8.0;
/// os PRDs de negócio seguem o mesmo padrão <c>&lt;domínio&gt;.&lt;propósito&gt;</c>.
/// </summary>
public static class DiagnosticsTopology
{
    public const string Exchange = "diagnostics.topic";

    public const string RoutingKey = "diagnostics.ping";

    public const string Queue = "notification.diagnostics";

    public const string CloudEventType = "com.localizestay.diagnostics.ping.v1";
}
