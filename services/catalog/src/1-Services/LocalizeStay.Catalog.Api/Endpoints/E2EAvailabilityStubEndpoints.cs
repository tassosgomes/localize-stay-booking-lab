// STUB DE TESTE/E2E (task 6.0, prd-solicitacao-reserva) — NÃO é o Catalog real.
//
// O Catalog real é skeleton (sem endpoints de negócio) e a feature Catalog F04
// (Consulta de Disponibilidade) ainda não tem PRD. Este stub mínimo existe
// somente para o E2E full-stack da task 6.0 (browser → Booking real → Catalog):
// responde o contrato provisório
// `GET /v1/accommodations/{id}/availability-check`
// (tasks/prd-solicitacao-reserva/api-contract.yaml, tag
// "Catalog Availability (dependency)") com fatos fixos para UMA accommodation
// fixture e 404 para qualquer outro id. Sem persistência, sem regra de negócio.
//
// Quando Catalog F04 for implementado, este arquivo deve ser REMOVIDO e
// substituído pelo endpoint oficial.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Catalog.Api.Endpoints;

public static class E2EAvailabilityStubEndpoints
{
    // Fixture do E2E 6.0 (mesmo UUID do exemplo do api-contract.yaml).
    public static readonly Guid FixtureAccommodationId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private const string FixturePricePerNight = "350.00";
    private const string FixtureCurrency = "BRL";
    private const int FixtureMaxGuests = 4;

    public static IEndpointRouteBuilder MapE2EAvailabilityStub(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/v1/accommodations/{accommodationId:guid}/availability-check", HandleAsync)
            .WithTags("Catalog Availability (dependency)");

        return endpoints;
    }

    private static IResult HandleAsync(
        Guid accommodationId,
        string? checkIn,
        string? checkOut,
        string? guestsCount)
    {
        _ = checkIn;
        _ = checkOut;
        _ = guestsCount;

        if (accommodationId != FixtureAccommodationId)
        {
            return Results.Json(
                new
                {
                    type = "https://localize-stay.lab/problems/accommodation-not-found",
                    title = "Accommodation não encontrada",
                    status = 404,
                    detail = "Nenhuma Accommodation existe com o identificador informado.",
                    instance = $"/v1/accommodations/{accommodationId}/availability-check",
                    code = "ACCOMMODATION_NOT_FOUND",
                },
                contentType: "application/problem+json",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(new
        {
            accommodationId,
            active = true,
            maxGuests = FixtureMaxGuests,
            availableForPeriod = true,
            pricePerNight = FixturePricePerNight,
            currency = FixtureCurrency,
        });
    }
}
