namespace LocalizeStay.Catalog.Api.ErrorHandling;

public sealed class CatalogProblemDetailItem
{
    public required string Field { get; init; }

    public required string Message { get; init; }
}
