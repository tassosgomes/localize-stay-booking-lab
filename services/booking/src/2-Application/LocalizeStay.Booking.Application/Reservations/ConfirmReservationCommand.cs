namespace LocalizeStay.Booking.Application.Reservations;

// Command de confirmação da Reservation após payment.payment_authorized (RF-01).
public sealed record ConfirmReservationCommand(Guid CorrelationId) : ICommand<ConfirmReservationOutcome>;

public enum ConfirmReservationOutcome
{
    Confirmed,
    AlreadyTerminal,
    NotCorrelatable
}
