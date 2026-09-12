using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Booking.Api.Extensions;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistenceConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Booking");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Booking não configurada. Defina via 'dotnet user-secrets set \"ConnectionStrings:Booking\" \"<connection-string>\"'.");
        }

        services.AddDbContext<BookingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "booking")));

        services.AddScoped<IReservationRepository, ReservationRepository>();

        return services;
    }
}
