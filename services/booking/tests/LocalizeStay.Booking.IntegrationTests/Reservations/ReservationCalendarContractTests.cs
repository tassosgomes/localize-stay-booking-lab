using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace LocalizeStay.Booking.IntegrationTests.Reservations;

[Collection("ReservationCalendarTests")]
public sealed class ReservationCalendarContractTests(ReservationCalendarFixture fixture)
{
    private readonly ReservationCalendarFixture _fixture = fixture;

    [Fact]
    public async Task ViewSchema_WhenPublished_MatchesTheSevenColumnContract()
    {
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        await using var columns = new NpgsqlCommand("""
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'integration' AND table_name = 'reservation_calendar_v1'
            ORDER BY ordinal_position;
            """, connection);
        await using var reader = await columns.ExecuteReaderAsync();
        var actual = new List<(string Name, string Type)>();
        while (await reader.ReadAsync()) actual.Add((reader.GetString(0), reader.GetString(1)));

        Assert.Equal(
            [("reservation_id", "uuid"), ("accommodation_id", "uuid"), ("check_in", "date"),
             ("check_out", "date"), ("status", "character varying"),
             ("created_at", "timestamp with time zone"), ("updated_at", "timestamp with time zone")],
            actual);
        await reader.DisposeAsync();

        await using var relkind = new NpgsqlCommand("""
            SELECT c.relkind
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'integration' AND c.relname = 'reservation_calendar_v1';
            """, connection);
        Assert.Equal('v', (char)(await relkind.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task View_WhenReservationIsSolicitada_DoesNotPublishIt()
    {
        var reservation = CreateReservation();
        await PersistAsync(reservation);

        Assert.Equal(0, await CountRowsAsync(reservation.Id));
    }

    [Fact]
    public async Task View_WhenReservationsAreTerminal_PreservesOneRowPerReservationAndPeriod()
    {
        var confirmed = CreateReservation(new DateOnly(2027, 1, 10), new DateOnly(2027, 1, 14));
        var cancelled = CreateReservation(new DateOnly(2027, 2, 2), new DateOnly(2027, 2, 5));
        confirmed.Confirm(new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc));
        cancelled.Cancel("teste", new DateTime(2026, 9, 13, 12, 1, 0, DateTimeKind.Utc));
        await PersistAsync(confirmed, cancelled);

        var rows = await ReadRowsAsync(confirmed.Id, cancelled.Id);
        Assert.Equal(2, rows.Count);
        AssertRow(rows.Single(row => row.Id == confirmed.Id), confirmed, "confirmada");
        AssertRow(rows.Single(row => row.Id == cancelled.Id), cancelled, "cancelada");
        Assert.All(rows, row => Assert.True(row.CheckOut > row.CheckIn));
    }

    [Fact]
    public async Task View_AfterTerminalCommit_IsVisibleFromANewConnectionWithPersistedUpdatedAt()
    {
        var reservation = CreateReservation();
        var transitionedAt = new DateTime(2026, 9, 13, 12, 2, 0, DateTimeKind.Utc);
        reservation.Confirm(transitionedAt);
        await PersistAsync(reservation);

        await using var context = _fixture.CreateDbContext();
        var persisted = await context.Reservations.AsNoTracking().SingleAsync(item => item.Id == reservation.Id);
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();
        var row = (await ReadRowsAsync(connection, reservation.Id)).Single();

        Assert.Equal(persisted.TerminalTransitionAt, row.UpdatedAt);
        Assert.Equal(transitionedAt, row.UpdatedAt);
    }

    [Fact]
    public async Task View_WhenLateTerminalTransitionIsRejected_RemainsUniqueAndUnchanged()
    {
        var reservation = CreateReservation();
        var original = new DateTime(2026, 9, 13, 12, 3, 0, DateTimeKind.Utc);
        reservation.Confirm(original);
        await PersistAsync(reservation);

        Assert.Throws<InvalidOperationException>(() => reservation.Cancel("tardia", original.AddMinutes(1)));
        var rows = await ReadRowsAsync(reservation.Id);
        Assert.Single(rows);
        Assert.Equal(original, rows[0].UpdatedAt);
    }

    [Fact]
    public async Task View_WhenInspected_DoesNotExposeForbiddenInternalColumns()
    {
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'integration' AND table_name = 'reservation_calendar_v1';
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));

        Assert.DoesNotContain(names, name => new[] { "guest_reference", "guests_count", "price_per_night", "total_amount", "currency", "correlation_id", "causation_id", "cancellation_reason" }.Contains(name));
    }

    [Fact]
    public async Task CalendarDdl_WhenAppliedTwice_ConvergesWithoutDuplicateRelation()
    {
        await _fixture.ExecuteCalendarDdlAsync();
        await _fixture.ExecuteCalendarDdlAsync();

        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'integration' AND c.relname = 'reservation_calendar_v1';
            """, connection);
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    private static Reservation CreateReservation(DateOnly? checkIn = null, DateOnly? checkOut = null) => Reservation.Create(
        Guid.NewGuid(), "guest-calendar", checkIn ?? new DateOnly(2027, 3, 1), checkOut ?? new DateOnly(2027, 3, 4), 2,
        new AvailabilityFacts(true, 4, true, 275.50m, "BRL"));

    private async Task PersistAsync(params Reservation[] reservations)
    {
        await using var context = _fixture.CreateDbContext();
        context.Reservations.AddRange(reservations);
        await context.SaveChangesAsync();
    }

    private static void AssertRow(CalendarRow row, Reservation reservation, string status)
    {
        Assert.Equal(reservation.Id, row.Id);
        Assert.Equal(reservation.AccommodationId, row.AccommodationId);
        Assert.Equal(reservation.CheckIn, row.CheckIn);
        Assert.Equal(reservation.CheckOut, row.CheckOut);
        Assert.Equal(status, row.Status);
        Assert.Equal(reservation.TerminalTransitionAt, row.UpdatedAt);
    }

    private async Task<int> CountRowsAsync(Guid reservationId)
    {
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();
        return (await ReadRowsAsync(connection, reservationId)).Count;
    }

    private async Task<List<CalendarRow>> ReadRowsAsync(params Guid[] reservationIds)
    {
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();
        return await ReadRowsAsync(connection, reservationIds);
    }

    private static async Task<List<CalendarRow>> ReadRowsAsync(NpgsqlConnection connection, params Guid[] reservationIds)
    {
        await using var command = new NpgsqlCommand("""
            SELECT reservation_id, accommodation_id, check_in, check_out, status, updated_at
            FROM integration.reservation_calendar_v1
            WHERE reservation_id = ANY(@ids)
            ORDER BY reservation_id;
            """, connection);
        command.Parameters.AddWithValue("ids", reservationIds);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<CalendarRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new CalendarRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetFieldValue<DateOnly>(3), reader.GetString(4), reader.GetDateTime(5)));
        }
        return rows;
    }

    private sealed record CalendarRow(Guid Id, Guid AccommodationId, DateOnly CheckIn, DateOnly CheckOut, string Status, DateTime UpdatedAt);
}
