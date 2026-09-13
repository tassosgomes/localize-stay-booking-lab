using AwesomeAssertions;
using LocalizeStay.Booking.Api.Messaging;
using LocalizeStay.Booking.Application;
using LocalizeStay.Booking.Application.Reservations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rmq.CloudEvents.Consuming;
using Xunit;

namespace LocalizeStay.Booking.UnitTests.Messaging;

// Adapter fino: correlação válida despacha exatamente o command esperado;
// nula/vazia/não-GUID nunca despacha (DP-03).
public sealed class PaymentAuthorizedConsumerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new(MockBehavior.Strict);

    private PaymentAuthorizedConsumer CreateConsumer() => new(
        _dispatcher.Object, NullLogger<PaymentAuthorizedConsumer>.Instance);

    private static MessageContext Context(string queue = "booking.payment_authorized") =>
        new()
        {
            EventId = Guid.NewGuid().ToString(),
            Source = new Uri("/payment", UriKind.Relative),
            EventType = "com.localizestay.payment.payment_authorized.v1",
            Timestamp = DateTimeOffset.UtcNow,
            QueueName = queue
        };

    [Fact]
    public async Task HandleAsync_with_valid_correlationId_dispatches_ConfirmReservationCommand_once()
    {
        var correlationId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(
                It.Is<ConfirmReservationCommand>(c => c.CorrelationId == correlationId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConfirmReservationOutcome.Confirmed);

        await CreateConsumer().HandleAsync(
            new PaymentAuthorizedMessage(correlationId.ToString(), DateTimeOffset.UtcNow),
            Context(),
            CancellationToken.None);

        _dispatcher.Verify(
            d => d.SendAsync(
                It.IsAny<ConfirmReservationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    public async Task HandleAsync_with_missing_or_invalid_correlationId_does_not_dispatch(string? correlationId)
    {
        await CreateConsumer().HandleAsync(
            new PaymentAuthorizedMessage(correlationId, DateTimeOffset.UtcNow),
            Context(),
            CancellationToken.None);

        _dispatcher.Verify(
            d => d.SendAsync(
                It.IsAny<ICommand<ConfirmReservationOutcome>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
