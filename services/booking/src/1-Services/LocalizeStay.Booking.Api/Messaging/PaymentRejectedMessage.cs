using System.Text.Json.Serialization;

namespace LocalizeStay.Booking.Api.Messaging;

// Record de desserialização defensiva do schema provisório de Payment (RF-02):
// somente correlationId e timestamp informativo; campos extras são ignorados.
public sealed record PaymentRejectedMessage(
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("rejectedAt")] DateTimeOffset? RejectedAt);
