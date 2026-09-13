using LocalizeStay.Booking.Domain.Reservations.Exceptions;

namespace LocalizeStay.Booking.Domain.Reservations;

public sealed class Reservation
{
    private Reservation()
    {
    }

    private Reservation(
        Guid id,
        Guid accommodationId,
        string guestReference,
        DateOnly checkIn,
        DateOnly checkOut,
        int guestsCount,
        ReservationStatus status,
        decimal pricePerNight,
        decimal totalAmount,
        string currency,
        DateTime createdAt)
    {
        Id = id;
        AccommodationId = accommodationId;
        GuestReference = guestReference;
        CheckIn = checkIn;
        CheckOut = checkOut;
        GuestsCount = guestsCount;
        Status = status;
        PricePerNight = pricePerNight;
        TotalAmount = totalAmount;
        Currency = currency;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid AccommodationId { get; private set; }

    public string GuestReference { get; private set; } = string.Empty;

    public DateOnly CheckIn { get; private set; }

    public DateOnly CheckOut { get; private set; }

    public int GuestsCount { get; private set; }

    public ReservationStatus Status { get; private set; }

    public decimal PricePerNight { get; private set; }

    public decimal TotalAmount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    // Instante UTC em que a Reservation entrou no estado terminal atual
    // (EN-01/ADR-005): nulo enquanto solicitada, obrigatório após a transição.
    public DateTime? TerminalTransitionAt { get; private set; }

    public ReservationSaga Saga { get; private set; } = null!;

    public static Reservation Create(
        Guid accommodationId,
        string guestReference,
        DateOnly checkIn,
        DateOnly checkOut,
        int guestsCount,
        AvailabilityFacts facts)
    {
        EnsurePeriodIsValid(checkIn, checkOut);
        EnsureGuestsCountIsValid(guestsCount);

        if (!facts.Active)
        {
            throw new AcomodacaoIndisponivelException();
        }

        if (guestsCount > facts.MaxGuests)
        {
            throw new CapacidadeExcedidaException();
        }

        if (!facts.AvailableForPeriod)
        {
            throw new PeriodoIndisponivelException();
        }

        var id = Guid.NewGuid();
        var nights = checkOut.DayNumber - checkIn.DayNumber;

        return new Reservation(
            id,
            accommodationId,
            guestReference,
            checkIn,
            checkOut,
            guestsCount,
            ReservationStatus.Solicitada,
            facts.PricePerNight,
            facts.PricePerNight * nights,
            facts.Currency,
            DateTime.UtcNow)
        {
            Saga = new ReservationSaga(Guid.NewGuid(), id, id, SagaState.PaymentPending, DateTime.UtcNow)
        };
    }

    public static void EnsurePeriodIsValid(DateOnly checkIn, DateOnly checkOut)
    {
        if (checkOut <= checkIn)
        {
            throw new PeriodoInvalidoException();
        }
    }

    public static void EnsureGuestsCountIsValid(int guestsCount)
    {
        if (guestsCount <= 0)
        {
            throw new QuantidadeHospedesInvalidaException();
        }
    }

    public void Confirm(DateTime terminalTransitionAt)
    {
        EnsurePending();
        TerminalTransitionAt = NormalizeToUtc(terminalTransitionAt);
        Status = ReservationStatus.Confirmada;
        Saga.MarkAuthorized();
    }

    public void Cancel(string cancellationReason, DateTime terminalTransitionAt)
    {
        EnsurePending();
        TerminalTransitionAt = NormalizeToUtc(terminalTransitionAt);
        Status = ReservationStatus.Cancelada;
        Saga.MarkRejected(cancellationReason);
    }

    // O instante terminal é sempre armazenado em UTC (ADR-005): um instante sem
    // Kind é assumido como UTC; qualquer outro Kind é convertido.
    private static DateTime NormalizeToUtc(DateTime instant) =>
        instant.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(instant, DateTimeKind.Utc)
            : instant.ToUniversalTime();

    private void EnsurePending()
    {
        if (Status != ReservationStatus.Solicitada)
        {
            throw new InvalidOperationException(
                $"Reservation {Id} não pode transicionar a partir do estado {Status} (RN-11).");
        }
    }
}
