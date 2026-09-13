namespace LocalizeStay.Booking.Domain.Reservations;

public sealed record AvailabilityFacts(
    bool Active,
    int MaxGuests,
    bool AvailableForPeriod,
    decimal PricePerNight,
    string Currency);
