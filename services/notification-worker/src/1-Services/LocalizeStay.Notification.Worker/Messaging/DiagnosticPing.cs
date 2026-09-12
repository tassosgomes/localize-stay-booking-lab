namespace LocalizeStay.Notification.Worker.Messaging;

/// <summary>
/// Mensagem de diagnóstico (V-03). Cópia intencional do contrato de Booking:
/// sem pacote compartilhado nesta fase (techspec, Desvios) — o contrato formal
/// nasce no AsyncAPI da task 8.0. Os eventos reais da saga (ADR-002) chegam depois.
/// </summary>
public sealed record DiagnosticPing(
    string PingId,
    string CorrelationId,
    string CausationId,
    DateTimeOffset SentAt);
