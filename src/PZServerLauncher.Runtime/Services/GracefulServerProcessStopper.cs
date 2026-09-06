using System.Diagnostics;

namespace PZServerLauncher.Runtime.Services;

internal enum GracefulProcessStopResult
{
    AlreadyExited,
    ExitedGracefully,
    CommandFailed,
    TimedOut,
}

internal static class GracefulServerProcessStopper
{
    public static async Task<GracefulProcessStopResult> TryStopAsync(
        Process process,
        SemaphoreSlim commandGate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return GracefulProcessStopResult.AlreadyExited;
        }

        var gateAcquired = false;
        try
        {
            await commandGate.WaitAsync(cancellationToken);
            gateAcquired = true;

            if (process.HasExited)
            {
                return GracefulProcessStopResult.AlreadyExited;
            }

            await process.StandardInput.WriteLineAsync("save".AsMemory(), cancellationToken);
            await process.StandardInput.WriteLineAsync("quit".AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return GracefulProcessStopResult.CommandFailed;
        }
        finally
        {
            if (gateAcquired)
            {
                try
                {
                    commandGate.Release();
                }
                catch (ObjectDisposedException)
                {
                    // The process-exit callback can remove the per-profile gate concurrently.
                }
            }
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            return GracefulProcessStopResult.ExitedGracefully;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GracefulProcessStopResult.TimedOut;
        }
    }
}
