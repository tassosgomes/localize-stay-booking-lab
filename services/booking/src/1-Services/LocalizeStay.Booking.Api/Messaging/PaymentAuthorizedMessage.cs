using System.Text.Json.Serialization;

namespace LocalizeStay.Booking.Api.Messaging;

// Record de desserialização defensiva do schema provisório de Payment (RF-01):
// somente correlationId e timestamp informativo; campos extras são ignorados.
public sealed record PaymentAuthorizedMessage(
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("authorizedAt")] DateTimeOffset? AuthorizedAt);
