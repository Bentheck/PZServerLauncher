using Microsoft.Win32;

namespace PZServerLauncher.Host.Services;

public sealed class HostStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PZServerLauncher.Host";

    public Task SyncAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (!enabled)
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            return Task.CompletedTask;
        }

        var hostPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not resolve the current host executable path.");
        runKey.SetValue(ValueName, $"\"{hostPath}\"");
        return Task.CompletedTask;
    }
}
