using AwesomeAssertions;
using LocalizeStay.Booking.Api.Messaging;
using LocalizeStay.Booking.Application;
using LocalizeStay.Booking.Application.Reservations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rmq.CloudEvents.Consuming;
using Xunit;

namespace LocalizeStay.Booking.UnitTests.Messaging;

// Adapter fino: correlação válida despacha CancelReservationCommand com o motivo
// fixo de negócio (DP-01); nula/vazia/não-GUID nunca despacha (DP-03).
public sealed class PaymentRejectedConsumerTests
{
    private const string ExpectedReason = "Pagamento rejeitado pela simulação de Payment.";

    private readonly Mock<IDispatcher> _dispatcher = new(MockBehavior.Strict);

    private PaymentRejectedConsumer CreateConsumer() => new(
        _dispatcher.Object, NullLogger<PaymentRejectedConsumer>.Instance);

    private static MessageContext Context(string queue = "booking.payment_rejected") =>
        new()
        {
            EventId = Guid.NewGuid().ToString(),
            Source = new Uri("/payment", UriKind.Relative),
            EventType = "com.localizestay.payment.payment_rejected.v1",
            Timestamp = DateTimeOffset.UtcNow,
            QueueName = queue
        };

    [Fact]
    public async Task HandleAsync_with_valid_correlationId_dispatches_CancelReservationCommand_with_fixed_reason()
    {
        var correlationId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(
                It.Is<CancelReservationCommand>(c =>
                    c.CorrelationId == correlationId && c.Reason == ExpectedReason),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CancelReservationOutcome.Cancelled);

        await CreateConsumer().HandleAsync(
            new PaymentRejectedMessage(correlationId.ToString(), DateTimeOffset.UtcNow),
            Context(),
            CancellationToken.None);

        _dispatcher.Verify(
            d => d.SendAsync(
                It.IsAny<CancelReservationCommand>(), It.IsAny<CancellationToken>()),
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
            new PaymentRejectedMessage(correlationId, DateTimeOffset.UtcNow),
            Context(),
            CancellationToken.None);

        _dispatcher.Verify(
            d => d.SendAsync(
                It.IsAny<ICommand<CancelReservationOutcome>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
