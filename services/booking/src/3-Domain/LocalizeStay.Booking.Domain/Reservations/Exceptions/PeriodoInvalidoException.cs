using LocalizeStay.Booking.Domain.Exceptions;

namespace LocalizeStay.Booking.Domain.Reservations.Exceptions;

public sealed class PeriodoInvalidoException : DomainException
{
    public PeriodoInvalidoException()
        : base("Período inválido: a data de check-out deve ser posterior à data de check-in.")
    {
    }
}
