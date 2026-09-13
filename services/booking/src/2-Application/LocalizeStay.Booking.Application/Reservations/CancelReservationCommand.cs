namespace LocalizeStay.Booking.Application.Reservations;

// Command de cancelamento da Reservation após payment.payment_rejected (RF-02, DP-01).
public sealed record CancelReservationCommand(Guid CorrelationId, string Reason) : ICommand<CancelReservationOutcome>;

public enum CancelReservationOutcome
{
    Cancelled,
    AlreadyTerminal,
    NotCorrelatable
}
