using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.Booking.Application;

// CQRS nativo (sem MediatR) — adaptado de
// dotnet-architecture/examples/cqrs.md para Minimal API. O lado de comando
// nasceu em F01; o lado de query entrou com a primeira consulta (F02).
public interface ICommand<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQuery<TResponse>;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);

    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);
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

    public async Task<TResponse> SendAsync<TResponse>(
        IQuery<TResponse> query, CancellationToken cancellationToken)
    {
        var queryType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResponse));

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["query.type"] = queryType.Name
        });

        logger.LogDebug("Executing query {QueryType}", queryType.Name);

        var handler = serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResponse>, TResponse>.HandleAsync));

        var task = (Task<TResponse>)method!.Invoke(handler, [query, cancellationToken])!;
        return await task.ConfigureAwait(false);
    }
}
