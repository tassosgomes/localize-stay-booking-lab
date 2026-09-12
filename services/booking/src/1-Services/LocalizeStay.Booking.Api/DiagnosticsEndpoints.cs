using LocalizeStay.Booking.Api.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Rmq.CloudEvents.Publishing;

namespace LocalizeStay.Booking.Api;

public sealed record PingAcceptedResponse(
    string PingId,
    string CorrelationId,
    string CausationId,
    DateTimeOffset SentAt);

public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/diagnostics/ping", HandlePingAsync);
        return endpoints;
    }

    private static async Task<IResult> HandlePingAsync(
        IRmqPublisher publisher,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("LocalizeStay.Booking.Api.Diagnostics");
        // NOTA (V-03): endpoint técnico de diagnóstico, fora do contrato público.
        // Remover ou isolar sob um flag quando o primeiro PRD de Booking chegar.
        // O ping origina a cadeia de correlação: causationId == correlationId.
        var ping = new DiagnosticPing(
            PingId: Guid.NewGuid().ToString("N"),
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: string.Empty,
            SentAt: DateTimeOffset.UtcNow);
        ping = ping with { CausationId = ping.CorrelationId };

        var headers = new Dictionary<string, object>
        {
            ["x-correlation-id"] = ping.CorrelationId,
            ["x-causation-id"] = ping.CausationId
        };

        await publisher.PublishToTopicAsync(
            DiagnosticsTopology.Exchange,
            DiagnosticsTopology.RoutingKey,
            ping,
            headers,
            DiagnosticsTopology.CloudEventType,
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "DiagnosticPing {PingId} publicado na exchange {Exchange} (correlationId={CorrelationId}, causationId={CausationId})",
            ping.PingId,
            DiagnosticsTopology.Exchange,
            ping.CorrelationId,
            ping.CausationId);

        return Results.Accepted(
            "/internal/diagnostics/ping",
            new PingAcceptedResponse(ping.PingId, ping.CorrelationId, ping.CausationId, ping.SentAt));
    }
}
