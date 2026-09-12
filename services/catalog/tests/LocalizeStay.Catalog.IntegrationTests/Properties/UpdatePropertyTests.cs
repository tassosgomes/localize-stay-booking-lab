using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.Catalog.Api.Contracts.Properties;
using LocalizeStay.Catalog.Api.ErrorHandling;
using LocalizeStay.Catalog.Domain.Properties;
using LocalizeStay.Catalog.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LocalizeStay.Catalog.IntegrationTests.Properties;

[Collection("CatalogIntegrationTests")]
[Trait("FeatureSlice", "PropertyUpdate")]
public sealed class UpdatePropertyTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid HostReferenceId = Guid.Parse("421ec5d4-3ba2-4d8e-8958-a51dd0711650");
    private static readonly Guid OtherHostReferenceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly CustomWebApplicationFactory _factory;

    public UpdatePropertyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetCatalogDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Update_WithNameOnly_Returns200PreservesOtherFieldsAndLogsSafely()
    {
        const string originalName = "Pousada Dunas do Sol";
        const string originalLocation = "Cumbuco, Caucaia - CE";
        const string updatedName = "  Pousada Dunas do Sol Boutique  ";
        var created = await SeedPropertyAsync(originalName, originalLocation);
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            PatchRequest(created.Id, HostReferenceId, $$"""{"name":"{{updatedName}}"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<PropertyResponse>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.Id.Should().Be(created.Id);
        updated.Name.Should().Be(updatedName);
        updated.Location.Should().Be(originalLocation);
        updated.HostReferenceId.Should().Be(HostReferenceId);
        updated.Status.Should().Be("active");

        var persisted = await FindPropertyAsync(created.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(updatedName);
        persisted.Location.Should().Be(originalLocation);
        persisted.HostReferenceId.Should().Be(HostReferenceId);
        persisted.Status.Should().Be(PropertyStatus.Active);

        var logs = string.Join('\n', _factory.SnapshotLogs());
        logs.Should().Contain("PropertyUpdated");
        logs.Should().Contain(created.Id.ToString());
        logs.Should().Contain(HostReferenceId.ToString());
        logs.Should().NotContain(updatedName.Trim());
        logs.Should().NotContain(originalLocation);
    }

    [Fact]
    public async Task Update_WithLocationOnly_Returns200AndPreservesName()
    {
        const string originalName = "Pousada Dunas do Sol";
        var created = await SeedPropertyAsync(originalName, "Cumbuco, Caucaia - CE");
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            PatchRequest(created.Id, HostReferenceId, """{"location":"Praia de Cumbuco, Caucaia - CE"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<PropertyResponse>(JsonOptions);
        updated!.Name.Should().Be(originalName);
        updated.Location.Should().Be("Praia de Cumbuco, Caucaia - CE");
        updated.Id.Should().Be(created.Id);
        updated.HostReferenceId.Should().Be(HostReferenceId);
        updated.Status.Should().Be("active");
    }

    [Fact]
    public async Task Update_WithBothFields_Returns200()
    {
        var created = await SeedPropertyAsync("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE");
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            PatchRequest(
                created.Id,
                HostReferenceId,
                """{"name":"Pousada Dunas do Sol Boutique","location":"Praia de Cumbuco, Caucaia - CE"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<PropertyResponse>(JsonOptions);
        updated!.Name.Should().Be("Pousada Dunas do Sol Boutique");
        updated.Location.Should().Be("Praia de Cumbuco, Caucaia - CE");
        updated.Id.Should().Be(created.Id);
        updated.HostReferenceId.Should().Be(HostReferenceId);
        updated.Status.Should().Be("active");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"name":null}""")]
    [InlineData("""{"name":""}""")]
    [InlineData("""{"name":"   "}""")]
    [InlineData("""{"location":null}""")]
    [InlineData("""{"location":""}""")]
    [InlineData("""{"location":"   "}""")]
    [InlineData("""{"unknown":true}""")]
    [InlineData("""{"name":"Pousada Boutique","status":"inactive"}""")]
    [InlineData("""{"id":"8ce29b2c-e67e-4a7d-bb3d-2ed87310f28a"}""")]
    [InlineData("""{"hostReferenceId":"421ec5d4-3ba2-4d8e-8958-a51dd0711650"}""")]
    [InlineData("{")]
    public async Task Update_WithInvalidBody_Returns400AndPreservesState(string payload)
    {
        var created = await SeedPropertyAsync("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE");
        var client = _factory.CreateClient();

        var response = await client.SendAsync(PatchRequest(created.Id, HostReferenceId, payload));

        await AssertValidationProblemAsync(response);
        await AssertUnchangedAsync(created);
    }

    [Fact]
    public async Task Update_WithNameAboveLimit_Returns400AndPreservesState()
    {
        var created = await SeedPropertyAsync("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE");
        var payload = $$"""{"name":"{{new string('a', Property.NameMaxLength + 1)}}"}""";
        var client = _factory.CreateClient();

        var response = await client.SendAsync(PatchRequest(created.Id, HostReferenceId, payload));

        var problem = await AssertValidationProblemAsync(response);
        problem.Details.Should().Contain(item => item.Field == "name");
        await AssertUnchangedAsync(created);
    }

    [Fact]
    public async Task Update_WithLocationAboveLimit_Returns400AndPreservesState()
    {
        var created = await SeedPropertyAsync("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE");
        var payload = $$"""{"location":"{{new string('b', Property.LocationMaxLength + 1)}}"}""";
        var client = _factory.CreateClient();

        var response = await client.SendAsync(PatchRequest(created.Id, HostReferenceId, payload));

        var problem = await AssertValidationProblemAsync(response);
        problem.Details.Should().Contain(item => item.Field == "location");
        await AssertUnchangedAsync(created);
    }

    [Fact]
    public async Task Update_WithInvalidPropertyId_Returns400AndPreservesState()
    {
        var created = await SeedPropertyAsync("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE");
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/v1/properties/not-a-uuid")
        {
            Content = new StringContent("""{"name":"Pousada Boutique"}""", Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Host-Reference-Id", HostReferenceId.ToString());

        var response = await client.SendAsync(request);

        await AssertValidationProblemAsync(response);
        await AssertUnchangedAsync(created);
    }

    [Fact]
    public async Task Update_WithMissingHostHeader_Returns400AndPreservesState()
    {
        var created = await SeedPropertyAsync("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE");
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/properties/{created.Id}")
        {
            Content = JsonContent.Create(new { name = "Pousada Boutique" })
        };

        var response = await client.SendAsync(request);

        await AssertValidationProblemAsync(response);
        await AssertUnchangedAsync(created);
    }

    [Fact]
    public async Task Update_WithInvalidHostHeader_Returns400AndPreservesState()
    {
        var created = await SeedPropertyAsync("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE");
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            PatchRequest(created.Id, "not-a-uuid", """{"name":"Pousada Boutique"}"""));

        await AssertValidationProblemAsync(response);
        await AssertUnchangedAsync(created);
    }

    [Fact]
    public async Task Update_WithDivergentHost_Returns403PreservesStateAndLogsWarningWithoutPayload()
    {
        const string originalName = "Pousada Dunas do Sol";
        const string originalLocation = "Cumbuco, Caucaia - CE";
        var created = await SeedPropertyAsync(originalName, originalLocation);
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            PatchRequest(created.Id, OtherHostReferenceId, """{"name":"Pousada Boutique"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<CatalogProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(403);
        problem.Code.Should().Be("HOST_OWNERSHIP_FORBIDDEN");
        problem.Details.Should().BeEmpty();
        problem.TraceId.Should().NotBeNullOrWhiteSpace();
        problem.Type.Should().Be(CatalogProblemDetailsFactory.HostOwnershipType);
        await AssertUnchangedAsync(created);

        var logs = string.Join('\n', _factory.SnapshotLogs());
        logs.Should().Contain("HOST_OWNERSHIP_FORBIDDEN");
        logs.Should().Contain(created.Id.ToString());
        logs.Should().Contain(OtherHostReferenceId.ToString());
        logs.Should().NotContain("Pousada Boutique");
        logs.Should().NotContain(originalLocation);
    }

    [Fact]
    public async Task Update_WithUnknownProperty_Returns404AndDoesNotCreate()
    {
        var missingId = Guid.Parse("8ce29b2c-e67e-4a7d-bb3d-2ed87310f28a");
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            PatchRequest(missingId, HostReferenceId, """{"name":"Pousada Boutique"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<CatalogProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
        problem.Code.Should().Be("PROPERTY_NOT_FOUND");
        problem.Details.Should().BeEmpty();
        problem.TraceId.Should().NotBeNullOrWhiteSpace();
        problem.Type.Should().Be(CatalogProblemDetailsFactory.PropertyNotFoundType);
        (await CountPropertiesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Update_WhenUnitOfWorkFails_ReturnsSanitized500AndPreservesState()
    {
        var created = await SeedPropertyAsync("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE");
        using var failingFactory = _factory.WithThrowingUnitOfWork();
        var client = failingFactory.CreateClient();

        var response = await client.SendAsync(
            PatchRequest(created.Id, HostReferenceId, """{"name":"Pousada Boutique"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Controlled failure");
        body.Should().NotContain("InvalidOperationException");
        body.Should().NotContain("Pousada Boutique");
        body.Should().NotContain("Cumbuco, Caucaia - CE");

        var problem = JsonSerializer.Deserialize<CatalogProblemDetails>(body, JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(500);
        problem.Code.Should().Be("INTERNAL_ERROR");
        problem.Details.Should().BeEmpty();
        problem.TraceId.Should().NotBeNullOrWhiteSpace();
        problem.Type.Should().Be(CatalogProblemDetailsFactory.InternalType);
        await AssertUnchangedAsync(created);
    }

    private async Task<PropertyResponse> SeedPropertyAsync(string name, string location)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/properties")
        {
            Content = new StringContent(
                $$"""{"name":"{{name}}","location":"{{location}}"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Host-Reference-Id", HostReferenceId.ToString());

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<PropertyResponse>(JsonOptions);
        created.Should().NotBeNull();
        return created!;
    }

    private async Task AssertUnchangedAsync(PropertyResponse created)
    {
        var persisted = await FindPropertyAsync(created.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(created.Name);
        persisted.Location.Should().Be(created.Location);
        persisted.HostReferenceId.Should().Be(created.HostReferenceId);
        persisted.Status.Should().Be(PropertyStatus.Active);
        (await CountPropertiesAsync()).Should().Be(1);
    }

    private async Task<int> CountPropertiesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await dbContext.Properties.CountAsync();
    }

    private async Task<Property?> FindPropertyAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await dbContext.Properties.AsNoTracking().SingleOrDefaultAsync(property => property.Id == id);
    }

    private static async Task<CatalogProblemDetails> AssertValidationProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<CatalogProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Code.Should().Be("VALIDATION_ERROR");
        problem.Details.Should().NotBeNull();
        problem.TraceId.Should().NotBeNullOrWhiteSpace();
        problem.Type.Should().Be(CatalogProblemDetailsFactory.ValidationType);
        return problem;
    }

    private static HttpRequestMessage PatchRequest(Guid propertyId, Guid hostReferenceId, string json) =>
        PatchRequest(propertyId, hostReferenceId.ToString(), json);

    private static HttpRequestMessage PatchRequest(Guid propertyId, string hostReferenceId, string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/properties/{propertyId}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Host-Reference-Id", hostReferenceId);
        return request;
    }
}
