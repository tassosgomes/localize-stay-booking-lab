using FluentValidation;

namespace LocalizeStay.Booking.Application.Reservations;

// Valida apenas o SHAPE do request (campos obrigatórios/formato) → 400
// VALIDATION_ERROR. Rejeições de negócio (período/hóspedes/capacidade) ficam
// no Domain para responder 422 com o code exato do api-contract.yaml.
public sealed class RequestReservationCommandValidator : AbstractValidator<RequestReservationCommand>
{
    public RequestReservationCommandValidator()
    {
        RuleFor(command => command.AccommodationId)
            .NotEmpty()
            .WithMessage("O campo 'accommodationId' é obrigatório.");

        RuleFor(command => command.GuestReference)
            .NotEmpty()
            .WithMessage("O campo 'guestReference' é obrigatório.")
            .MaximumLength(255)
            .WithMessage("O campo 'guestReference' deve ter no máximo 255 caracteres.");
    }
}
