using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Messaging.IntegrationTests;

/// <summary>
/// Captura o log renderizado em memória para assert de correlação
/// (<c>correlationId</c>/<c>causationId</c>) sem acoplar a sinks externos.
/// </summary>
public sealed class CaptureLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentBag<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages;

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, _messages);

    public void Dispose()
    {
    }

    private sealed class CaptureLogger(string categoryName, ConcurrentBag<string> messages) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            _ = state;
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _ = eventId;
            messages.Add($"{logLevel}:{categoryName}:{formatter(state, exception)}");
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
