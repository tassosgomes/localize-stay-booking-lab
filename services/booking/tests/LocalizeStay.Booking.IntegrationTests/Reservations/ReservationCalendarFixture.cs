using LocalizeStay.Booking.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace LocalizeStay.Booking.IntegrationTests.Reservations;

public sealed class ReservationCalendarFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("localize_stay")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _database.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        await using (var context = CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = """
                CREATE SCHEMA IF NOT EXISTS integration;
                DO $$
                BEGIN
                    CREATE ROLE catalog_role NOLOGIN;
                EXCEPTION WHEN duplicate_object THEN NULL;
                END $$;
                DO $$
                BEGIN
                    CREATE ROLE booking_role NOLOGIN;
                EXCEPTION WHEN duplicate_object THEN NULL;
                END $$;
                DO $$
                BEGIN
                    CREATE ROLE payment_role NOLOGIN;
                EXCEPTION WHEN duplicate_object THEN NULL;
                END $$;
                """;
            await setup.ExecuteNonQueryAsync();
        }

        await ExecuteCalendarDdlAsync(connection);
    }

    public BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "booking"))
            .Options;
        return new BookingDbContext(options);
    }

    public NpgsqlConnection CreateConnection() => new(ConnectionString);

    public async Task ExecuteCalendarDdlAsync(NpgsqlConnection? connection = null)
    {
        var ownsConnection = connection is null;
        connection ??= CreateConnection();
        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = await File.ReadAllTextAsync(FindDdlPath());
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (ownsConnection)
            {
                await connection.DisposeAsync();
            }
        }
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static string FindDdlPath()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "db", "integration", "001-reservation-calendar-v1.sql");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("Não foi possível localizar o DDL reservation_calendar_v1.");
    }
}
