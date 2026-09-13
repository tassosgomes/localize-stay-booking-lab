using FluentValidation;
using LocalizeStay.Booking.Domain.Reservations;
using LocalizeStay.Booking.Domain.Reservations.Exceptions;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Booking.Application.Reservations;

// Orquestra a consulta de reserva (RF-01 de F02), operação 100% somente
// leitura. Retorna a Reservation de domínio diretamente (com .Saga carregado)
// sem nenhuma transformação — a tradução para o schema do contrato acontece
// na camada de API (mesmo padrão de RequestReservationCommandHandler /
// ReservationResponseDto em F01).
public sealed class GetReservationByIdQueryHandler(
    IValidator<GetReservationByIdQuery> validator,
    IReservationRepository reservationRepository,
    ILogger<GetReservationByIdQueryHandler> logger) : IQueryHandler<GetReservationByIdQuery, Reservation>
{
    public async Task<Reservation> HandleAsync(
        GetReservationByIdQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken).ConfigureAwait(false);

        var id = Guid.Parse(query.ReservationId);

        logger.LogInformation(
            "Consulta de reserva recebida (reservationId={ReservationId})",
            query.ReservationId);

        var reservation = await reservationRepository
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (reservation is null)
        {
            logger.LogWarning(
                "Reservation {ReservationId} não encontrada",
                query.ReservationId);
            throw new ReservationNotFoundException();
        }

        logger.LogInformation(
            "Reservation {ReservationId} encontrada com status {Status} e saga {SagaState}",
            reservation.Id,
            reservation.Status,
            reservation.Saga.State);

        return reservation;
    }
}
