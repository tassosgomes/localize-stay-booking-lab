namespace LocalizeStay.Notification.Worker.Messaging;

/// <summary>
/// Nomes da topologia de diagnóstico (V-03) dentro do vhost <c>/localize-stay</c>,
/// espelhando Booking. Os argumentos de declaração (quorum, DLX/DLQ, bindings)
/// seguem as convenções de <c>Rmq.CloudEvents.Infrastructure.QueueManager</c>
/// para que publisher (biblioteca) e consumer (este worker) interoperem sem
/// conflito de redeclaração.
/// </summary>
public static class DiagnosticsTopology
{
    public const string Exchange = "diagnostics.topic";

    public const string RoutingKey = "diagnostics.ping";

    public const string Queue = "notification.diagnostics";

    public const string DeadLetterExchange = "notification.diagnostics.dlx";

    public const string DeadLetterQueue = "notification.diagnostics.dlq";

    public const string CloudEventType = "com.localizestay.diagnostics.ping.v1";
}
