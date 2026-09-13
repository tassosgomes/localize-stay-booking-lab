using LocalizeStay.Booking.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace LocalizeStay.Booking.Api.Extensions;

public static class MiddlewarePipelineExtensions
{
    public static WebApplication UseApplicationPipeline(this WebApplication app, IWebHostEnvironment environment)
    {
        _ = environment;

        // Sem UseHttpsRedirection nesta fase: o laboratório local não tem TLS;
        // a decisão volta no primeiro PRD que exigir transporte seguro.
        app.UseExceptionHandler();
        app.UseSwaggerConfiguration();
        app.UseCorsConfiguration();
        app.MapHealthCheckConfiguration();
        app.MapReservationEndpoints();
        app.MapDiagnosticsEndpoints();

        return app;
    }
}
