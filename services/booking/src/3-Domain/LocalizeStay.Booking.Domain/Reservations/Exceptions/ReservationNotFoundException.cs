using LocalizeStay.Booking.Domain.Exceptions;

namespace LocalizeStay.Booking.Domain.Reservations.Exceptions;

public sealed class ReservationNotFoundException : DomainException
{
    public ReservationNotFoundException()
        : base("Reservation não encontrada: nenhuma Reservation existe com o identificador informado.")
    {
    }
}
