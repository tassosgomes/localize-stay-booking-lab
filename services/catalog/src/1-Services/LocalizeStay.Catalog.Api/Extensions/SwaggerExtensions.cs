using LocalizeStay.Catalog.Api.Contracts.Properties;
using LocalizeStay.Catalog.Api.ErrorHandling;
using LocalizeStay.Catalog.Domain.Properties;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

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
            options.OperationFilter<CatalogOpenApiOperationFilter>();
            options.SchemaFilter<CatalogOpenApiSchemaFilter>();
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

internal sealed class CatalogOpenApiOperationFilter : IOperationFilter
{
    private static readonly string[] ProblemStatuses = ["400", "403", "404", "500"];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var responses = operation.Responses;
        if (responses is not null)
        {
            foreach (var status in ProblemStatuses)
            {
                if (!responses.TryGetValue(status, out var response) || response is not OpenApiResponse problemResponse)
                {
                    continue;
                }

                if (problemResponse.Content is null)
                {
                    continue;
                }

                if (problemResponse.Content.TryGetValue("application/json", out var mediaType))
                {
                    problemResponse.Content.Remove("application/json");
                    problemResponse.Content["application/problem+json"] = mediaType;
                }
            }

            if (string.Equals(operation.OperationId, "createProperty", StringComparison.Ordinal)
                && responses.TryGetValue("201", out var created)
                && created is OpenApiResponse createdResponse)
            {
                createdResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                createdResponse.Headers["Location"] = new OpenApiHeader
                {
                    Description = "Caminho versionado da Property cadastrada.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                };
            }
        }

        if (operation.RequestBody is OpenApiRequestBody requestBody)
        {
            requestBody.Required = true;
        }
    }
}

internal sealed class CatalogOpenApiSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete)
        {
            return;
        }

        if (context.Type == typeof(CreatePropertyRequest))
        {
            RequireProperties(concrete, "name", "location");
            ConfigureTextProperty(concrete, "name", Property.NameMaxLength);
            ConfigureTextProperty(concrete, "location", Property.LocationMaxLength);
            return;
        }

        if (context.Type == typeof(UpdatePropertyRequest))
        {
            concrete.MinProperties = 1;
            ConfigureTextProperty(concrete, "name", Property.NameMaxLength);
            ConfigureTextProperty(concrete, "location", Property.LocationMaxLength);
            return;
        }

        if (context.Type == typeof(PropertyResponse))
        {
            RequireProperties(concrete, "id", "name", "location", "hostReferenceId", "status");
            ConfigureTextProperty(concrete, "name", Property.NameMaxLength);
            ConfigureTextProperty(concrete, "location", Property.LocationMaxLength);
            if (TryGetProperty(concrete, "status", out var statusSchema))
            {
                RemoveNull(statusSchema);
            }
        }
    }

    private static void RequireProperties(OpenApiSchema schema, params string[] names)
    {
        schema.Required ??= new HashSet<string>();
        foreach (var name in names)
        {
            schema.Required.Add(name);
        }
    }

    private static void ConfigureTextProperty(OpenApiSchema schema, string name, int maxLength)
    {
        if (!TryGetProperty(schema, name, out var property))
        {
            return;
        }

        RemoveNull(property);
        property.MinLength = 1;
        property.MaxLength = maxLength;
        property.Pattern = @".*\S.*";
    }

    private static bool TryGetProperty(OpenApiSchema schema, string name, out OpenApiSchema property)
    {
        property = null!;
        if (schema.Properties is null || !schema.Properties.TryGetValue(name, out var candidate))
        {
            return false;
        }

        if (candidate is not OpenApiSchema concrete)
        {
            return false;
        }

        property = concrete;
        return true;
    }

    private static void RemoveNull(OpenApiSchema schema)
    {
        if (schema.Type is { } type)
        {
            schema.Type = type & ~JsonSchemaType.Null;
        }
    }
}
