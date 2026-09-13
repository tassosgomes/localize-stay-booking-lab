using LocalizeStay.Booking.Api.Contracts;
using LocalizeStay.Booking.Application;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.Booking.Api.Endpoints;

// POST /v1/reservations (operationId requestReservation do api-contract.yaml):
// Minimal API → IDispatcher.SendAsync → RequestReservationCommandHandler.
// GET /v1/reservations/{reservationId} (operationId getReservationById do
// api-contract.yaml de F02): Minimal API → IDispatcher.SendAsync →
// GetReservationByIdQueryHandler → ReservationDetailResponseDto.
// Erros de negócio/integração não são capturados aqui — o GlobalExceptionHandler
// traduz as exceções no status/code exatos do contrato.
public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/v1/reservations", HandleAsync)
            .Produces<ReservationResponseDto>(StatusCodes.Status201Created)
            .WithTags("Reservations");

        // reservationId chega como string bruta (sem constraint ":guid" da
        // rota): uma constraint faria o próprio ASP.NET Core devolver 404
        // (rota não encontrada) para um valor malformado, sobrepondo-se à
        // distinção 400×404 exigida pelo AC de RF-01.
        endpoints
            .MapGet("/v1/reservations/{reservationId}", HandleGetByIdAsync)
            .Produces<ReservationDetailResponseDto>(StatusCodes.Status200OK)
            .WithTags("Reservations");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        CreateReservationRequestDto request,
        IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var command = new RequestReservationCommand(
            request.AccommodationId,
            request.GuestReference,
            request.CheckIn,
            request.CheckOut,
            request.GuestsCount);

        var reservation = await dispatcher
            .SendAsync(command, cancellationToken)
            .ConfigureAwait(false);

        var response = ReservationResponseDto.From(reservation);

        return Results.Created($"/v1/reservations/{reservation.Id}", response);
    }

    private static async Task<IResult> HandleGetByIdAsync(
        string reservationId,
        IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var reservation = await dispatcher
            .SendAsync(new GetReservationByIdQuery(reservationId), cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(ReservationDetailResponseDto.From(reservation));
    }
}
