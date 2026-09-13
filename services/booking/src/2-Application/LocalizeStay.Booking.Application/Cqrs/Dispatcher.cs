using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Booking.Application;

// CQRS nativo (sem MediatR), lado de comando apenas — adaptado de
// dotnet-architecture/examples/cqrs.md para Minimal API. Queries entram quando
// a primeira consulta existir (F02), não antes.
public interface ICommand<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);
}

public sealed class Dispatcher(IServiceProvider serviceProvider, ILogger<Dispatcher> logger) : IDispatcher
{
    public async Task<TResponse> SendAsync<TResponse>(
        ICommand<TResponse> command, CancellationToken cancellationToken)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResponse));

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["command.type"] = commandType.Name
        });

        logger.LogDebug("Executing command {CommandType}", commandType.Name);

        var handler = serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResponse>, TResponse>.HandleAsync));

        var task = (Task<TResponse>)method!.Invoke(handler, [command, cancellationToken])!;
        return await task.ConfigureAwait(false);
    }
}
