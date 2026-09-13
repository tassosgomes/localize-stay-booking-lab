using System.Text.Json.Serialization;
using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Api.Contracts;

// Response de uma Reservation recém-criada — espelha ReservationResponse do
// api-contract.yaml (mesma ordem de campos). pricePerNight/totalAmount saem
// como string decimal de 2 casas via MoneyStringJsonConverter.
public sealed record ReservationResponseDto(
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
    DateTime CreatedAt)
{
    public static ReservationResponseDto From(Reservation reservation) => new(
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
        reservation.CreatedAt);
}
