namespace LocalizeStay.Catalog.Application.Properties.Models;

public sealed record CreatePropertyInput(Guid HostReferenceId, string? Name, string? Location);
