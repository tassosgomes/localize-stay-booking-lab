using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Booking.Application.Reservations;

// Orquestra RF-01: busca por correlação, guarda RN-11, transição, persistência
// antes da publicação e publicação best-effort (RF-03/DP-04).
public sealed class ConfirmReservationCommandHandler(
    IReservationRepository repository,
    IReservationConfirmedPublisher publisher,
    ILogger<ConfirmReservationCommandHandler> logger)
    : ICommandHandler<ConfirmReservationCommand, ConfirmReservationOutcome>
{
    public async Task<ConfirmReservationOutcome> HandleAsync(
        ConfirmReservationCommand command, CancellationToken cancellationToken)
    {
        var reservation = await repository
            .GetByCorrelationIdAsync(command.CorrelationId, cancellationToken)
            .ConfigureAwait(false);

        if (reservation is null)
        {
            logger.LogWarning(
                "payment.payment_authorized não correlacionável: nenhuma saga para " +
                "correlationId={CorrelationId}", command.CorrelationId);
            return ConfirmReservationOutcome.NotCorrelatable;
        }

        if (reservation.Status != ReservationStatus.Solicitada)
        {
            logger.LogWarning(
                "payment.payment_authorized tardio/duplicado ignorado: Reservation {ReservationId} " +
                "já {Status} (correlationId={CorrelationId})",
                reservation.Id, reservation.Status, command.CorrelationId);
            return ConfirmReservationOutcome.AlreadyTerminal;
        }

        reservation.Confirm();
        await repository.UpdateAsync(reservation, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Reservation {ReservationId} confirmada (correlationId={CorrelationId})",
            reservation.Id, command.CorrelationId);

        try
        {
            await publisher.PublishAsync(reservation, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Evento booking.reservation_confirmed publicado para a Reservation {ReservationId}",
                reservation.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Falha best-effort ao publicar booking.reservation_confirmed da Reservation " +
                "{ReservationId}; estado terminal já persistido",
                reservation.Id);
        }

        return ConfirmReservationOutcome.Confirmed;
    }
}
