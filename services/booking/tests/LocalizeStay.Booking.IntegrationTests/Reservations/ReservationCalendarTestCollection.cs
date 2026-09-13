using Xunit;

namespace LocalizeStay.Booking.IntegrationTests.Reservations;

[CollectionDefinition("ReservationCalendarTests")]
public sealed class ReservationCalendarTestCollection : ICollectionFixture<ReservationCalendarFixture>
{
}
