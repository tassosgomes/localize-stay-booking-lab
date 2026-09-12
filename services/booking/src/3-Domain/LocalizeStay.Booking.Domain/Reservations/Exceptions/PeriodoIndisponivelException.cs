using LocalizeStay.Booking.Domain.Exceptions;

namespace LocalizeStay.Booking.Domain.Reservations.Exceptions;

public sealed class PeriodoIndisponivelException : DomainException
{
    public PeriodoIndisponivelException()
        : base("Período indisponível: a acomodação não está disponível no período solicitado.")
    {
    }
}
