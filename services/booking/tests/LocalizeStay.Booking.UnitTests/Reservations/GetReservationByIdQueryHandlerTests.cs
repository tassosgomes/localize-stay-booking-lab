using FluentValidation;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using LocalizeStay.Booking.Domain.Reservations.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LocalizeStay.Booking.UnitTests.Reservations;

// Ordem normativa do handler: validação de formato ANTES de qualquer acesso
// ao repositório; null do repositório vira ReservationNotFoundException; uma
// Reservation encontrada é devolvida sem transformação (a projeção para o
// contrato vive na camada de API).
public sealed class GetReservationByIdQueryHandlerTests
{
    private readonly Mock<IReservationRepository> _repository = new();

    private GetReservationByIdQueryHandler CreateHandler() => new(
        new GetReservationByIdQueryValidator(),
        _repository.Object,
        NullLogger<GetReservationByIdQueryHandler>.Instance);

    private static Reservation CreateValidReservation()
    {
        var facts = new AvailabilityFacts(
            Active: true,
            MaxGuests: 4,
            AvailableForPeriod: true,
            PricePerNight: 350.00m,
            Currency: "BRL");

        return Reservation.Create(
            Guid.NewGuid(),
            "guest-unit",
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 13),
            2,
            facts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-e-um-uuid")]
    public async Task HandleAsync_with_invalid_reservationId_throws_ValidationException_without_calling_repository(
        string reservationId)
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(new GetReservationByIdQuery(reservationId), CancellationToken.None));

        _repository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_when_repository_returns_null_throws_ReservationNotFoundException()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reservation?)null);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => handler.HandleAsync(new GetReservationByIdQuery(id.ToString()), CancellationToken.None));

        _repository.Verify(
            repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_when_repository_returns_reservation_returns_same_instance()
    {
        var reservation = CreateValidReservation();
        _repository
            .Setup(repository => repository.GetByIdAsync(reservation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            new GetReservationByIdQuery(reservation.Id.ToString()), CancellationToken.None);

        Assert.Same(reservation, result);
    }
}
