using FluentValidation;
using LocalizeStay.Booking.Application.Reservations;
using LocalizeStay.Booking.Domain.Reservations;
using LocalizeStay.Booking.Domain.Reservations.Exceptions;
using LocalizeStay.Booking.Infra.Catalog;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LocalizeStay.Booking.UnitTests.Reservations;

// Ordem normativa do handler (techspec): período/hóspedes ANTES de Catalog;
// nenhuma rejeição persiste (AddAsync) nem publica (PublishAsync).
public sealed class RequestReservationCommandHandlerTests
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

    private readonly Mock<ICatalogAvailabilityClient> _catalogClient = new();

    private readonly Mock<IReservationRepository> _repository = new();

    private readonly Mock<IReservationRequestedPublisher> _publisher = new();

    private readonly Mock<IPaymentRequestedPublisher> _paymentRequestedPublisher = new();

    private RequestReservationCommandHandler CreateHandler() => new(
        new RequestReservationCommandValidator(),
        _catalogClient.Object,
        _repository.Object,
        _publisher.Object,
        _paymentRequestedPublisher.Object,
        NullLogger<RequestReservationCommandHandler>.Instance);

    private static RequestReservationCommand ValidCommand(int guestsCount = 2) =>
        new(AccommodationId, "guest-unit", CheckIn, CheckOut, guestsCount);

    private void SetupCatalogReturning(AvailabilityFacts? facts) =>
        _catalogClient
            .Setup(client => client.CheckAvailabilityAsync(
                AccommodationId, CheckIn, CheckOut, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(facts);

    [Fact]
    public async Task HandleAsync_with_valid_command_creates_persists_publishes_and_returns_solicitada()
    {
        SetupCatalogReturning(ValidFacts);
        var handler = CreateHandler();

        var reservation = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal(AccommodationId, reservation.AccommodationId);
        Assert.Equal(ReservationStatus.Solicitada, reservation.Status);
        Assert.Equal(350.00m, reservation.PricePerNight);
        Assert.Equal(1050.00m, reservation.TotalAmount);
        Assert.Equal("BRL", reservation.Currency);

        _repository.Verify(
            repository => repository.AddAsync(
                It.Is<Reservation>(r => r.Id == reservation.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        _publisher.Verify(
            publisher => publisher.PublishAsync(
                It.Is<Reservation>(r => r.Id == reservation.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_with_checkOut_not_after_checkIn_throws_PeriodoInvalido_without_calling_catalog()
    {
        var handler = CreateHandler();
        var command = ValidCommand() with { CheckIn = CheckIn, CheckOut = CheckIn };

        await Assert.ThrowsAsync<PeriodoInvalidoException>(
            () => handler.HandleAsync(command, CancellationToken.None));

        _catalogClient.Verify(
            client => client.CheckAvailabilityAsync(
                It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisher.Verify(
            publisher => publisher.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task HandleAsync_with_non_positive_guests_throws_QuantidadeHospedesInvalida_without_calling_catalog(
        int guestsCount)
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<QuantidadeHospedesInvalidaException>(
            () => handler.HandleAsync(ValidCommand(guestsCount), CancellationToken.None));

        _catalogClient.Verify(
            client => client.CheckAvailabilityAsync(
                It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisher.Verify(
            publisher => publisher.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_with_malformed_command_throws_ValidationException_before_any_dependency()
    {
        var handler = CreateHandler();
        var command = ValidCommand() with { AccommodationId = Guid.Empty };

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(command, CancellationToken.None));

        _catalogClient.Verify(
            client => client.CheckAvailabilityAsync(
                It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisher.Verify(
            publisher => publisher.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_when_catalog_returns_null_throws_AcomodacaoIndisponivel()
    {
        SetupCatalogReturning(null);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<AcomodacaoIndisponivelException>(
            () => handler.HandleAsync(ValidCommand(), CancellationToken.None));

        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisher.Verify(
            publisher => publisher.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_when_catalog_fails_propagates_CatalogUnavailableException_without_side_effects()
    {
        _catalogClient
            .Setup(client => client.CheckAvailabilityAsync(
                AccommodationId, CheckIn, CheckOut, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CatalogUnavailableException("Falha ao consultar disponibilidade."));

        var handler = CreateHandler();

        await Assert.ThrowsAsync<CatalogUnavailableException>(
            () => handler.HandleAsync(ValidCommand(), CancellationToken.None));

        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisher.Verify(
            publisher => publisher.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_with_inactive_accommodation_throws_AcomodacaoIndisponivel()
    {
        SetupCatalogReturning(ValidFacts with { Active = false });
        var handler = CreateHandler();

        await Assert.ThrowsAsync<AcomodacaoIndisponivelException>(
            () => handler.HandleAsync(ValidCommand(), CancellationToken.None));

        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisher.Verify(
            publisher => publisher.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_with_guests_above_capacity_throws_CapacidadeExcedida()
    {
        SetupCatalogReturning(ValidFacts);
        var handler = CreateHandler();

        await Assert.ThrowsAsync<CapacidadeExcedidaException>(
            () => handler.HandleAsync(ValidCommand(5), CancellationToken.None));

        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisher.Verify(
            publisher => publisher.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_with_unavailable_period_throws_PeriodoIndisponivel()
    {
        SetupCatalogReturning(ValidFacts with { AvailableForPeriod = false });
        var handler = CreateHandler();

        await Assert.ThrowsAsync<PeriodoIndisponivelException>(
            () => handler.HandleAsync(ValidCommand(), CancellationToken.None));

        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisher.Verify(
            publisher => publisher.PublishAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_when_publisher_fails_returns_reservation_anyway_best_effort()
    {
        SetupCatalogReturning(ValidFacts);
        _publisher
            .Setup(publisher => publisher.PublishAsync(
                It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker fora do ar"));
        var handler = CreateHandler();

        var reservation = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.Equal(ReservationStatus.Solicitada, reservation.Status);
        _repository.Verify(
            repository => repository.AddAsync(
                It.Is<Reservation>(r => r.Id == reservation.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        _publisher.Verify(
            publisher => publisher.PublishAsync(
                It.Is<Reservation>(r => r.Id == reservation.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_when_payment_requested_publisher_succeeds_marks_saga_and_updates_once()
    {
        SetupCatalogReturning(ValidFacts);
        var handler = CreateHandler();

        var reservation = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.NotNull(reservation.Saga.PaymentRequestSentAt);
        _paymentRequestedPublisher.Verify(
            publisher => publisher.PublishAsync(
                It.Is<Reservation>(r => r.Id == reservation.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        _repository.Verify(
            repository => repository.UpdateAsync(
                It.Is<Reservation>(r => r.Id == reservation.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_when_payment_requested_publisher_fails_does_not_mark_saga_nor_update_nor_propagate()
    {
        SetupCatalogReturning(ValidFacts);
        _paymentRequestedPublisher
            .Setup(publisher => publisher.PublishAsync(
                It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker fora do ar"));
        var handler = CreateHandler();

        var reservation = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.Equal(ReservationStatus.Solicitada, reservation.Status);
        Assert.Null(reservation.Saga.PaymentRequestSentAt);
        _repository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
