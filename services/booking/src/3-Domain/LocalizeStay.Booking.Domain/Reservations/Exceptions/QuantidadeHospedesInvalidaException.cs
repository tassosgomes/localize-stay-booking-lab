using LocalizeStay.Booking.Domain.Exceptions;

namespace LocalizeStay.Booking.Domain.Reservations.Exceptions;

public sealed class QuantidadeHospedesInvalidaException : DomainException
{
    public QuantidadeHospedesInvalidaException()
        : base("Quantidade de hóspedes inválida: deve ser maior que zero.")
    {
    }
}
