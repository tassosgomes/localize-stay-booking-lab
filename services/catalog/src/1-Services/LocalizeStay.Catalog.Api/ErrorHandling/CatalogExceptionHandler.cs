using System.Text.Json;
using FluentValidation;
using LocalizeStay.Catalog.Application.Properties;
using LocalizeStay.Catalog.Domain.Properties;
using Microsoft.AspNetCore.Diagnostics;

namespace LocalizeStay.Catalog.Api.ErrorHandling;

public sealed class CatalogExceptionHandler(ILogger<CatalogExceptionHandler> logger) : IExceptionHandler
{
    private readonly ILogger<CatalogExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var instance = CatalogProblemDetailsFactory.ResolveInstance(httpContext);
        var traceId = CatalogProblemDetailsFactory.ResolveTraceId(httpContext);

        var problem = exception switch
        {
            ValidationException validationException => CatalogProblemDetailsFactory.Validation(
                instance,
                traceId,
                validationException.Errors
                    .Select(error => new CatalogProblemDetailItem
                    {
                        Field = error.PropertyName,
                        Message = error.ErrorMessage
                    })
                    .ToArray()),
            JsonException jsonException => CatalogProblemDetailsFactory.Validation(
                instance,
                traceId,
                CatalogProblemDetailsFactory.FromJsonException(jsonException)),
            BadHttpRequestException => CatalogProblemDetailsFactory.Validation(
                instance,
                traceId,
                [
                    new CatalogProblemDetailItem
                    {
                        Field = "body",
                        Message = "JSON inválido."
                    }
                ]),
            HostOwnershipForbiddenException => CatalogProblemDetailsFactory.HostOwnershipForbidden(instance, traceId),
            PropertyNotFoundException => CatalogProblemDetailsFactory.PropertyNotFound(instance, traceId),
            _ => CatalogProblemDetailsFactory.Internal(instance, traceId)
        };

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "INTERNAL_ERROR traceId={TraceId} instance={Instance}",
                traceId,
                instance);
        }
        else if (exception is HostOwnershipForbiddenException ownershipException)
        {
            _logger.LogWarning(
                "HOST_OWNERSHIP_FORBIDDEN propertyId={PropertyId} hostReferenceId={HostReferenceId} traceId={TraceId} instance={Instance}",
                ownershipException.PropertyId,
                ownershipException.HostReferenceId,
                traceId,
                instance);
        }
        else
        {
            _logger.LogInformation(
                "{Code} traceId={TraceId} instance={Instance} code={Code}",
                problem.Code,
                traceId,
                instance,
                problem.Code);
        }

        httpContext.Response.StatusCode = problem.Status;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            "application/problem+json",
            cancellationToken);
        return true;
    }
}
