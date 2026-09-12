namespace LocalizeStay.Catalog.IntegrationTests;

using Xunit;

[CollectionDefinition("CatalogIntegrationTests")]
public sealed class CatalogIntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>;
