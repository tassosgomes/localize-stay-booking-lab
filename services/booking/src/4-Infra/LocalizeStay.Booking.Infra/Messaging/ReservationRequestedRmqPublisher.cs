using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.Extensions.Logging;
using Rmq.CloudEvents.Publishing;

namespace LocalizeStay.Booking.Infra.Messaging;

// Nomes da topologia do evento de negócio booking.reservation_requested
// dentro do vhost localize-stay (convenção <domínio>.<propósito> da fundação,
// ADR-002): exchange booking.reservation-events + routing key
// reservation.requested; cloudEventType versionado .v1.
public static class ReservationRequestedTopology
{
    public const string Exchange = "booking.reservation-events";

    public const string RoutingKey = "reservation.requested";

    public const string CloudEventType = "booking.reservation_requested.v1";
}

// Adapta o IRmqPublisher genérico do Rmq.CloudEvents (já configurado pela
// fundação) à porta de aplicação IReservationRequestedPublisher. Correlação:
// correlationId = causationId = Reservation.Id (evento autocausado — primeiro
// da saga, sem evento anterior para propagar).
public sealed class ReservationRequestedRmqPublisher(
    IRmqPublisher publisher,
    ILogger<ReservationRequestedRmqPublisher> logger) : IReservationRequestedPublisher
{
    public async Task PublishAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var payload = new ReservationRequestedEvent(
            reservation.Id,
            reservation.AccommodationId,
            reservation.GuestReference,
            reservation.CheckIn,
            reservation.CheckOut,
            reservation.GuestsCount,
            reservation.Status.ToString().ToLowerInvariant(),
            reservation.PricePerNight,
            reservation.Currency,
            reservation.TotalAmount,
            DateTimeOffset.UtcNow);

        logger.LogDebug(
            "Publicando {CloudEventType} na exchange {Exchange} com routing key {RoutingKey} " +
            "(correlationId={CorrelationId})",
            ReservationRequestedTopology.CloudEventType,
            ReservationRequestedTopology.Exchange,
            ReservationRequestedTopology.RoutingKey,
            reservation.Id);

        await publisher
            .PublishToTopicAsync(
                ReservationRequestedTopology.Exchange,
                ReservationRequestedTopology.RoutingKey,
                payload,
                new Dictionary<string, object>
                {
                    ["x-correlation-id"] = reservation.Id.ToString(),
                    ["x-causation-id"] = reservation.Id.ToString()
                },
                ReservationRequestedTopology.CloudEventType,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // Payload publicado dentro do envelope CloudEvents (campo data). Valores
    // monetários seguem como número aqui: a regra de string decimal de 2 casas
    // é exclusiva do contrato HTTP (ReservationResponseDto).
    private sealed record ReservationRequestedEvent(
        Guid ReservationId,
        Guid AccommodationId,
        string GuestReference,
        DateOnly CheckIn,
        DateOnly CheckOut,
        int GuestsCount,
        string Status,
        decimal PricePerNight,
        string Currency,
        decimal TotalAmount,
        DateTimeOffset OccurredAt);
}
