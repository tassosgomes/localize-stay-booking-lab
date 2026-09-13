namespace LocalizeStay.Booking.Api.Contracts;

// Shape HTTP de CreateReservationRequest do api-contract.yaml. Tipo/obrigatoriedade
// de data e inteiros são garantidos pelo binding JSON; regras de negócio não vivem aqui.
public sealed record CreateReservationRequestDto(
    Guid AccommodationId,
    string GuestReference,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int GuestsCount);
