using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace LocalizeStay.Booking.IntegrationTests.Catalog;

public enum FakeCatalogBehavior
{
    Ok,
    NotFound,
    ServerError,
    Timeout
}

// Fake server mínimo dedicado a simular Catalog (in-memory, via TestServer do
// próprio Microsoft.AspNetCore.Mvc.Testing já referenciado — sem NuGet adicional).
public sealed class FakeCatalogServerFactory : IAsyncDisposable
{
    private const int TimeoutDelaySeconds = 10;

    private const string OkBody =
        """
        {"accommodationId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","active":true,"maxGuests":4,"availableForPeriod":true,"pricePerNight":"350.00","currency":"BRL"}
        """;

    private readonly IHost _host;
    private readonly HttpMessageHandler _handler;
    private readonly State _state;

    private FakeCatalogServerFactory(IHost host, HttpMessageHandler handler, State state)
    {
        _host = host;
        _handler = handler;
        _state = state;
    }

    public Uri BaseAddress => _host.GetTestServer().BaseAddress;

    public HttpMessageHandler Handler => _handler;

    public int RequestsReceived => Volatile.Read(ref _state.RequestsReceived);

    public string? LastRequestPathAndQuery => Volatile.Read(ref _state.LastPathAndQuery);

    public static async Task<FakeCatalogServerFactory> StartAsync(
        FakeCatalogBehavior behavior, string? okBody = null)
    {
        var state = new State();

        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.Run(async context =>
                    {
                        Interlocked.Increment(ref state.RequestsReceived);
                        Volatile.Write(
                            ref state.LastPathAndQuery,
                            $"{context.Request.Path}{context.Request.QueryString}");

                        switch (behavior)
                        {
                            case FakeCatalogBehavior.NotFound:
                                context.Response.StatusCode = StatusCodes.Status404NotFound;
                                context.Response.ContentType = "application/problem+json";
                                await context.Response.WriteAsync(
                                    """{"type":"about:blank","title":"Accommodation não encontrada","status":404}""");
                                break;

                            case FakeCatalogBehavior.ServerError:
                                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                context.Response.ContentType = "application/problem+json";
                                await context.Response.WriteAsync(
                                    """{"type":"about:blank","title":"Erro interno","status":500}""");
                                break;

                            case FakeCatalogBehavior.Timeout:
                                // Simula Catalog que nunca responde dentro do timeout do cliente.
                                try
                                {
                                    await Task.Delay(
                                        TimeSpan.FromSeconds(TimeoutDelaySeconds), context.RequestAborted);
                                }
                                catch (OperationCanceledException)
                                {
                                    // Cliente desistiu (timeout da pipeline): encerra sem responder.
                                }

                                break;

                            default:
                                context.Response.StatusCode = StatusCodes.Status200OK;
                                context.Response.ContentType = "application/json";
                                // okBody customizado permite fatos hipotéticos de 200
                                // (active=false, availableForPeriod=false etc.).
                                await context.Response.WriteAsync(okBody ?? OkBody);
                                break;
                        }
                    });
                });
            })
            .Build();

        await host.StartAsync();

        var server = host.GetTestServer();

        return new FakeCatalogServerFactory(host, server.CreateHandler(), state);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync().ConfigureAwait(false);
        _host.Dispose();
    }

    private sealed class State
    {
        public int RequestsReceived;
        public string? LastPathAndQuery;
    }
}
