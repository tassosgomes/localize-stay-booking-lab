namespace LocalizeStay.Payment.IntegrationTests;

using Xunit;

[CollectionDefinition("PaymentIntegrationTests")]
public sealed class PaymentIntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>;
