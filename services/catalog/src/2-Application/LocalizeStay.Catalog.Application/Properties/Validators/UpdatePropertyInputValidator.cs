using FluentValidation;
using LocalizeStay.Catalog.Application.Properties.Models;
using LocalizeStay.Catalog.Domain.Properties;

namespace LocalizeStay.Catalog.Application.Properties.Validators;

public sealed class UpdatePropertyInputValidator : AbstractValidator<UpdatePropertyInput>
{
    public UpdatePropertyInputValidator()
    {
        RuleFor(input => input)
            .Must(input => input.NameIsPresent || input.LocationIsPresent)
            .WithMessage("Informe ao menos um campo para atualizar.")
            .OverridePropertyName("body");

        RuleFor(input => input.Name)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("O nome é obrigatório.")
            .OverridePropertyName("name")
            .NotEmpty()
            .WithMessage("O nome é obrigatório.")
            .Must(HasNonWhitespaceCharacter)
            .WithMessage("O nome não pode conter apenas espaços.")
            .MaximumLength(Property.NameMaxLength)
            .WithMessage($"O nome deve ter no máximo {Property.NameMaxLength} caracteres.")
            .When(input => input.NameIsPresent);

        RuleFor(input => input.Location)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("A localização é obrigatória.")
            .OverridePropertyName("location")
            .NotEmpty()
            .WithMessage("A localização é obrigatória.")
            .Must(HasNonWhitespaceCharacter)
            .WithMessage("A localização não pode conter apenas espaços.")
            .MaximumLength(Property.LocationMaxLength)
            .WithMessage($"A localização deve ter no máximo {Property.LocationMaxLength} caracteres.")
            .When(input => input.LocationIsPresent);
    }

    private static bool HasNonWhitespaceCharacter(string? value) =>
        value is not null && value.Any(character => !char.IsWhiteSpace(character));
}
