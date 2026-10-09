using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Saas.IntegrationTests.Platform;

/// <summary>
/// Capture les logs de l'API (Serilog les transmet aux providers enregistrés), avec la trace de la
/// requête en cours pour séparer les logs d'une requête de ceux des tâches de fond.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
        // Rien à libérer.
    }

    public sealed record LogEntry(string Category, LogLevel Level, string Message, ActivityTraceId? TraceId);

    private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), Activity.Current?.TraceId));
        }
    }
}
