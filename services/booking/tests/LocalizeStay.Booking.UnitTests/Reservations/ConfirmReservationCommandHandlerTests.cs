using AwesomeAssertions;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LocalizeStay.Booking.UnitTests.Reservations;

// RF-01/RF-03: sucesso persiste + publica; não correlacionável e terminal não
// têm efeitos; falha do publisher preserva estado/retorno sem relançar.
public sealed class ConfirmReservationCommandHandlerTests
{
    private static readonly Guid AccommodationId = Guid.NewGuid();

    private static readonly DateOnly CheckIn = new(2026, 10, 10);

    private static readonly DateOnly CheckOut = new(2026, 10, 13);

    private static readonly AvailabilityFacts ValidFacts = new(
        Active: true,
        MaxGuests: 4,
        AvailableForPeriod: true,
        PricePerNight: 350.00m,
        Currency: "BRL");

    private readonly Mock<IReservationRepository> _repository = new();

    private readonly Mock<IReservationConfirmedPublisher> _publisher = new();

    private ConfirmReservationCommandHandler CreateHandler() => new(
        _repository.Object,
        _publisher.Object,
        NullLogger<ConfirmReservationCommandHandler>.Instance);

    private static Reservation CreateSolicitada() =>
        Reservation.Create(AccommodationId, "guest-unit", CheckIn, CheckOut, 2, ValidFacts);

    [Fact]
    public async Task HandleAsync_with_pending_reservation_confirms_updates_and_publishes_once()
    {
        var reservation = CreateSolicitada();
        var correlationId = reservation.Saga.CorrelationId;
        _repository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var outcome = await CreateHandler().HandleAsync(
            new ConfirmReservationCommand(correlationId), CancellationToken.None);

        outcome.Should().Be(ConfirmReservationOutcome.Confirmed);
        reservation.Status.Should().Be(ReservationStatus.Confirmada);
        reservation.Saga.State.Should().Be(SagaState.Authorized);
        _repository.Verify(
            r => r.UpdateAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(
            p => p.PublishAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_with_unknown_correlation_returns_NotCorrelatable_without_effects()
    {
        var correlationId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reservation?)null);

        var outcome = await CreateHandler().HandleAsync(
            new ConfirmReservationCommand(correlationId), CancellationToken.None);

        outcome.Should().Be(ConfirmReservationOutcome.NotCorrelatable);
        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(
            p => p.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ReservationStatus.Confirmada)]
    [InlineData(ReservationStatus.Cancelada)]
    public async Task HandleAsync_with_terminal_reservation_returns_AlreadyTerminal_without_effects(
        ReservationStatus terminal)
    {
        var reservation = CreateSolicitada();
        if (terminal == ReservationStatus.Confirmada)
        {
            reservation.Confirm();
        }
        else
        {
            reservation.Cancel("Pagamento rejeitado pela simulação de Payment.");
        }

        var correlationId = reservation.Saga.CorrelationId;
        _repository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var outcome = await CreateHandler().HandleAsync(
            new ConfirmReservationCommand(correlationId), CancellationToken.None);

        outcome.Should().Be(ConfirmReservationOutcome.AlreadyTerminal);
        reservation.Status.Should().Be(terminal);
        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(
            p => p.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_when_publisher_fails_returns_Confirmed_and_preserves_terminal_state()
    {
        var reservation = CreateSolicitada();
        var correlationId = reservation.Saga.CorrelationId;
        _repository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _publisher
            .Setup(p => p.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker indisponível"));

        Func<Task<ConfirmReservationOutcome>> act = () => CreateHandler().HandleAsync(
            new ConfirmReservationCommand(correlationId), CancellationToken.None);

        var outcome = await act();
        outcome.Should().Be(ConfirmReservationOutcome.Confirmed);
        reservation.Status.Should().Be(ReservationStatus.Confirmada);
        reservation.Saga.State.Should().Be(SagaState.Authorized);
        _repository.Verify(
            r => r.UpdateAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
    }
}
