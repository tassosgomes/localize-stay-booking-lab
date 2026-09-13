namespace LocalizeStay.Booking.Api.Messaging;

// Topologia de consumo de payment.payment_rejected (F04, ADR-002):
// exchange mantida por Payment + fila própria durável de Booking + binding 1:1.
public static class PaymentRejectedTopology
{
    public const string Exchange = "payment.payment_rejected";

    public const string Queue = "booking.payment_rejected";

    public const string RoutingKey = "payment.payment_rejected";
}
