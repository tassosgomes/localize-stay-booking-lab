using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace LocalizeStay.Catalog.IntegrationTests.Contracts;

[Collection("CatalogIntegrationTests")]
public sealed class PropertyOpenApiContractTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    [Fact]
    [Trait("FeatureSlice", "PropertyCreate")]
    public async Task GeneratedOpenApi_ContainsCompatibleCreatePropertyOperation()
    {
        var contractYaml = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Contracts", "api-contract.yaml"));
        contractYaml.Should().Contain("operationId: createProperty");
        contractYaml.Should().Contain("/properties:");
        contractYaml.Should().Contain("url: /v1");

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var (path, method, operation) = FindOperation(document, "createProperty");

        ResolveAbsolutePath(document, path).Should().Be("/v1/properties");
        method.Should().Be("post");
        operation.GetProperty("operationId").GetString().Should().Be("createProperty");

        var parameters = operation.GetProperty("parameters");
        parameters.EnumerateArray().Should().Contain(parameter =>
            parameter.GetProperty("name").GetString() == "X-Host-Reference-Id"
            && parameter.GetProperty("in").GetString() == "header"
            && parameter.GetProperty("required").GetBoolean());

        var requestSchema = operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        requestSchema.TryGetProperty("$ref", out var requestRef).Should().BeTrue();
        requestRef.GetString().Should().Contain("CreatePropertyRequest");

        var responses = operation.GetProperty("responses");
        responses.TryGetProperty("201", out _).Should().BeTrue();
        responses.TryGetProperty("400", out _).Should().BeTrue();
        responses.TryGetProperty("500", out _).Should().BeTrue();

        var createdSchema = responses
            .GetProperty("201")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        createdSchema.TryGetProperty("$ref", out var createdRef).Should().BeTrue();
        createdRef.GetString().Should().Contain("Property");
        createdRef.GetString().Should().NotContain("PropertyResponse");
    }

    private static (string Path, string Method, JsonElement Operation) FindOperation(JsonDocument document, string operationId)
    {
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                if (method.Value.TryGetProperty("operationId", out var id)
                    && string.Equals(id.GetString(), operationId, StringComparison.Ordinal))
                {
                    return (path.Name, method.Name, method.Value);
                }
            }
        }

        throw new InvalidOperationException($"operationId '{operationId}' was not found in the generated OpenAPI document.");
    }

    private static string ResolveAbsolutePath(JsonDocument document, string path)
    {
        if (document.RootElement.TryGetProperty("servers", out var servers)
            && servers.ValueKind == JsonValueKind.Array
            && servers.GetArrayLength() > 0)
        {
            var server = servers[0].GetProperty("url").GetString()?.TrimEnd('/') ?? string.Empty;
            return string.Concat(server, path.StartsWith('/') ? path : "/" + path);
        }

        return path.StartsWith('/') ? path : "/" + path;
    }
}
