using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace LocalizeStay.Payment.Api.Extensions;

public static class MiddlewarePipelineExtensions
{
    public static WebApplication UseApplicationPipeline(this WebApplication app, IWebHostEnvironment environment)
    {
        _ = environment;

        // Sem UseHttpsRedirection nesta fase: o laboratório local não tem TLS;
        // a decisão volta no primeiro PRD que exigir transporte seguro.
        app.UseSwaggerConfiguration();
        app.UseCorsConfiguration();
        app.MapHealthCheckConfiguration();

        return app;
    }
}
