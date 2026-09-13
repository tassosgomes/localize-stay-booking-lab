using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Application.Reservations;

// Command da solicitação de reserva (RF-01). Regras de período/hóspedes são
// rejeições de negócio (422) aplicadas pelo Domain, não validação de shape.
public sealed record RequestReservationCommand(
    Guid AccommodationId,
    string GuestReference,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int GuestsCount) : ICommand<Reservation>;
