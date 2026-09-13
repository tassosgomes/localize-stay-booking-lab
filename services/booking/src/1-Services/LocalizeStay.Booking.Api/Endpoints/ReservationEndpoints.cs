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
}
