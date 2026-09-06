using System.Diagnostics;
using PZServerLauncher.Runtime.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class GracefulServerProcessStopperTests
{
    [Fact]
    public async Task TryStopAsync_SendsSaveThenQuitAndWaitsForProcessExit()
    {
        using var process = StartProcess(
            "-NoProfile -Command \"$first=[Console]::In.ReadLine(); $second=[Console]::In.ReadLine(); [Console]::Out.WriteLine($first + '|' + $second)\"");
        using var commandGate = new SemaphoreSlim(1, 1);

        try
        {
            var result = await GracefulServerProcessStopper.TryStopAsync(
                process,
                commandGate,
                TimeSpan.FromSeconds(5),
                CancellationToken.None);

            Assert.Equal(GracefulProcessStopResult.ExitedGracefully, result);
            var output = await process.StandardOutput.ReadToEndAsync();
            Assert.Contains("save|quit", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await StopProcessIfRunningAsync(process);
        }
    }

    [Fact]
    public async Task TryStopAsync_ReturnsTimedOutWhenProcessDoesNotExit()
    {
        using var process = StartProcess("-NoProfile -Command \"Start-Sleep -Seconds 30\"");
        using var commandGate = new SemaphoreSlim(1, 1);

        try
        {
            var result = await GracefulServerProcessStopper.TryStopAsync(
                process,
                commandGate,
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None);

            Assert.Equal(GracefulProcessStopResult.TimedOut, result);
        }
        finally
        {
            await StopProcessIfRunningAsync(process);
        }
    }

    private static Process StartProcess(string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.Start();
        return process;
    }

    private static async Task StopProcessIfRunningAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }
}
