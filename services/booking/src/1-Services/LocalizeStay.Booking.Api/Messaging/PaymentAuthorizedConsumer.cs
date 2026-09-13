using LocalizeStay.Booking.Application;
using LocalizeStay.Booking.Application.Reservations;
using Microsoft.Extensions.Logging;
using Rmq.CloudEvents.Consuming;

namespace LocalizeStay.Booking.Api.Messaging;

// Adapter fino (driving): sem regra de negócio, só resolve correlationId e
// despacha ConfirmReservationCommand via IDispatcher.
public sealed class PaymentAuthorizedConsumer(
    IDispatcher dispatcher, ILogger<PaymentAuthorizedConsumer> logger)
    : IRmqMessageHandler<PaymentAuthorizedMessage>
{
    public async Task HandleAsync(
        PaymentAuthorizedMessage message, MessageContext context, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.CorrelationId, out var correlationId))
        {
            logger.LogWarning(
                "payment.payment_authorized com correlationId ausente/inválido na fila {Queue}: " +
                "ignorado (DP-03)", context.QueueName);
            return;
        }

        var outcome = await dispatcher
            .SendAsync(new ConfirmReservationCommand(correlationId), cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "payment.payment_authorized consumido (correlationId={CorrelationId}, outcome={Outcome})",
            correlationId, outcome);
    }
}
