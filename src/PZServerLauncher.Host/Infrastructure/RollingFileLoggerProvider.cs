using Microsoft.Extensions.Logging;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Host.Infrastructure;

public sealed class RollingFileLoggerProvider(PersistentLogService persistentLogService) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        new RollingFileLogger(categoryName, persistentLogService);

    public void Dispose()
    {
    }

    private sealed class RollingFileLogger(string categoryName, PersistentLogService persistentLogService) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            var levelCode = logLevel switch
            {
                LogLevel.Trace => "TRC",
                LogLevel.Debug => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => "LOG",
            };

            var formatted = $"{levelCode} {categoryName}: {message}";
            if (exception is not null)
            {
                formatted = $"{formatted}{Environment.NewLine}{exception}";
            }

            try
            {
                persistentLogService.WriteHostLine(formatted);
            }
            catch
            {
                // Never let file logging break the host process.
            }
        }
    }
}
