using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using LocalizeStay.Booking.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LocalizeStay.Booking.IntegrationTests.Reservations;

[Collection("BookingIntegrationTests")]
public sealed class ReservationPersistenceTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static Reservation CreateValidReservation()
    {
        var facts = new AvailabilityFacts(
            Active: true,
            MaxGuests: 4,
            AvailableForPeriod: true,
            PricePerNight: 275.50m,
            Currency: "BRL");

        return Reservation.Create(
            Guid.NewGuid(),
            "guest-integration",
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 4),
            3,
            facts);
    }

    [Fact]
    public async Task AddAsync_persists_reservation_and_saga_and_read_back_matches()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var reservation = CreateValidReservation();

        // Act
        await repository.AddAsync(reservation, CancellationToken.None);

        // Assert: leitura de volta por um DbContext limpo, sem tracking do Add.
        using var readScope = _factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var stored = await readContext.Reservations
            .AsNoTracking()
            .Include(r => r.Saga)
            .SingleAsync(r => r.Id == reservation.Id);

        Assert.Equal(ReservationStatus.Solicitada, stored.Status);
        Assert.Equal(reservation.AccommodationId, stored.AccommodationId);
        Assert.Equal("guest-integration", stored.GuestReference);
        Assert.Equal(new DateOnly(2026, 11, 1), stored.CheckIn);
        Assert.Equal(new DateOnly(2026, 11, 4), stored.CheckOut);
        Assert.Equal(3, stored.GuestsCount);
        Assert.Equal(275.50m, stored.PricePerNight);
        Assert.Equal(826.50m, stored.TotalAmount);
        Assert.Equal("BRL", stored.Currency);

        Assert.NotNull(stored.Saga);
        Assert.Equal(SagaState.PaymentPending, stored.Saga.State);
        Assert.Equal(stored.Id, stored.Saga.ReservationId);
        Assert.Equal(stored.Id, stored.Saga.CorrelationId);
    }

    [Fact]
    public async Task AddReservationAndSaga_migration_stores_exact_column_values_and_drops_sentinel()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var reservation = CreateValidReservation();

        // Act
        await repository.AddAsync(reservation, CancellationToken.None);

        // Assert: representação exata gravada nas colunas do schema booking.
        var storedStatus = await dbContext.Database.SqlQueryRaw<string>(
            "SELECT status AS \"Value\" FROM booking.reservations WHERE id = {0}", reservation.Id).SingleAsync();
        Assert.Equal("solicitada", storedStatus);

        var storedState = await dbContext.Database.SqlQueryRaw<string>(
            "SELECT state AS \"Value\" FROM booking.reservation_sagas WHERE reservation_id = {0}", reservation.Id).SingleAsync();
        Assert.Equal("PaymentPending", storedState);

        var correlationMatchesReservation = await dbContext.Database.SqlQueryRaw<bool>(
            "SELECT (correlation_id = reservation_id) AS \"Value\" FROM booking.reservation_sagas WHERE reservation_id = {0}", reservation.Id).SingleAsync();
        Assert.True(correlationMatchesReservation);

        var sentinelTables = await dbContext.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM information_schema.tables WHERE table_schema = 'booking' AND table_name = '__bootstrap_check'").SingleAsync();
        Assert.Equal(0, sentinelTables);

        var reservationTables = await dbContext.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM information_schema.tables WHERE table_schema = 'booking' AND table_name IN ('reservations', 'reservation_sagas')").SingleAsync();
        Assert.Equal(2, reservationTables);
    }
}
