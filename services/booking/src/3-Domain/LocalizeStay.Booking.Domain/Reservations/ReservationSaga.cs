namespace LocalizeStay.Booking.Domain.Reservations;

public sealed class ReservationSaga
{
    private ReservationSaga()
    {
    }

    internal ReservationSaga(Guid id, Guid reservationId, Guid correlationId, SagaState state, DateTime createdAt)
    {
        Id = id;
        ReservationId = reservationId;
        CorrelationId = correlationId;
        State = state;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ReservationId { get; private set; }

    public Guid CorrelationId { get; private set; }

    public SagaState State { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? PaymentRequestSentAt { get; private set; }

    public void MarkPaymentRequestSent(DateTime occurredAt)
    {
        PaymentRequestSentAt = occurredAt;
    }

    public void MarkAuthorized()
    {
        State = SagaState.Authorized;
    }

    public void MarkRejected(string cancellationReason)
    {
        State = SagaState.Rejected;
        CancellationReason = cancellationReason;
    }

    public string? CancellationReason { get; private set; }
}
