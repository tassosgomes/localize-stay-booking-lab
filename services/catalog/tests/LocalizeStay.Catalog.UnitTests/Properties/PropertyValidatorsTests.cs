using AwesomeAssertions;
using LocalizeStay.Catalog.Application.Properties.Models;
using LocalizeStay.Catalog.Application.Properties.Validators;
using LocalizeStay.Catalog.Domain.Properties;
using Xunit;

namespace LocalizeStay.Catalog.UnitTests.Properties;

[Trait("FeatureSlice", "PropertyCreate")]
public sealed class PropertyValidatorsTests
{
    private readonly CreatePropertyInputValidator _validator = new();
    private static readonly Guid HostReferenceId = Guid.Parse("421ec5d4-3ba2-4d8e-8958-a51dd0711650");

    [Fact]
    public async Task Validate_WithValidInput_Succeeds()
    {
        var input = new CreatePropertyInput(HostReferenceId, "Pousada Dunas do Sol", "Cumbuco, Caucaia - CE");

        var result = await _validator.ValidateAsync(input);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithLeadingAndTrailingSpaces_SucceedsWithoutTrimming()
    {
        var input = new CreatePropertyInput(HostReferenceId, "  Pousada  ", "  Cumbuco  ");

        var result = await _validator.ValidateAsync(input);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithMissingOrBlankName_Fails(string? name)
    {
        var input = new CreatePropertyInput(HostReferenceId, name, "Cumbuco, Caucaia - CE");

        var result = await _validator.ValidateAsync(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithMissingOrBlankLocation_Fails(string? location)
    {
        var input = new CreatePropertyInput(HostReferenceId, "Pousada Dunas do Sol", location);

        var result = await _validator.ValidateAsync(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "location");
    }

    [Fact]
    public async Task Validate_WithNameAboveLimit_Fails()
    {
        var input = new CreatePropertyInput(
            HostReferenceId,
            new string('a', Property.NameMaxLength + 1),
            "Cumbuco, Caucaia - CE");

        var result = await _validator.ValidateAsync(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "name");
    }

    [Fact]
    public async Task Validate_WithLocationAboveLimit_Fails()
    {
        var input = new CreatePropertyInput(
            HostReferenceId,
            "Pousada Dunas do Sol",
            new string('b', Property.LocationMaxLength + 1));

        var result = await _validator.ValidateAsync(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "location");
    }
}
