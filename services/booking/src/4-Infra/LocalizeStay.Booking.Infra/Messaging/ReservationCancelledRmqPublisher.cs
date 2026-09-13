using System.Text.Json.Serialization;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.Extensions.Logging;
using Rmq.CloudEvents.Publishing;

namespace LocalizeStay.Booking.Infra.Messaging;

// Nomes da topologia do evento de negócio booking.reservation_cancelled dentro do
// vhost localize-stay (F04, ADR-002): exchange e routing key modeladas 1:1
// com o evento, conforme api-contract.yaml (AsyncAPI já Aprovado).
public static class ReservationCancelledTopology
{
    public const string Exchange = "booking.reservation_cancelled";

    public const string RoutingKey = "booking.reservation_cancelled";

    public const string CloudEventType = "com.localizestay.booking.reservation_cancelled.v1";
}

// Adapta o IRmqPublisher genérico do Rmq.CloudEvents à porta de aplicação
// IReservationCancelledPublisher. Correlação: correlationId = causationId =
// reservation.Saga.CorrelationId (auto-causação, sem identificador de evento
// próprio em payment.payment_rejected ainda).
public sealed class ReservationCancelledRmqPublisher(
    IRmqPublisher publisher,
    ILogger<ReservationCancelledRmqPublisher> logger) : IReservationCancelledPublisher
{
    public async Task PublishAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var correlationId = reservation.Saga.CorrelationId;

        var payload = new ReservationCancelledEvent(
            correlationId,
            correlationId,
            reservation.Id,
            reservation.AccommodationId,
            reservation.GuestReference,
            reservation.CheckIn,
            reservation.CheckOut,
            DateTimeOffset.UtcNow,
            reservation.Saga.CancellationReason ?? string.Empty);

        logger.LogDebug(
            "Publicando {CloudEventType} na exchange {Exchange} com routing key {RoutingKey} " +
            "(correlationId={CorrelationId})",
            ReservationCancelledTopology.CloudEventType,
            ReservationCancelledTopology.Exchange,
            ReservationCancelledTopology.RoutingKey,
            correlationId);

        await publisher
            .PublishToTopicAsync(
                ReservationCancelledTopology.Exchange,
                ReservationCancelledTopology.RoutingKey,
                payload,
                new Dictionary<string, object>
                {
                    ["x-correlation-id"] = correlationId.ToString(),
                    ["x-causation-id"] = correlationId.ToString()
                },
                ReservationCancelledTopology.CloudEventType,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // Payload publicado dentro do envelope CloudEvents (campo data). Nomes
    // camelCase exigidos pelo api-contract.yaml (F04).
    private sealed record ReservationCancelledEvent(
        [property: JsonPropertyName("correlationId")] Guid CorrelationId,
        [property: JsonPropertyName("causationId")] Guid CausationId,
        [property: JsonPropertyName("reservationId")] Guid ReservationId,
        [property: JsonPropertyName("accommodationId")] Guid AccommodationId,
        [property: JsonPropertyName("guestReference")] string GuestReference,
        [property: JsonPropertyName("checkIn")] DateOnly CheckIn,
        [property: JsonPropertyName("checkOut")] DateOnly CheckOut,
        [property: JsonPropertyName("cancelledAt")] DateTimeOffset CancelledAt,
        [property: JsonPropertyName("cancellationReason")] string CancellationReason);
}
