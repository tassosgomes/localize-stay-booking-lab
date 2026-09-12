using FluentValidation;
using LocalizeStay.Catalog.Application.Properties;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Catalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        services.AddScoped<IPropertyService, PropertyService>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
