using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LocalizeStay.Catalog.Api.ErrorHandling;

public static class CatalogProblemDetailsFactory
{
    public const string ValidationType = "https://localize-stay.example/problems/validation-error";
    public const string InternalType = "https://localize-stay.example/problems/internal-error";
    public const string HostOwnershipType = "https://localize-stay.example/problems/host-ownership-forbidden";
    public const string PropertyNotFoundType = "https://localize-stay.example/problems/property-not-found";
    public const string ValidationCode = "VALIDATION_ERROR";
    public const string InternalCode = "INTERNAL_ERROR";
    public const string HostOwnershipCode = "HOST_OWNERSHIP_FORBIDDEN";
    public const string PropertyNotFoundCode = "PROPERTY_NOT_FOUND";

    private static readonly Regex JsonPropertyName = new(
        "JSON property '([^']+)'",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static CatalogProblemDetails Validation(
        string instance,
        string traceId,
        IReadOnlyList<CatalogProblemDetailItem> details) =>
        new()
        {
            Type = ValidationType,
            Title = "Dados inválidos",
            Status = StatusCodes.Status400BadRequest,
            Detail = "Corrija os campos indicados e tente novamente.",
            Instance = instance,
            Code = ValidationCode,
            Details = details,
            TraceId = traceId
        };

    public static CatalogProblemDetails Internal(string instance, string traceId) =>
        new()
        {
            Type = InternalType,
            Title = "Erro interno",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "Ocorreu um erro inesperado. Tente novamente mais tarde.",
            Instance = instance,
            Code = InternalCode,
            Details = [],
            TraceId = traceId
        };

    public static CatalogProblemDetails HostOwnershipForbidden(string instance, string traceId) =>
        new()
        {
            Type = HostOwnershipType,
            Title = "Operação não permitida",
            Status = StatusCodes.Status403Forbidden,
            Detail = "Somente o Host responsável pode editar esta Property.",
            Instance = instance,
            Code = HostOwnershipCode,
            Details = [],
            TraceId = traceId
        };

    public static CatalogProblemDetails PropertyNotFound(string instance, string traceId) =>
        new()
        {
            Type = PropertyNotFoundType,
            Title = "Property não encontrada",
            Status = StatusCodes.Status404NotFound,
            Detail = "Não foi encontrada uma Property com o identificador informado.",
            Instance = instance,
            Code = PropertyNotFoundCode,
            Details = [],
            TraceId = traceId
        };

    public static string ResolveTraceId(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() is { Length: > 0 } traceId
            ? traceId
            : httpContext.TraceIdentifier;

    public static string ResolveInstance(HttpContext httpContext) =>
        httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value! : "/";

    public static IReadOnlyList<CatalogProblemDetailItem> FromModelState(ModelStateDictionary modelState)
    {
        var details = new List<CatalogProblemDetailItem>();

        foreach (var (key, entry) in modelState)
        {
            foreach (var error in entry.Errors)
            {
                var message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? error.Exception?.Message ?? "Valor inválido."
                    : error.ErrorMessage;

                details.Add(new CatalogProblemDetailItem
                {
                    Field = ResolveField(key, message),
                    Message = message
                });
            }
        }

        return details;
    }

    public static IReadOnlyList<CatalogProblemDetailItem> FromJsonException(JsonException exception)
    {
        var message = exception.Message;
        return
        [
            new CatalogProblemDetailItem
            {
                Field = ResolveField(string.Empty, message),
                Message = message
            }
        ];
    }

    private static string ResolveField(string key, string message)
    {
        var jsonProperty = JsonPropertyName.Match(message);
        if (jsonProperty.Success)
        {
            return jsonProperty.Groups[1].Value;
        }

        var lastSegment = key.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? key;

        return lastSegment switch
        {
            "hostReferenceId" or "HostReferenceId" or "X-Host-Reference-Id" => "X-Host-Reference-Id",
            "propertyId" or "PropertyId" => "propertyId",
            "Name" => "name",
            "Location" => "location",
            "request" or "Request" or "$" or "" => "body",
            _ => JsonNamingPolicy.CamelCase.ConvertName(lastSegment)
        };
    }
}
