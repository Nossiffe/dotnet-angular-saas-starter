using Mediator;
using Microsoft.Extensions.Logging;

namespace Saas.Application.Common.Behaviors;

public sealed partial class LoggingBehavior<TMessage, TResponse>(
    ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        // Pas de journalisation des exceptions ici : une erreur de validation devient un 400, et une
        // vraie erreur est journalisée une seule fois par le gestionnaire d'exceptions de l'API.
        var name = typeof(TMessage).Name;
        LogHandling(logger, name);
        var response = await next(message, cancellationToken);
        LogHandled(logger, name);
        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {MessageType}")]
    private static partial void LogHandling(ILogger logger, string messageType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {MessageType}")]
    private static partial void LogHandled(ILogger logger, string messageType);
}
