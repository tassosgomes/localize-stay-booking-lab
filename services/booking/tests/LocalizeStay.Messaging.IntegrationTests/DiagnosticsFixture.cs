using System.Net.Http.Headers;
using System.Text;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace LocalizeStay.Messaging.IntegrationTests;

/// <summary>
/// Dono do ciclo de vida dos containers do fluxo de diagnóstico (V-03):
/// RabbitMQ efêmero (mesma imagem do broker do homelab) com o vhost
/// <c>localize-stay</c> criado via API de management, + Postgres efêmero
/// para o boot do Booking.
/// </summary>
public sealed class DiagnosticsFixture : IAsyncLifetime
{
    private const string UserName = "guest";
    private const string Password = "guest";
    private const string Vhost = "localize-stay";

    private readonly RabbitMqContainer _rabbitContainer = new RabbitMqBuilder("rabbitmq:4.3.5-management-alpine")
        .WithUsername(UserName)
        .WithPassword(Password)
        .WithPortBinding(15672, true)
        .Build();

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("localize_stay")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public CaptureLoggerProvider Capture { get; } = new();

    public string AmqpHost => _rabbitContainer.Hostname;

    public int AmqpPort => _rabbitContainer.GetMappedPublicPort(5672);

    public string BookingConnectionString => _dbContainer.GetConnectionString();

    public Dictionary<string, string?> RabbitMqOverrides() => new()
    {
        ["RabbitMQ:HostName"] = AmqpHost,
        ["RabbitMQ:Port"] = AmqpPort.ToString(),
        ["RabbitMQ:UserName"] = UserName,
        ["RabbitMQ:Password"] = Password,
        ["RabbitMQ:VirtualHost"] = Vhost
    };

    public async Task InitializeAsync()
    {
        await _rabbitContainer.StartAsync();
        await CreateVhostAsync();
        await _dbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await _rabbitContainer.StopAsync();
    }

    private async Task CreateVhostAsync()
    {
        using var management = CreateManagementClient();

        using var vhostResponse = await management.PutAsync(
            "api/vhosts/localize-stay",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        vhostResponse.EnsureSuccessStatusCode();

        using var permissionsResponse = await management.PutAsync(
            $"api/permissions/localize-stay/{UserName}",
            new StringContent(
                """{"configure":".*","write":".*","read":".*"}""",
                Encoding.UTF8,
                "application/json"));
        permissionsResponse.EnsureSuccessStatusCode();
    }

    private HttpClient CreateManagementClient()
    {
        var management = new HttpClient
        {
            BaseAddress = new Uri($"http://{AmqpHost}:{_rabbitContainer.GetMappedPublicPort(15672)}/")
        };

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{UserName}:{Password}"));
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return management;
    }
}
