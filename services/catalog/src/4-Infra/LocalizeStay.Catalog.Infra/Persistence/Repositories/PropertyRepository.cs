using LocalizeStay.Catalog.Application.Abstractions.Persistence;
using LocalizeStay.Catalog.Domain.Properties;

namespace LocalizeStay.Catalog.Infra.Persistence.Repositories;

public sealed class PropertyRepository(CatalogDbContext dbContext) : IPropertyRepository
{
    private readonly CatalogDbContext _dbContext = dbContext;

    public async Task AddAsync(Property property, CancellationToken cancellationToken)
    {
        await _dbContext.Properties.AddAsync(property, cancellationToken);
    }
}
