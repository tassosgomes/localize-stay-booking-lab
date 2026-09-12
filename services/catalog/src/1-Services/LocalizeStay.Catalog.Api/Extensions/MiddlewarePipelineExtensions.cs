using LocalizeStay.Catalog.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace LocalizeStay.Catalog.Api.Extensions;

public static class MiddlewarePipelineExtensions
{
    public static WebApplication UseApplicationPipeline(this WebApplication app, IWebHostEnvironment environment)
    {
        _ = environment;

        // Sem UseHttpsRedirection nesta fase: o laboratório local não tem TLS;
        // a decisão volta no primeiro PRD que exigir transporte seguro.
        app.UseErrorHandling();
        app.UseSwaggerConfiguration();
        app.UseCorsConfiguration();
        app.MapHealthCheckConfiguration();
        app.MapControllers();

        // STUB DE TESTE/E2E da task 6.0 (prd-solicitacao-reserva): remover
        // quando Catalog F04 implementar o endpoint oficial.
        app.MapE2EAvailabilityStub();

        return app;
    }
}
