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
}
