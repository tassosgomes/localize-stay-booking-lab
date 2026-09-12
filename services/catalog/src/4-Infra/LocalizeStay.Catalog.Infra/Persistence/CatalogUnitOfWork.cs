using LocalizeStay.Catalog.Application.Abstractions.Persistence;

namespace LocalizeStay.Catalog.Infra.Persistence;

public sealed class CatalogUnitOfWork(CatalogDbContext dbContext) : IUnitOfWork
{
    private readonly CatalogDbContext _dbContext = dbContext;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
