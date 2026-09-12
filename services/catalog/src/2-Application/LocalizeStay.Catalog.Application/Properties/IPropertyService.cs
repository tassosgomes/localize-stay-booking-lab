using LocalizeStay.Catalog.Application.Properties.Models;

namespace LocalizeStay.Catalog.Application.Properties;

public interface IPropertyService
{
    Task<PropertyResult> CreateAsync(CreatePropertyInput input, CancellationToken cancellationToken);

    Task<PropertyResult> UpdateAsync(UpdatePropertyInput input, CancellationToken cancellationToken);
}
