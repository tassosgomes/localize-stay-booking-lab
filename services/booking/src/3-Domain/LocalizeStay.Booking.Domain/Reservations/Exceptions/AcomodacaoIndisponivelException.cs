using LocalizeStay.Booking.Domain.Exceptions;

namespace LocalizeStay.Booking.Domain.Reservations.Exceptions;

public sealed class AcomodacaoIndisponivelException : DomainException
{
    public AcomodacaoIndisponivelException()
        : base("Acomodação indisponível: a acomodação está inativa ou não foi encontrada.")
    {
    }
}
