using AwesomeAssertions;
using LocalizeStay.Catalog.Domain.Properties;
using Xunit;

namespace LocalizeStay.Catalog.UnitTests.Properties;

[Trait("FeatureSlice", "PropertyCreate")]
public sealed class PropertyTests
{
    private static readonly Guid HostReferenceId = Guid.Parse("421ec5d4-3ba2-4d8e-8958-a51dd0711650");

    [Fact]
    public void Create_WithValidValues_AssignsIdentityActiveStatusAndPreservesInputs()
    {
        const string name = "  Pousada Dunas do Sol  ";
        const string location = " Cumbuco, Caucaia - CE ";

        var property = Property.Create(name, location, HostReferenceId);

        property.Id.Should().NotBe(Guid.Empty);
        property.Name.Should().Be(name);
        property.Location.Should().Be(location);
        property.HostReferenceId.Should().Be(HostReferenceId);
        property.Status.Should().Be(PropertyStatus.Active);
    }

    [Fact]
    public void Create_WhenCalledTwice_GeneratesDistinctIds()
    {
        var first = Property.Create("Pousada A", "Cumbuco", HostReferenceId);
        var second = Property.Create("Pousada A", "Cumbuco", HostReferenceId);

        first.Id.Should().NotBe(second.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingOrBlankName_ThrowsArgumentException(string? name)
    {
        var action = () => Property.Create(name!, "Cumbuco, Caucaia - CE", HostReferenceId);

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingOrBlankLocation_ThrowsArgumentException(string? location)
    {
        var action = () => Property.Create("Pousada Dunas do Sol", location!, HostReferenceId);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNameLongerThanLimit_ThrowsArgumentOutOfRangeException()
    {
        var action = () => Property.Create(new string('a', Property.NameMaxLength + 1), "Cumbuco", HostReferenceId);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithLocationLongerThanLimit_ThrowsArgumentOutOfRangeException()
    {
        var action = () => Property.Create("Pousada", new string('b', Property.LocationMaxLength + 1), HostReferenceId);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithValuesAtExactLimits_Succeeds()
    {
        var property = Property.Create(
            new string('a', Property.NameMaxLength),
            new string('b', Property.LocationMaxLength),
            HostReferenceId);

        property.Name.Should().HaveLength(Property.NameMaxLength);
        property.Location.Should().HaveLength(Property.LocationMaxLength);
    }

    [Fact]
    [Trait("FeatureSlice", "PropertyUpdate")]
    public void EnsureOwnedBy_WithMatchingHost_DoesNotThrow()
    {
        var property = Property.Create("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE", HostReferenceId);

        var action = () => property.EnsureOwnedBy(HostReferenceId);

        action.Should().NotThrow();
    }

    [Fact]
    [Trait("FeatureSlice", "PropertyUpdate")]
    public void EnsureOwnedBy_WithDivergentHost_ThrowsHostOwnershipForbiddenException()
    {
        var property = Property.Create("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE", HostReferenceId);
        var otherHost = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var action = () => property.EnsureOwnedBy(otherHost);

        var exception = action.Should().Throw<HostOwnershipForbiddenException>().Which;
        exception.PropertyId.Should().Be(property.Id);
        exception.HostReferenceId.Should().Be(otherHost);
        property.Name.Should().Be("Pousada Dunas do Sol");
        property.Location.Should().Be("Cumbuco, Caucaia - CE");
        property.HostReferenceId.Should().Be(HostReferenceId);
        property.Status.Should().Be(PropertyStatus.Active);
    }

    [Fact]
    [Trait("FeatureSlice", "PropertyUpdate")]
    public void UpdateDetails_WithNameOnly_PreservesLocationIdentityHostAndStatus()
    {
        var property = Property.Create("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE", HostReferenceId);
        var originalId = property.Id;

        property.UpdateDetails("Pousada Dunas do Sol Boutique", true, null, false);

        property.Id.Should().Be(originalId);
        property.Name.Should().Be("Pousada Dunas do Sol Boutique");
        property.Location.Should().Be("Cumbuco, Caucaia - CE");
        property.HostReferenceId.Should().Be(HostReferenceId);
        property.Status.Should().Be(PropertyStatus.Active);
    }

    [Fact]
    [Trait("FeatureSlice", "PropertyUpdate")]
    public void UpdateDetails_WithLocationOnly_PreservesNameIdentityHostAndStatus()
    {
        var property = Property.Create("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE", HostReferenceId);

        property.UpdateDetails(null, false, "Praia de Cumbuco, Caucaia - CE", true);

        property.Name.Should().Be("Pousada Dunas do Sol");
        property.Location.Should().Be("Praia de Cumbuco, Caucaia - CE");
        property.HostReferenceId.Should().Be(HostReferenceId);
        property.Status.Should().Be(PropertyStatus.Active);
    }

    [Fact]
    [Trait("FeatureSlice", "PropertyUpdate")]
    public void UpdateDetails_WithBothFields_UpdatesNameAndLocationAtomically()
    {
        var property = Property.Create("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE", HostReferenceId);

        property.UpdateDetails("Pousada Dunas do Sol Boutique", true, "Praia de Cumbuco, Caucaia - CE", true);

        property.Name.Should().Be("Pousada Dunas do Sol Boutique");
        property.Location.Should().Be("Praia de Cumbuco, Caucaia - CE");
    }

    [Fact]
    [Trait("FeatureSlice", "PropertyUpdate")]
    public void UpdateDetails_WhenSecondFieldIsInvalid_DoesNotChangeEitherField()
    {
        var property = Property.Create("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE", HostReferenceId);

        var action = () => property.UpdateDetails(
            "Pousada Dunas do Sol Boutique",
            true,
            new string('b', Property.LocationMaxLength + 1),
            true);

        action.Should().Throw<ArgumentOutOfRangeException>();
        property.Name.Should().Be("Pousada Dunas do Sol");
        property.Location.Should().Be("Cumbuco, Caucaia - CE");
        property.HostReferenceId.Should().Be(HostReferenceId);
        property.Status.Should().Be(PropertyStatus.Active);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("FeatureSlice", "PropertyUpdate")]
    public void UpdateDetails_WithBlankPresentName_ThrowsAndPreservesState(string? name)
    {
        var property = Property.Create("Pousada Dunas do Sol", "Cumbuco, Caucaia - CE", HostReferenceId);

        var action = () => property.UpdateDetails(name, true, null, false);

        action.Should().Throw<ArgumentException>();
        property.Name.Should().Be("Pousada Dunas do Sol");
        property.Location.Should().Be("Cumbuco, Caucaia - CE");
    }
}
