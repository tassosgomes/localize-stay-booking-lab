using LocalizeStay.Notification.Worker.Extensions;
using LocalizeStay.Notification.Worker.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LocalizeStay.Notification.Worker;

// Program clássico (com namespace) em vez de top-level statements: evita colisão
// do tipo global `Program` com os serviços HTTP quando os testes referenciam
// Booking e Worker no mesmo projeto (WebApplicationFactory de cada um).
public sealed class Program
{
    public static async Task Main(string[] args)
    {
        // Host HTTP leve: existe só para /health/live e /health/ready (probes).
        // Não é API pública — o trabalho real roda no DiagnosticPingConsumer.
        var builder = WebApplication.CreateBuilder(args);

        builder.Services
            .AddMessagingConfiguration(builder.Configuration)
            .AddHealthCheckConfiguration(builder.Configuration)
            .AddHostedService<DiagnosticPingConsumer>();

        var app = builder.Build();

        app.MapHealthCheckConfiguration();

        await app.RunAsync();
    }
}
