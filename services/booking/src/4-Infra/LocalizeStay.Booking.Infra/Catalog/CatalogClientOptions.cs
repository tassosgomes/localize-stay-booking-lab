namespace LocalizeStay.Booking.Infra.Catalog;

public sealed class CatalogClientOptions
{
    public const string SectionName = "CatalogClient";

    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 3;
}
