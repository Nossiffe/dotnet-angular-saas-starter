using Mediator;
using Microsoft.Extensions.Logging;

namespace Saas.Application.Common.Behaviors;

public sealed class LoggingBehavior<TMessage, TResponse>(
    ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    private static readonly Action<ILogger, string, Exception?> _logHandling =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, "Handling"), "Handling {MessageType}");

    private static readonly Action<ILogger, string, Exception?> _logHandled =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "Handled"), "Handled {MessageType}");

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        // Pas de journalisation des exceptions ici : une erreur de validation devient un 400, et une
        // vraie erreur est journalisée une seule fois par le gestionnaire d'exceptions de l'API.
        var name = typeof(TMessage).Name;
        _logHandling(logger, name, null);
        var response = await next(message, cancellationToken);
        _logHandled(logger, name, null);
        return response;
    }
}
