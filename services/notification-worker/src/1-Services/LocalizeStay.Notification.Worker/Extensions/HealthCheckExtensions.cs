using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalizeStay.Notification.Worker.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddHealthCheckConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        if (RabbitMqSettings.TryRead(configuration, out var settings) && settings is not null)
        {
            var healthSettings = settings;
            services.AddSingleton(new RabbitMqHealthConnection(healthSettings));
            builder.AddRabbitMQ(
                async serviceProvider => await serviceProvider
                    .GetRequiredService<RabbitMqHealthConnection>()
                    .GetConnectionAsync()
                    .ConfigureAwait(false),
                name: "rabbitmq",
                tags: ["ready"]);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapHealthCheckConfiguration(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        return endpoints;
    }
}
