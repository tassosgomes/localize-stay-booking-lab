using LocalizeStay.Booking.Domain.Reservations;
using Xunit;

namespace LocalizeStay.Booking.UnitTests.Reservations;

// F03 (RF-01): PaymentRequestSentAt é um atributo simples, sem transição de
// SagaState — MarkPaymentRequestSent apenas atribui o instante recebido.
public sealed class ReservationSagaTests
{
    private static readonly Guid AccommodationId = Guid.NewGuid();

    private static readonly DateOnly CheckIn = new(2026, 10, 1);

    private static readonly AvailabilityFacts ValidFacts = new(
        Active: true,
        MaxGuests: 4,
        AvailableForPeriod: true,
        PricePerNight: 350.00m,
        Currency: "BRL");

    private static ReservationSaga CreateSaga() =>
        Reservation.Create(AccommodationId, "guest-ref", CheckIn, CheckIn.AddDays(2), 2, ValidFacts).Saga;

    [Fact]
    public void PaymentRequestSentAt_is_null_right_after_creation()
    {
        var saga = CreateSaga();

        Assert.Null(saga.PaymentRequestSentAt);
    }

    [Fact]
    public void MarkPaymentRequestSent_records_the_exact_instant_received()
    {
        var saga = CreateSaga();
        var occurredAt = new DateTime(2026, 9, 12, 14, 22, 5, DateTimeKind.Utc);

        saga.MarkPaymentRequestSent(occurredAt);

        Assert.Equal(occurredAt, saga.PaymentRequestSentAt);
    }

    [Fact]
    public void MarkPaymentRequestSent_does_not_change_SagaState()
    {
        var saga = CreateSaga();

        saga.MarkPaymentRequestSent(DateTime.UtcNow);

        Assert.Equal(SagaState.PaymentPending, saga.State);
    }
}
