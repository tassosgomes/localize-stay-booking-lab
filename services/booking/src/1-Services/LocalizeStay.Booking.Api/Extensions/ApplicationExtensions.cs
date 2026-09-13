using FluentValidation;
using LocalizeStay.Booking.Application;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Booking.Api.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<ICommandHandler<RequestReservationCommand, Reservation>, RequestReservationCommandHandler>();
        services.AddScoped<IValidator<RequestReservationCommand>, RequestReservationCommandValidator>();
        services.AddScoped<IQueryHandler<GetReservationByIdQuery, Reservation>, GetReservationByIdQueryHandler>();
        services.AddScoped<IValidator<GetReservationByIdQuery>, GetReservationByIdQueryValidator>();

        return services;
    }
}
