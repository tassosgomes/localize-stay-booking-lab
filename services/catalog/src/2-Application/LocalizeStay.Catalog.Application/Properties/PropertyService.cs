using System.Diagnostics;
using FluentValidation;
using LocalizeStay.Catalog.Application.Abstractions.Persistence;
using LocalizeStay.Catalog.Application.Properties.Models;
using LocalizeStay.Catalog.Domain.Properties;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Catalog.Application.Properties;

public sealed class PropertyService(
    IValidator<CreatePropertyInput> createValidator,
    IValidator<UpdatePropertyInput> updateValidator,
    IPropertyRepository propertyRepository,
    IUnitOfWork unitOfWork,
    ILogger<PropertyService> logger) : IPropertyService
{
    private readonly IValidator<CreatePropertyInput> _createValidator = createValidator;
    private readonly IValidator<UpdatePropertyInput> _updateValidator = updateValidator;
    private readonly IPropertyRepository _propertyRepository = propertyRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<PropertyService> _logger = logger;

    public async Task<PropertyResult> CreateAsync(CreatePropertyInput input, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(input, cancellationToken);

        var property = Property.Create(input.Name!, input.Location!, input.HostReferenceId);

        await _propertyRepository.AddAsync(property, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "PropertyCreated propertyId={PropertyId} hostReferenceId={HostReferenceId} operation={Operation} traceId={TraceId}",
            property.Id,
            property.HostReferenceId,
            "createProperty",
            Activity.Current?.TraceId.ToString() ?? string.Empty);

        return Map(property);
    }

    public async Task<PropertyResult> UpdateAsync(UpdatePropertyInput input, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(input, cancellationToken);

        var property = await _propertyRepository.GetByIdForUpdateAsync(input.PropertyId, cancellationToken);
        if (property is null)
        {
            throw new PropertyNotFoundException(input.PropertyId);
        }

        try
        {
            property.EnsureOwnedBy(input.HostReferenceId);
        }
        catch (HostOwnershipForbiddenException)
        {
            _logger.LogWarning(
                "HOST_OWNERSHIP_FORBIDDEN propertyId={PropertyId} hostReferenceId={HostReferenceId} operation={Operation} traceId={TraceId}",
                property.Id,
                input.HostReferenceId,
                "updateProperty",
                Activity.Current?.TraceId.ToString() ?? string.Empty);
            throw;
        }

        property.UpdateDetails(input.Name, input.NameIsPresent, input.Location, input.LocationIsPresent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "PropertyUpdated propertyId={PropertyId} hostReferenceId={HostReferenceId} operation={Operation} traceId={TraceId}",
            property.Id,
            property.HostReferenceId,
            "updateProperty",
            Activity.Current?.TraceId.ToString() ?? string.Empty);

        return Map(property);
    }

    private static PropertyResult Map(Property property) =>
        new(
            property.Id,
            property.Name,
            property.Location,
            property.HostReferenceId,
            property.Status);
}
