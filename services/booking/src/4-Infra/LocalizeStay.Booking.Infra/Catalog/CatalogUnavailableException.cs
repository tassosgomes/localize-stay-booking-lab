namespace LocalizeStay.Booking.Infra.Catalog;

public sealed class CatalogUnavailableException : Exception
{
    public CatalogUnavailableException(string message)
        : base(message)
    {
    }

    public CatalogUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
