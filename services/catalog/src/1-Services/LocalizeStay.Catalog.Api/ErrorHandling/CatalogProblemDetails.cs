namespace LocalizeStay.Catalog.Api.ErrorHandling;

public sealed class CatalogProblemDetails
{
    public required string Type { get; init; }

    public required string Title { get; init; }

    public required int Status { get; init; }

    public required string Detail { get; init; }

    public required string Instance { get; init; }

    public required string Code { get; init; }

    public required IReadOnlyList<CatalogProblemDetailItem> Details { get; init; }

    public required string TraceId { get; init; }
}
