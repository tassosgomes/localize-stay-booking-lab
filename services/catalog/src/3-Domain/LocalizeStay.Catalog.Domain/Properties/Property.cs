namespace LocalizeStay.Catalog.Domain.Properties;

public sealed class Property
{
    public const int NameMaxLength = 120;
    public const int LocationMaxLength = 500;

    private Property()
    {
        Name = null!;
        Location = null!;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Location { get; private set; }

    public Guid HostReferenceId { get; private set; }

    public PropertyStatus Status { get; private set; }

    public static Property Create(string name, string location, Guid hostReferenceId)
    {
        EnsureValidName(name);
        EnsureValidLocation(location);

        return new Property
        {
            Id = Guid.NewGuid(),
            Name = name,
            Location = location,
            HostReferenceId = hostReferenceId,
            Status = PropertyStatus.Active
        };
    }

    public void EnsureOwnedBy(Guid hostReferenceId)
    {
        if (HostReferenceId != hostReferenceId)
        {
            throw new HostOwnershipForbiddenException(Id, hostReferenceId);
        }
    }

    public void UpdateDetails(string? name, bool updateName, string? location, bool updateLocation)
    {
        if (!updateName && !updateLocation)
        {
            throw new ArgumentException("At least one field must be provided.");
        }

        if (updateName)
        {
            EnsureValidName(name!);
        }

        if (updateLocation)
        {
            EnsureValidLocation(location!);
        }

        if (updateName)
        {
            Name = name!;
        }

        if (updateLocation)
        {
            Location = location!;
        }
    }

    private static void EnsureValidName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Length > NameMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                name.Length,
                $"Name must have at most {NameMaxLength} characters.");
        }
    }

    private static void EnsureValidLocation(string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        if (location.Length > LocationMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(location),
                location.Length,
                $"Location must have at most {LocationMaxLength} characters.");
        }
    }
}
