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

    private static readonly Action<ILogger, string, Exception?> _logError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "Error"), "Error handling {MessageType}");

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var name = typeof(TMessage).Name;
        _logHandling(logger, name, null);
        try
        {
            var response = await next(message, cancellationToken);
            _logHandled(logger, name, null);
            return response;
        }
        catch (Exception ex)
        {
            _logError(logger, name, ex);
            throw;
        }
    }
}
