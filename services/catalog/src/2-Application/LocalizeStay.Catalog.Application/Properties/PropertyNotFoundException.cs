namespace LocalizeStay.Catalog.Application.Properties;

public sealed class PropertyNotFoundException : Exception
{
    public PropertyNotFoundException(Guid propertyId)
        : base("Property was not found.")
    {
        PropertyId = propertyId;
    }

    public Guid PropertyId { get; }
}
