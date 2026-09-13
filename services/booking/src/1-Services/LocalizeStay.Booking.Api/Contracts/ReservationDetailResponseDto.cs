using System.Text.Json.Serialization;
using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Api.Contracts;

// Response da consulta de uma Reservation existente — espelha
// ReservationDetailResponse do api-contract.yaml de F02 (mesma ordem de
// campos). pricePerNight/totalAmount saem como string decimal de 2 casas via
// MoneyStringJsonConverter. A tradução SagaState → sagaStatus acontece aqui,
// não no domínio: PaymentPending→"pendente", Authorized→"autorizado",
// Rejected→"rejeitado". cancellationReason é nulo quando ausente.
public sealed record ReservationDetailResponseDto(
    Guid Id,
    Guid AccommodationId,
    string GuestReference,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int GuestsCount,
    string Status,
    [property: JsonConverter(typeof(MoneyStringJsonConverter))] decimal PricePerNight,
    string Currency,
    [property: JsonConverter(typeof(MoneyStringJsonConverter))] decimal TotalAmount,
    DateTime CreatedAt,
    string SagaStatus,
    Guid CorrelationId,
    string? CancellationReason)
{
    public static ReservationDetailResponseDto From(Reservation reservation) => new(
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
        reservation.CreatedAt,
        MapSagaStatus(reservation.Saga.State),
        reservation.Saga.CorrelationId,
        reservation.Saga.CancellationReason);

    private static string MapSagaStatus(SagaState state) => state switch
    {
        SagaState.PaymentPending => "pendente",
        SagaState.Authorized => "autorizado",
        SagaState.Rejected => "rejeitado",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Estado de saga desconhecido."),
    };
}
