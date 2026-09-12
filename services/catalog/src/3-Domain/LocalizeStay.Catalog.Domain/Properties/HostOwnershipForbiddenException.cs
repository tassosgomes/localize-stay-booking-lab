namespace LocalizeStay.Catalog.Domain.Properties;

public sealed class HostOwnershipForbiddenException : Exception
{
    public HostOwnershipForbiddenException(Guid propertyId, Guid hostReferenceId)
        : base("Only the responsible Host can edit this Property.")
    {
        PropertyId = propertyId;
        HostReferenceId = hostReferenceId;
    }

    public Guid PropertyId { get; }

    public Guid HostReferenceId { get; }
}
