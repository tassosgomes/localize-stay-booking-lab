using LocalizeStay.Catalog.Domain.Properties;

namespace LocalizeStay.Catalog.Application.Properties.Models;

public sealed record PropertyResult(
    Guid Id,
    string Name,
    string Location,
    Guid HostReferenceId,
    PropertyStatus Status);
