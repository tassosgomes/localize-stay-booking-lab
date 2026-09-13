using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Booking.Application.Reservations;

// Orquestra RF-02: espelho do handler de confirmação, com motivo de negócio (DP-01).
public sealed class CancelReservationCommandHandler(
    IReservationRepository repository,
    IReservationCancelledPublisher publisher,
    ILogger<CancelReservationCommandHandler> logger)
    : ICommandHandler<CancelReservationCommand, CancelReservationOutcome>
{
    public async Task<CancelReservationOutcome> HandleAsync(
        CancelReservationCommand command, CancellationToken cancellationToken)
    {
        var reservation = await repository
            .GetByCorrelationIdAsync(command.CorrelationId, cancellationToken)
            .ConfigureAwait(false);

        if (reservation is null)
        {
            logger.LogWarning(
                "payment.payment_rejected não correlacionável: nenhuma saga para " +
                "correlationId={CorrelationId}", command.CorrelationId);
            return CancelReservationOutcome.NotCorrelatable;
        }

        if (reservation.Status != ReservationStatus.Solicitada)
        {
            logger.LogWarning(
                "payment.payment_rejected tardio/duplicado ignorado: Reservation {ReservationId} " +
                "já {Status} (correlationId={CorrelationId})",
                reservation.Id, reservation.Status, command.CorrelationId);
            return CancelReservationOutcome.AlreadyTerminal;
        }

        // Instante único da transição terminal (EN-01/ADR-005): persistido no
        // mesmo commit e reusado pelo publisher, nunca recalculado.
        var terminalTransitionAt = DateTime.UtcNow;

        reservation.Cancel(command.Reason, terminalTransitionAt);
        await repository.UpdateAsync(reservation, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Reservation {ReservationId} cancelada (correlationId={CorrelationId})",
            reservation.Id, command.CorrelationId);

        try
        {
            await publisher.PublishAsync(reservation, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Evento booking.reservation_cancelled publicado para a Reservation {ReservationId}",
                reservation.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Falha best-effort ao publicar booking.reservation_cancelled da Reservation " +
                "{ReservationId}; estado terminal já persistido",
                reservation.Id);
        }

        return CancelReservationOutcome.Cancelled;
    }
}
