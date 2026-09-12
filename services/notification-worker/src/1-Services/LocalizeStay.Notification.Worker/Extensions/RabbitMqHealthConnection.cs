using RabbitMQ.Client;

namespace LocalizeStay.Notification.Worker.Extensions;

/// <summary>
/// Conexão AMQP compartilhada e preguiçosa para o health check de RabbitMQ.
/// O factory do <c>AddRabbitMQ</c> é invocado a cada probe e o check não descarta
/// a conexão devolvida — por isso o holder reaproveita uma única conexão aberta
/// e a recria quando ela cai, em vez de abrir uma nova a cada chamada.
/// </summary>
internal sealed class RabbitMqHealthConnection : IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private bool _disposed;

    public RabbitMqHealthConnection(RabbitMqSettings settings)
    {
        _factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = MessagingExtensions.ExpectedVirtualHost,
            ClientProvidedName = "notification-worker-health",
            AutomaticRecoveryEnabled = true
        };
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }

            _connection = await _factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
