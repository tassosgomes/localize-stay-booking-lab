using System.Diagnostics;
using FluentValidation;
using LocalizeStay.Catalog.Application.Abstractions.Persistence;
using LocalizeStay.Catalog.Application.Properties.Models;
using LocalizeStay.Catalog.Domain.Properties;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Catalog.Application.Properties;

public sealed class PropertyService(
    IValidator<CreatePropertyInput> validator,
    IPropertyRepository propertyRepository,
    IUnitOfWork unitOfWork,
    ILogger<PropertyService> logger) : IPropertyService
{
    private readonly IValidator<CreatePropertyInput> _validator = validator;
    private readonly IPropertyRepository _propertyRepository = propertyRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<PropertyService> _logger = logger;

    public async Task<PropertyResult> CreateAsync(CreatePropertyInput input, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(input, cancellationToken);

        var property = Property.Create(input.Name!, input.Location!, input.HostReferenceId);

        await _propertyRepository.AddAsync(property, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "PropertyCreated propertyId={PropertyId} hostReferenceId={HostReferenceId} operation={Operation} traceId={TraceId}",
            property.Id,
            property.HostReferenceId,
            "createProperty",
            Activity.Current?.TraceId.ToString() ?? string.Empty);

        return new PropertyResult(
            property.Id,
            property.Name,
            property.Location,
            property.HostReferenceId,
            property.Status);
    }
}
