using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Booking.Api.Extensions;

public static class CorsExtensions
{
    public const string DefaultPolicy = "Default";

    // Origem do Vite dev server do frontend de teste (task 7.0 / V-04, ADR-003).
    public const string FrontendDevOrigin = "http://localhost:5173";

    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var origins = allowedOrigins.Contains(FrontendDevOrigin) ? allowedOrigins : [.. allowedOrigins, FrontendDevOrigin];

        services.AddCors(options =>
        {
            options.AddPolicy(DefaultPolicy, policy =>
            {
                policy.WithOrigins(origins);

                policy.AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }

    public static IApplicationBuilder UseCorsConfiguration(this IApplicationBuilder app)
    {
        return app.UseCors(DefaultPolicy);
    }
}
