using LocalizeStay.Catalog.Api.ErrorHandling;
using Microsoft.AspNetCore.Mvc;

namespace LocalizeStay.Catalog.Api.Extensions;

public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddErrorHandlingConfiguration(this IServiceCollection services)
    {
        services.AddExceptionHandler<CatalogExceptionHandler>();
        services.AddProblemDetails();
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var httpContext = context.HttpContext;
                var problem = CatalogProblemDetailsFactory.Validation(
                    CatalogProblemDetailsFactory.ResolveInstance(httpContext),
                    CatalogProblemDetailsFactory.ResolveTraceId(httpContext),
                    CatalogProblemDetailsFactory.FromModelState(context.ModelState));

                return new JsonResult(problem)
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentType = "application/problem+json"
                };
            };
        });

        return services;
    }

    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        return app;
    }
}
