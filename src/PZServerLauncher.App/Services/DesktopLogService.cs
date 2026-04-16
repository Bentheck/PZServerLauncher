using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.Services;

public sealed class DesktopLogService
{
    private readonly RollingFileLogWriter _writer;

    public DesktopLogService()
    {
        var rootDirectory = LauncherStorageRootResolver.Resolve();
        _writer = new RollingFileLogWriter(Path.Combine(rootDirectory, "logs", "app.log"));
    }

    public void Info(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _writer.WriteLine($"INF {message}");
    }

    public void Error(string message, Exception? exception = null)
    {
        var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "Desktop error." : message;
        if (exception is null)
        {
            _writer.WriteLine($"ERR {normalizedMessage}");
            return;
        }

        _writer.WriteLine($"ERR {normalizedMessage}{Environment.NewLine}{exception}");
    }
}
