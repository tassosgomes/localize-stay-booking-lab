namespace LocalizeStay.Catalog.Api.Contracts.Properties;

public sealed class PropertyResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Location { get; init; }

    public required Guid HostReferenceId { get; init; }

    public required string Status { get; init; }
}
