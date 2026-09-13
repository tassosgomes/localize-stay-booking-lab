namespace LocalizeStay.Booking.Api.Messaging;

// Topologia de consumo de payment.payment_authorized (F04, ADR-002):
// exchange mantida por Payment + fila própria durável de Booking + binding 1:1.
public static class PaymentAuthorizedTopology
{
    public const string Exchange = "payment.payment_authorized";

    public const string Queue = "booking.payment_authorized";

    public const string RoutingKey = "payment.payment_authorized";
}
