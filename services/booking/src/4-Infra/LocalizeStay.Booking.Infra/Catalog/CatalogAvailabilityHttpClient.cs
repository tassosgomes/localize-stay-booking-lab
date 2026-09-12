using System.Globalization;
using System.Net;
using System.Text.Json;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using Polly.Timeout;

namespace LocalizeStay.Booking.Infra.Catalog;

public sealed class CatalogAvailabilityHttpClient(HttpClient httpClient) : ICatalogAvailabilityClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient = httpClient;

    public async Task<AvailabilityFacts?> CheckAvailabilityAsync(
        Guid accommodationId, DateOnly checkIn, DateOnly checkOut, int guestsCount,
        CancellationToken cancellationToken)
    {
        var requestUri =
            $"accommodations/{accommodationId}/availability-check" +
            $"?checkIn={checkIn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" +
            $"&checkOut={checkOut.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" +
            $"&guestsCount={guestsCount}";

        AvailabilityCheckResponse payload;
        try
        {
            using var response = await _httpClient
                .GetAsync(requestUri, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new CatalogUnavailableException(
                    $"Catalog respondeu status HTTP inesperado {(int)response.StatusCode} ao consultar " +
                    $"a acomodação {accommodationId}.");
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            payload = await JsonSerializer
                .DeserializeAsync<AvailabilityCheckResponse>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new CatalogUnavailableException(
                    $"Catalog respondeu 200 com corpo vazio para a acomodação {accommodationId}.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CatalogUnavailableException(FailureMessage(accommodationId), ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException or JsonException)
        {
            throw new CatalogUnavailableException(FailureMessage(accommodationId), ex);
        }

        if (!decimal.TryParse(
                payload.PricePerNight, NumberStyles.Number, CultureInfo.InvariantCulture, out var pricePerNight))
        {
            throw new CatalogUnavailableException(
                $"Catalog respondeu pricePerNight inválido ('{payload.PricePerNight}') para a acomodação " +
                $"{accommodationId}.");
        }

        return new AvailabilityFacts(
            payload.Active,
            payload.MaxGuests,
            payload.AvailableForPeriod,
            pricePerNight,
            payload.Currency);
    }

    private static string FailureMessage(Guid accommodationId) =>
        $"Falha ao consultar disponibilidade da acomodação {accommodationId} em Catalog.";

    private sealed class AvailabilityCheckResponse
    {
        public required Guid AccommodationId { get; init; }

        public required bool Active { get; init; }

        public required int MaxGuests { get; init; }

        public required bool AvailableForPeriod { get; init; }

        public required string PricePerNight { get; init; } = null!;

        public required string Currency { get; init; } = null!;
    }
}
