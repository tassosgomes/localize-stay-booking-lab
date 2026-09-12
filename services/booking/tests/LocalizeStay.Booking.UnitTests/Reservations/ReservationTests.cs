using LocalizeStay.Booking.Domain.Reservations;
using LocalizeStay.Booking.Domain.Reservations.Exceptions;
using Xunit;

namespace LocalizeStay.Booking.UnitTests.Reservations;

public sealed class ReservationTests
{
    private static readonly Guid AccommodationId = Guid.NewGuid();

    private static readonly DateOnly CheckIn = new(2026, 10, 1);

    private static readonly AvailabilityFacts ValidFacts = new(
        Active: true,
        MaxGuests: 4,
        AvailableForPeriod: true,
        PricePerNight: 350.00m,
        Currency: "BRL");

    [Fact]
    public void EnsurePeriodIsValid_with_checkOut_equal_to_checkIn_throws_PeriodoInvalido()
    {
        var sameDay = new DateOnly(2026, 10, 1);

        Assert.Throws<PeriodoInvalidoException>(() => Reservation.EnsurePeriodIsValid(sameDay, sameDay));
    }

    [Fact]
    public void EnsurePeriodIsValid_with_checkOut_before_checkIn_throws_PeriodoInvalido()
    {
        Assert.Throws<PeriodoInvalidoException>(
            () => Reservation.EnsurePeriodIsValid(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void EnsurePeriodIsValid_with_checkOut_after_checkIn_does_not_throw()
    {
        Reservation.EnsurePeriodIsValid(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2));
    }

    [Fact]
    public void EnsureGuestsCountIsValid_with_zero_guests_throws_QuantidadeHospedesInvalida()
    {
        Assert.Throws<QuantidadeHospedesInvalidaException>(() => Reservation.EnsureGuestsCountIsValid(0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-3)]
    public void EnsureGuestsCountIsValid_with_negative_guests_throws_QuantidadeHospedesInvalida(int guestsCount)
    {
        Assert.Throws<QuantidadeHospedesInvalidaException>(() => Reservation.EnsureGuestsCountIsValid(guestsCount));
    }

    [Fact]
    public void EnsureGuestsCountIsValid_with_one_guest_does_not_throw()
    {
        Reservation.EnsureGuestsCountIsValid(1);
    }

    [Fact]
    public void Create_with_inactive_accommodation_throws_AcomodacaoIndisponivel()
    {
        var facts = ValidFacts with { Active = false };

        Assert.Throws<AcomodacaoIndisponivelException>(
            () => Reservation.Create(AccommodationId, "guest-1", CheckIn, CheckIn.AddDays(2), 2, facts));
    }

    [Fact]
    public void Create_with_guests_above_capacity_throws_CapacidadeExcedida()
    {
        Assert.Throws<CapacidadeExcedidaException>(
            () => Reservation.Create(AccommodationId, "guest-1", CheckIn, CheckIn.AddDays(2), 5, ValidFacts));
    }

    [Fact]
    public void Create_with_unavailable_period_throws_PeriodoIndisponivel()
    {
        var facts = ValidFacts with { AvailableForPeriod = false };

        Assert.Throws<PeriodoIndisponivelException>(
            () => Reservation.Create(AccommodationId, "guest-1", CheckIn, CheckIn.AddDays(2), 2, facts));
    }

    [Fact]
    public void Create_with_invalid_period_throws_PeriodoInvalido_before_facts_checks()
    {
        Assert.Throws<PeriodoInvalidoException>(
            () => Reservation.Create(AccommodationId, "guest-1", CheckIn, CheckIn, 2, ValidFacts));
    }

    [Fact]
    public void Create_with_non_positive_guests_throws_QuantidadeHospedesInvalida_before_facts_checks()
    {
        Assert.Throws<QuantidadeHospedesInvalidaException>(
            () => Reservation.Create(AccommodationId, "guest-1", CheckIn, CheckIn.AddDays(2), 0, ValidFacts));
    }

    [Fact]
    public void Create_with_inactive_and_over_capacity_throws_AcomodacaoIndisponivel_first()
    {
        var facts = ValidFacts with { Active = false };

        Assert.Throws<AcomodacaoIndisponivelException>(
            () => Reservation.Create(AccommodationId, "guest-1", CheckIn, CheckIn.AddDays(2), 5, facts));
    }

    [Fact]
    public void Create_with_over_capacity_and_unavailable_period_throws_CapacidadeExcedida_first()
    {
        var facts = ValidFacts with { AvailableForPeriod = false };

        Assert.Throws<CapacidadeExcedidaException>(
            () => Reservation.Create(AccommodationId, "guest-1", CheckIn, CheckIn.AddDays(2), 5, facts));
    }

    [Fact]
    public void Create_with_all_rejections_active_throws_AcomodacaoIndisponivel_first()
    {
        var facts = ValidFacts with { Active = false, AvailableForPeriod = false };

        Assert.Throws<AcomodacaoIndisponivelException>(
            () => Reservation.Create(AccommodationId, "guest-1", CheckIn, CheckIn.AddDays(2), 5, facts));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void Create_with_valid_facts_returns_solicitada_reservation_with_frozen_pricing(int nights)
    {
        var reservation = Reservation.Create(
            AccommodationId, "guest-ref", CheckIn, CheckIn.AddDays(nights), 2, ValidFacts);

        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal(AccommodationId, reservation.AccommodationId);
        Assert.Equal("guest-ref", reservation.GuestReference);
        Assert.Equal(CheckIn, reservation.CheckIn);
        Assert.Equal(CheckIn.AddDays(nights), reservation.CheckOut);
        Assert.Equal(2, reservation.GuestsCount);
        Assert.Equal(ReservationStatus.Solicitada, reservation.Status);
        Assert.Equal(350.00m, reservation.PricePerNight);
        Assert.Equal(350.00m * nights, reservation.TotalAmount);
        Assert.Equal("BRL", reservation.Currency);
    }

    [Fact]
    public void Create_with_fractional_price_computes_exact_total()
    {
        var facts = ValidFacts with { PricePerNight = 275.50m };

        var reservation = Reservation.Create(
            AccommodationId, "guest-ref", CheckIn, CheckIn.AddDays(3), 2, facts);

        Assert.Equal(275.50m, reservation.PricePerNight);
        Assert.Equal(826.50m, reservation.TotalAmount);
    }

    [Fact]
    public void Create_with_guests_equal_to_capacity_succeeds()
    {
        var reservation = Reservation.Create(
            AccommodationId, "guest-ref", CheckIn, CheckIn.AddDays(3), 4, ValidFacts);

        Assert.Equal(ReservationStatus.Solicitada, reservation.Status);
        Assert.Equal(1050.00m, reservation.TotalAmount);
    }

    [Fact]
    public void Create_returns_saga_in_PaymentPending_with_correlation_equal_to_reservation_id()
    {
        var reservation = Reservation.Create(
            AccommodationId, "guest-ref", CheckIn, CheckIn.AddDays(2), 2, ValidFacts);

        Assert.NotNull(reservation.Saga);
        Assert.NotEqual(Guid.Empty, reservation.Saga.Id);
        Assert.Equal(reservation.Id, reservation.Saga.ReservationId);
        Assert.Equal(reservation.Id, reservation.Saga.CorrelationId);
        Assert.Equal(SagaState.PaymentPending, reservation.Saga.State);
    }
}
