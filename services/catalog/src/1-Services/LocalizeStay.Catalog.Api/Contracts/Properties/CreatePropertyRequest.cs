using System.Text.Json.Serialization;

namespace LocalizeStay.Catalog.Api.Contracts.Properties;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreatePropertyRequest
{
    public string? Name { get; init; }

    public string? Location { get; init; }
}
