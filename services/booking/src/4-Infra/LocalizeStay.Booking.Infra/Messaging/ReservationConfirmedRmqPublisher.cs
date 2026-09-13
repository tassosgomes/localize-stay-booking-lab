using System.Text.Json.Serialization;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.Extensions.Logging;
using Rmq.CloudEvents.Publishing;

namespace LocalizeStay.Booking.Infra.Messaging;

// Nomes da topologia do evento de negócio booking.reservation_confirmed dentro do
// vhost localize-stay (F04, ADR-002): exchange e routing key modeladas 1:1
// com o evento, conforme api-contract.yaml (AsyncAPI já Aprovado).
public static class ReservationConfirmedTopology
{
    public const string Exchange = "booking.reservation_confirmed";

    public const string RoutingKey = "booking.reservation_confirmed";

    public const string CloudEventType = "com.localizestay.booking.reservation_confirmed.v1";
}

// Adapta o IRmqPublisher genérico do Rmq.CloudEvents à porta de aplicação
// IReservationConfirmedPublisher. Correlação: correlationId = causationId =
// reservation.Saga.CorrelationId (auto-causação, sem identificador de evento
// próprio em payment.payment_authorized ainda).
public sealed class ReservationConfirmedRmqPublisher(
    IRmqPublisher publisher,
    ILogger<ReservationConfirmedRmqPublisher> logger) : IReservationConfirmedPublisher
{
    public async Task PublishAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var correlationId = reservation.Saga.CorrelationId;

        var payload = new ReservationConfirmedEvent(
            correlationId,
            correlationId,
            reservation.Id,
            reservation.AccommodationId,
            reservation.GuestReference,
            reservation.CheckIn,
            reservation.CheckOut,
            DateTimeOffset.UtcNow);

        logger.LogDebug(
            "Publicando {CloudEventType} na exchange {Exchange} com routing key {RoutingKey} " +
            "(correlationId={CorrelationId})",
            ReservationConfirmedTopology.CloudEventType,
            ReservationConfirmedTopology.Exchange,
            ReservationConfirmedTopology.RoutingKey,
            correlationId);

        await publisher
            .PublishToTopicAsync(
                ReservationConfirmedTopology.Exchange,
                ReservationConfirmedTopology.RoutingKey,
                payload,
                new Dictionary<string, object>
                {
                    ["x-correlation-id"] = correlationId.ToString(),
                    ["x-causation-id"] = correlationId.ToString()
                },
                ReservationConfirmedTopology.CloudEventType,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // Payload publicado dentro do envelope CloudEvents (campo data). Nomes
    // camelCase exigidos pelo api-contract.yaml (F04).
    private sealed record ReservationConfirmedEvent(
        [property: JsonPropertyName("correlationId")] Guid CorrelationId,
        [property: JsonPropertyName("causationId")] Guid CausationId,
        [property: JsonPropertyName("reservationId")] Guid ReservationId,
        [property: JsonPropertyName("accommodationId")] Guid AccommodationId,
        [property: JsonPropertyName("guestReference")] string GuestReference,
        [property: JsonPropertyName("checkIn")] DateOnly CheckIn,
        [property: JsonPropertyName("checkOut")] DateOnly CheckOut,
        [property: JsonPropertyName("confirmedAt")] DateTimeOffset ConfirmedAt);
}
