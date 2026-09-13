using LocalizeStay.Booking.Application;
using LocalizeStay.Booking.Application.Reservations;
using Microsoft.Extensions.Logging;
using Rmq.CloudEvents.Consuming;

namespace LocalizeStay.Booking.Api.Messaging;

// Adapter fino (driving): sem regra de negócio, só resolve correlationId e
// despacha CancelReservationCommand com o motivo fixo de negócio (DP-01).
public sealed class PaymentRejectedConsumer(
    IDispatcher dispatcher, ILogger<PaymentRejectedConsumer> logger)
    : IRmqMessageHandler<PaymentRejectedMessage>
{
    private const string RejectionReason = "Pagamento rejeitado pela simulação de Payment.";

    public async Task HandleAsync(
        PaymentRejectedMessage message, MessageContext context, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.CorrelationId, out var correlationId))
        {
            logger.LogWarning(
                "payment.payment_rejected com correlationId ausente/inválido na fila {Queue}: " +
                "ignorado (DP-03)", context.QueueName);
            return;
        }

        var outcome = await dispatcher
            .SendAsync(new CancelReservationCommand(correlationId, RejectionReason), cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "payment.payment_rejected consumido (correlationId={CorrelationId}, outcome={Outcome})",
            correlationId, outcome);
    }
}
