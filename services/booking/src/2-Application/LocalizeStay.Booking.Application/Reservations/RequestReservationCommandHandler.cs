using FluentValidation;
using LocalizeStay.Booking.Domain.Reservations;
using LocalizeStay.Booking.Domain.Reservations.Exceptions;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Booking.Application.Reservations;

// Orquestra RF-01 de ponta a ponta. A ordem de execução é NORMATIVA
// (techspec): validações locais de período/hóspedes rodam ANTES da chamada a
// Catalog — a AC de RF-01 exige rejeição 422 sem nenhuma chamada de negócio a
// Catalog nesses cenários.
public sealed class RequestReservationCommandHandler(
    IValidator<RequestReservationCommand> validator,
    ICatalogAvailabilityClient catalogAvailabilityClient,
    IReservationRepository reservationRepository,
    IReservationRequestedPublisher publisher,
    IPaymentRequestedPublisher paymentRequestedPublisher,
    ILogger<RequestReservationCommandHandler> logger) : ICommandHandler<RequestReservationCommand, Reservation>
{
    public async Task<Reservation> HandleAsync(
        RequestReservationCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Solicitação de reserva recebida para a acomodação {AccommodationId} " +
            "(guestReference={GuestReference}, checkIn={CheckIn}, checkOut={CheckOut}, guestsCount={GuestsCount})",
            command.AccommodationId,
            command.GuestReference,
            command.CheckIn,
            command.CheckOut,
            command.GuestsCount);

        Reservation.EnsurePeriodIsValid(command.CheckIn, command.CheckOut);
        Reservation.EnsureGuestsCountIsValid(command.GuestsCount);

        var facts = await catalogAvailabilityClient
            .CheckAvailabilityAsync(
                command.AccommodationId, command.CheckIn, command.CheckOut, command.GuestsCount, cancellationToken)
            .ConfigureAwait(false);

        if (facts is null)
        {
            logger.LogWarning(
                "Catalog não encontrou a acomodação {AccommodationId}; traduzindo 404 em rejeição de negócio",
                command.AccommodationId);
            throw new AcomodacaoIndisponivelException();
        }

        logger.LogInformation(
            "Catalog devolveu fatos para a acomodação {AccommodationId} " +
            "(active={Active}, maxGuests={MaxGuests}, availableForPeriod={AvailableForPeriod})",
            command.AccommodationId,
            facts.Active,
            facts.MaxGuests,
            facts.AvailableForPeriod);

        var reservation = Reservation.Create(
            command.AccommodationId,
            command.GuestReference,
            command.CheckIn,
            command.CheckOut,
            command.GuestsCount,
            facts);

        await reservationRepository.AddAsync(reservation, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Reservation {ReservationId} criada com status {Status} e total {TotalAmount} {Currency} " +
            "(correlationId={CorrelationId})",
            reservation.Id,
            reservation.Status,
            reservation.TotalAmount,
            reservation.Currency,
            reservation.Id);

        try
        {
            await publisher.PublishAsync(reservation, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Evento booking.reservation_requested publicado para a Reservation {ReservationId} " +
                "(correlationId={CorrelationId})",
                reservation.Id,
                reservation.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Publish best-effort, sem outbox nesta fase (risco conhecido da
            // TechSpec): a falha não desfaz a Reservation já persistida.
            logger.LogError(
                ex,
                "Falha best-effort ao publicar booking.reservation_requested da Reservation {ReservationId}; " +
                "a Reservation permanece criada (correlationId={CorrelationId})",
                reservation.Id,
                reservation.Id);
        }

        try
        {
            await paymentRequestedPublisher.PublishAsync(reservation, cancellationToken).ConfigureAwait(false);

            reservation.Saga.MarkPaymentRequestSent(DateTime.UtcNow);
            await reservationRepository.UpdateAsync(reservation, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Evento booking.payment_requested publicado para a Reservation {ReservationId} " +
                "(correlationId={CorrelationId})", reservation.Id, reservation.Saga.CorrelationId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Falha best-effort ao publicar booking.payment_requested da Reservation {ReservationId} " +
                "(correlationId={CorrelationId}); ReservationSaga permanece sem registro de solicitação enviada",
                reservation.Id, reservation.Saga.CorrelationId);
        }

        return reservation;
    }
}
