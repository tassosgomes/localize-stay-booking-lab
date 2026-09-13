using System.Globalization;
using System.Text.Json.Serialization;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.Extensions.Logging;
using Rmq.CloudEvents.Publishing;

namespace LocalizeStay.Booking.Infra.Messaging;

// Nomes da topologia do evento de negócio booking.payment_requested dentro do
// vhost localize-stay (F03, ADR-002): exchange e routing key modeladas 1:1
// com o evento, conforme api-contract.yaml (AsyncAPI já Aprovado).
public static class PaymentRequestedTopology
{
    public const string Exchange = "booking.payment_requested";

    public const string RoutingKey = "booking.payment_requested";

    public const string CloudEventType = "com.localizestay.booking.payment_requested.v1";
}

// Adapta o IRmqPublisher genérico do Rmq.CloudEvents (mesma instância já
// configurada pela fundação/F01) à porta de aplicação
// IPaymentRequestedPublisher. Correlação: correlationId = causationId =
// reservation.Saga.CorrelationId (evento autocausado, origina a etapa
// assíncrona da saga a partir da criação síncrona da Reservation).
public sealed class PaymentRequestedRmqPublisher(
    IRmqPublisher publisher,
    ILogger<PaymentRequestedRmqPublisher> logger) : IPaymentRequestedPublisher
{
    public async Task PublishAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var correlationId = reservation.Saga.CorrelationId;

        var payload = new PaymentRequestedEvent(
            correlationId,
            correlationId,
            reservation.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
            reservation.Currency,
            DateTimeOffset.UtcNow);

        logger.LogDebug(
            "Publicando {CloudEventType} na exchange {Exchange} com routing key {RoutingKey} " +
            "(correlationId={CorrelationId})",
            PaymentRequestedTopology.CloudEventType,
            PaymentRequestedTopology.Exchange,
            PaymentRequestedTopology.RoutingKey,
            correlationId);

        await publisher
            .PublishToTopicAsync(
                PaymentRequestedTopology.Exchange,
                PaymentRequestedTopology.RoutingKey,
                payload,
                new Dictionary<string, object>
                {
                    ["x-correlation-id"] = correlationId.ToString(),
                    ["x-causation-id"] = correlationId.ToString()
                },
                PaymentRequestedTopology.CloudEventType,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // Payload publicado dentro do envelope CloudEvents (campo data). Nomes
    // camelCase exigidos pelo api-contract.yaml (F03) — diferente do payload
    // de reservation_requested, que não tem essa exigência de contrato.
    private sealed record PaymentRequestedEvent(
        [property: JsonPropertyName("correlationId")] Guid CorrelationId,
        [property: JsonPropertyName("causationId")] Guid CausationId,
        [property: JsonPropertyName("totalAmount")] string TotalAmount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("requestedAt")] DateTimeOffset RequestedAt);
}
