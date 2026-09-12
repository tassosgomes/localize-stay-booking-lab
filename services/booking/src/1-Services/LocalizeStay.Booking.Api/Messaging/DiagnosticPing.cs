namespace LocalizeStay.Booking.Api.Messaging;

/// <summary>
/// Mensagem de diagnóstico (V-03): prova a topologia e a correlação ponta a ponta
/// sem carregar semântica de negócio. Os eventos reais da saga (PaymentRequested etc.,
/// ADR-002) chegam nos PRDs de negócio e reaproveitam esta infraestrutura.
/// </summary>
public sealed record DiagnosticPing(
    string PingId,
    string CorrelationId,
    string CausationId,
    DateTimeOffset SentAt);
