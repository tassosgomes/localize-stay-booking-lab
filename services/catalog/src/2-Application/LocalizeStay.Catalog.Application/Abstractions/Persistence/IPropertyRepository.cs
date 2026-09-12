using LocalizeStay.Catalog.Domain.Properties;

namespace LocalizeStay.Catalog.Application.Abstractions.Persistence;

public interface IPropertyRepository
{
    Task AddAsync(Property property, CancellationToken cancellationToken);

    Task<Property?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);
}
