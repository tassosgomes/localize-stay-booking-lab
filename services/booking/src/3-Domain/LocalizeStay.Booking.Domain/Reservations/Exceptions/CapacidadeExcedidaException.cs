using LocalizeStay.Booking.Domain.Exceptions;

namespace LocalizeStay.Booking.Domain.Reservations.Exceptions;

public sealed class CapacidadeExcedidaException : DomainException
{
    public CapacidadeExcedidaException()
        : base("Capacidade excedida: o número de hóspedes é maior que a capacidade da acomodação.")
    {
    }
}
