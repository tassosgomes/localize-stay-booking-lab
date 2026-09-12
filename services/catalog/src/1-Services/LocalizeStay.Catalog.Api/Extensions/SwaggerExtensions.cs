using LocalizeStay.Catalog.Api.Contracts.Properties;
using LocalizeStay.Catalog.Api.ErrorHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LocalizeStay.Catalog.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "LocalizeStay Catalog API", Version = "v1" });
            options.CustomOperationIds(description => description.ActionDescriptor.AttributeRouteInfo?.Name);
            options.CustomSchemaIds(type => type.Name switch
            {
                nameof(PropertyResponse) => "Property",
                nameof(CatalogProblemDetails) => "ProblemDetails",
                nameof(CatalogProblemDetailItem) => "ProblemDetailItem",
                _ => type.Name
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerConfiguration(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "LocalizeStay Catalog API v1");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
