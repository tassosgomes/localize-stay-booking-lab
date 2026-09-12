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
[Trait("FeatureSlice", "PropertyCreate")]
public sealed class CreatePropertyTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid HostReferenceId = Guid.Parse("421ec5d4-3ba2-4d8e-8958-a51dd0711650");

    private readonly CustomWebApplicationFactory _factory;

    public CreatePropertyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetCatalogDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_WithValidPayload_Returns201PersistsAndLogsSafely()
    {
        const string name = "  Pousada Dunas do Sol  ";
        const string location = "Cumbuco, Caucaia - CE";
        var client = _factory.CreateClient();

        var response = await client.SendAsync(CreateRequest(HostReferenceId, $$"""{"name":"{{name}}","location":"{{location}}"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<PropertyResponse>(JsonOptions);
        created.Should().NotBeNull();
        created!.Id.Should().NotBe(Guid.Empty);
        created.Name.Should().Be(name);
        created.Location.Should().Be(location);
        created.HostReferenceId.Should().Be(HostReferenceId);
        created.Status.Should().Be("active");
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/v1/properties/{created.Id}");

        var persisted = await FindPropertyAsync(created.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(name);
        persisted.Location.Should().Be(location);
        persisted.HostReferenceId.Should().Be(HostReferenceId);
        persisted.Status.Should().Be(PropertyStatus.Active);

        var logs = string.Join('\n', _factory.SnapshotLogs());
        logs.Should().Contain("PropertyCreated");
        logs.Should().Contain(created.Id.ToString());
        logs.Should().Contain(HostReferenceId.ToString());
        logs.Should().NotContain(name.Trim());
        logs.Should().NotContain(location);
    }

    [Fact]
    public async Task Create_WithDuplicateNameAndLocation_AcceptsDistinctIds()
    {
        const string payload = """{"name":"Pousada Dunas do Sol","location":"Cumbuco, Caucaia - CE"}""";
        var client = _factory.CreateClient();

        var first = await client.SendAsync(CreateRequest(HostReferenceId, payload));
        var second = await client.SendAsync(CreateRequest(HostReferenceId, payload));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstCreated = await first.Content.ReadFromJsonAsync<PropertyResponse>(JsonOptions);
        var secondCreated = await second.Content.ReadFromJsonAsync<PropertyResponse>(JsonOptions);
        firstCreated!.Id.Should().NotBe(secondCreated!.Id);
        (await CountPropertiesAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"location":"Cumbuco, Caucaia - CE"}""")]
    [InlineData("""{"name":null,"location":"Cumbuco, Caucaia - CE"}""")]
    [InlineData("""{"name":"","location":"Cumbuco, Caucaia - CE"}""")]
    [InlineData("""{"name":"   ","location":"Cumbuco, Caucaia - CE"}""")]
    [InlineData("""{"name":"Pousada Dunas do Sol"}""")]
    [InlineData("""{"name":"Pousada Dunas do Sol","location":null}""")]
    [InlineData("""{"name":"Pousada Dunas do Sol","location":""}""")]
    [InlineData("""{"name":"Pousada Dunas do Sol","location":"   "}""")]
    [InlineData("""{"name":"Pousada Dunas do Sol","location":"Cumbuco, Caucaia - CE","unknown":true}""")]
    [InlineData("{")]
    public async Task Create_WithInvalidBody_Returns400AndDoesNotPersist(string payload)
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(CreateRequest(HostReferenceId, payload));

        await AssertValidationProblemAsync(response);
        (await CountPropertiesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_WithNameAboveLimit_Returns400AndDoesNotPersist()
    {
        var payload = $$"""{"name":"{{new string('a', Property.NameMaxLength + 1)}}","location":"Cumbuco"}""";
        var client = _factory.CreateClient();

        var response = await client.SendAsync(CreateRequest(HostReferenceId, payload));

        var problem = await AssertValidationProblemAsync(response);
        problem.Details.Should().Contain(item => item.Field == "name");
        (await CountPropertiesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_WithLocationAboveLimit_Returns400AndDoesNotPersist()
    {
        var payload = $$"""{"name":"Pousada","location":"{{new string('b', Property.LocationMaxLength + 1)}}"}""";
        var client = _factory.CreateClient();

        var response = await client.SendAsync(CreateRequest(HostReferenceId, payload));

        var problem = await AssertValidationProblemAsync(response);
        problem.Details.Should().Contain(item => item.Field == "location");
        (await CountPropertiesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_WithMissingHostHeader_Returns400AndDoesNotPersist()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/properties")
        {
            Content = JsonContent.Create(new { name = "Pousada Dunas do Sol", location = "Cumbuco, Caucaia - CE" })
        };

        var response = await client.SendAsync(request);

        await AssertValidationProblemAsync(response);
        (await CountPropertiesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_WithInvalidHostHeader_Returns400AndDoesNotPersist()
    {
        var client = _factory.CreateClient();
        using var request = CreateRequest("not-a-uuid", """{"name":"Pousada Dunas do Sol","location":"Cumbuco, Caucaia - CE"}""");

        var response = await client.SendAsync(request);

        await AssertValidationProblemAsync(response);
        (await CountPropertiesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_WhenUnitOfWorkFails_ReturnsSanitized500AndDoesNotPersist()
    {
        using var failingFactory = _factory.WithThrowingUnitOfWork();
        var client = failingFactory.CreateClient();

        var response = await client.SendAsync(
            CreateRequest(HostReferenceId, """{"name":"Pousada Dunas do Sol","location":"Cumbuco, Caucaia - CE"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Controlled failure");
        body.Should().NotContain("InvalidOperationException");
        body.Should().NotContain("Pousada Dunas do Sol");
        body.Should().NotContain("Cumbuco, Caucaia - CE");

        var problem = JsonSerializer.Deserialize<CatalogProblemDetails>(body, JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(500);
        problem.Code.Should().Be("INTERNAL_ERROR");
        problem.Details.Should().BeEmpty();
        problem.TraceId.Should().NotBeNullOrWhiteSpace();
        problem.Type.Should().Be(CatalogProblemDetailsFactory.InternalType);
        (await CountPropertiesAsync()).Should().Be(0);
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

    private static HttpRequestMessage CreateRequest(Guid hostReferenceId, string json) =>
        CreateRequest(hostReferenceId.ToString(), json);

    private static HttpRequestMessage CreateRequest(string hostReferenceId, string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/properties")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Host-Reference-Id", hostReferenceId);
        return request;
    }
}
