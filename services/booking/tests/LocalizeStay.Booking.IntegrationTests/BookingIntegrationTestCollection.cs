namespace LocalizeStay.Booking.IntegrationTests;

using Xunit;

[CollectionDefinition("BookingIntegrationTests")]
public sealed class BookingIntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>;
