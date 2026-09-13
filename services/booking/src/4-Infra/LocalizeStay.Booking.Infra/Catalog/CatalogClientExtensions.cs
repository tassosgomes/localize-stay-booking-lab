using LocalizeStay.Booking.Application.Reservations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;

namespace LocalizeStay.Booking.Infra.Catalog;

public static class CatalogClientExtensions
{
    public static IServiceCollection AddCatalogAvailabilityClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CatalogClientOptions>(
            configuration.GetSection(CatalogClientOptions.SectionName));

        services.AddHttpClient<ICatalogAvailabilityClient, CatalogAvailabilityHttpClient>(
                (serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<CatalogClientOptions>>().Value;
                    client.BaseAddress = BuildBaseAddress(options.BaseUrl);
                })
            .AddResilienceHandler("catalog-availability", (pipelineBuilder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<CatalogClientOptions>>().Value;

                if (options.TimeoutSeconds <= 0)
                {
                    throw new InvalidOperationException(
                        "CatalogClient:TimeoutSeconds deve ser maior que zero.");
                }

                // Única estratégia do pipeline: timeout explícito, SEM retry —
                // retry/idempotência de rede pertencem a F06, não a esta feature.
                pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds));
            });

        return services;
    }

    private static Uri BuildBaseAddress(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        return new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
    }
}
