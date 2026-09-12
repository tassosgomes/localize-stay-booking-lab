using LocalizeStay.Booking.Infra.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Messaging.IntegrationTests;

/// <summary>
/// Booking real (endpoint <c>POST /internal/diagnostics/ping</c> + publisher
/// <c>Rmq.CloudEvents</c>) apontado para os containers da fixture.
/// Segue o padrão da <c>CustomWebApplicationFactory</c> do Booking: o boot do host
/// lê de variáveis de ambiente, que aqui recebem os valores dos containers.
/// </summary>
public sealed class BookingDiagnosticsFactory : WebApplicationFactory<Program>
{
    private readonly DiagnosticsFixture _fixture;
    private bool _migrated;

    public BookingDiagnosticsFactory(DiagnosticsFixture fixture)
    {
        _fixture = fixture;

        Environment.SetEnvironmentVariable("ConnectionStrings__Booking", fixture.BookingConnectionString);
        Environment.SetEnvironmentVariable("RabbitMQ__HostName", fixture.AmqpHost);
        Environment.SetEnvironmentVariable("RabbitMQ__Port", fixture.AmqpPort.ToString());
        Environment.SetEnvironmentVariable("RabbitMQ__UserName", "guest");
        Environment.SetEnvironmentVariable("RabbitMQ__Password", "guest");
        Environment.SetEnvironmentVariable("RabbitMQ__VirtualHost", "/localize-stay");
    }

    public async Task EnsureDatabaseMigratedAsync()
    {
        if (_migrated)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await dbContext.Database.MigrateAsync();
        _migrated = true;
    }
}
