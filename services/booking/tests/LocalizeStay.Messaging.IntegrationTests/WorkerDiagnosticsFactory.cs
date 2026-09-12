using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkerProgram = LocalizeStay.Notification.Worker.Program;

namespace LocalizeStay.Messaging.IntegrationTests;

/// <summary>
/// Notification Worker real (<c>DiagnosticPingConsumer</c> + <c>/health/ready</c>)
/// apontado para os containers da fixture, com log capturado em memória.
/// Boot via variáveis de ambiente, no padrão das demais factories do repo.
/// </summary>
public sealed class WorkerDiagnosticsFactory : WebApplicationFactory<WorkerProgram>
{
    private readonly DiagnosticsFixture _fixture;

    public WorkerDiagnosticsFactory(DiagnosticsFixture fixture)
    {
        _fixture = fixture;

        Environment.SetEnvironmentVariable("RabbitMQ__HostName", fixture.AmqpHost);
        Environment.SetEnvironmentVariable("RabbitMQ__Port", fixture.AmqpPort.ToString());
        Environment.SetEnvironmentVariable("RabbitMQ__UserName", "guest");
        Environment.SetEnvironmentVariable("RabbitMQ__Password", "guest");
        Environment.SetEnvironmentVariable("RabbitMQ__VirtualHost", "localize-stay");
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(_fixture.Capture);
        });
    }
}
