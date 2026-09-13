using FluentValidation;

namespace LocalizeStay.Booking.Application.Reservations;

// Valida apenas o SHAPE do identificador → 400 VALIDATION_ERROR. Não saber se
// o id existe é papel do handler (404), não desta validação.
public sealed class GetReservationByIdQueryValidator : AbstractValidator<GetReservationByIdQuery>
{
    public GetReservationByIdQueryValidator()
    {
        RuleFor(query => query.ReservationId)
            .NotEmpty()
            .WithMessage("O campo 'reservationId' é obrigatório.")
            .Must(reservationId => Guid.TryParse(reservationId, out _))
            .WithMessage("O identificador informado não está em um formato válido.");
    }
}
