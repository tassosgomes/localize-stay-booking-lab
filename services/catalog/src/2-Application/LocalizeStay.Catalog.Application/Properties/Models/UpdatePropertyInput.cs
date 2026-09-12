namespace LocalizeStay.Catalog.Application.Properties.Models;

public sealed record UpdatePropertyInput(
    Guid PropertyId,
    Guid HostReferenceId,
    bool NameIsPresent,
    string? Name,
    bool LocationIsPresent,
    string? Location);
