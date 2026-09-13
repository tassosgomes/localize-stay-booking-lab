using LocalizeStay.Booking.Domain.Reservations;

namespace LocalizeStay.Booking.Application.Reservations;

public interface ICatalogAvailabilityClient
{
    // Retorna null quando Catalog responde 404 (accommodation não encontrada).
    // Lança CatalogUnavailableException para timeout, erro de rede, 5xx ou payload inesperado.
    Task<AvailabilityFacts?> CheckAvailabilityAsync(
        Guid accommodationId, DateOnly checkIn, DateOnly checkOut, int guestsCount,
        CancellationToken cancellationToken);
}
