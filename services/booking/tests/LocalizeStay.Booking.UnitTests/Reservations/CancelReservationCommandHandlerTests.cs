using AwesomeAssertions;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LocalizeStay.Booking.UnitTests.Reservations;

// RF-02/RF-03: sucesso cancela com motivo exato, persiste + publica; não
// correlacionável e terminal não têm efeitos; falha do publisher preserva
// estado/retorno sem relançar.
public sealed class CancelReservationCommandHandlerTests
{
    private const string RejectionReason = "Pagamento rejeitado pela simulação de Payment.";

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

    private readonly Mock<IReservationCancelledPublisher> _publisher = new();

    private CancelReservationCommandHandler CreateHandler() => new(
        _repository.Object,
        _publisher.Object,
        NullLogger<CancelReservationCommandHandler>.Instance);

    private static Reservation CreateSolicitada() =>
        Reservation.Create(AccommodationId, "guest-unit", CheckIn, CheckOut, 2, ValidFacts);

    [Fact]
    public async Task HandleAsync_with_pending_reservation_cancels_with_reason_updates_and_publishes_once()
    {
        var reservation = CreateSolicitada();
        var correlationId = reservation.Saga.CorrelationId;
        _repository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var outcome = await CreateHandler().HandleAsync(
            new CancelReservationCommand(correlationId, RejectionReason), CancellationToken.None);

        outcome.Should().Be(CancelReservationOutcome.Cancelled);
        reservation.Status.Should().Be(ReservationStatus.Cancelada);
        reservation.Saga.State.Should().Be(SagaState.Rejected);
        reservation.Saga.CancellationReason.Should().Be(RejectionReason);
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
            new CancelReservationCommand(correlationId, RejectionReason), CancellationToken.None);

        outcome.Should().Be(CancelReservationOutcome.NotCorrelatable);
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
            reservation.Cancel(RejectionReason);
        }

        var correlationId = reservation.Saga.CorrelationId;
        _repository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var outcome = await CreateHandler().HandleAsync(
            new CancelReservationCommand(correlationId, RejectionReason), CancellationToken.None);

        outcome.Should().Be(CancelReservationOutcome.AlreadyTerminal);
        reservation.Status.Should().Be(terminal);
        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(
            p => p.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_when_publisher_fails_returns_Cancelled_and_preserves_terminal_state()
    {
        var reservation = CreateSolicitada();
        var correlationId = reservation.Saga.CorrelationId;
        _repository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _publisher
            .Setup(p => p.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker indisponível"));

        Func<Task<CancelReservationOutcome>> act = () => CreateHandler().HandleAsync(
            new CancelReservationCommand(correlationId, RejectionReason), CancellationToken.None);

        var outcome = await act();
        outcome.Should().Be(CancelReservationOutcome.Cancelled);
        reservation.Status.Should().Be(ReservationStatus.Cancelada);
        reservation.Saga.State.Should().Be(SagaState.Rejected);
        reservation.Saga.CancellationReason.Should().Be(RejectionReason);
        _repository.Verify(
            r => r.UpdateAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
    }
}
