using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Hubs;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Host.Services;

public sealed class ServerProcessSupervisor(
    AppPaths appPaths,
    ProjectZomboidServerPlanner planner,
    RuntimeStateStore runtimeStateStore,
    IHubContext<RuntimeHub> hubContext,
    IServiceScopeFactory scopeFactory,
    ILogger<ServerProcessSupervisor> logger)
{
    private readonly ConcurrentDictionary<string, Process> _processes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _commandGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _stopRequested = new(StringComparer.OrdinalIgnoreCase);

    public async Task StartAsync(PZServerLauncher.Core.Profiles.ServerProfile profile, CancellationToken cancellationToken)
    {
        if (_processes.ContainsKey(profile.ProfileId))
        {
            throw new InvalidOperationException($"Profile '{profile.ProfileId}' is already running.");
        }

        var liveOperations = runtimeStateStore.ResetLiveOperations(profile.ProfileId);
        runtimeStateStore.Update(new ServerRuntimeStatus(
            profile.ProfileId,
            ServerRuntimeState.Starting,
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            null));
        await hubContext.Clients.All.SendAsync("statusChanged", runtimeStateStore.GetOrDefault(profile.ProfileId), cancellationToken);
        await hubContext.Clients.All.SendAsync("liveOperationsChanged", liveOperations, cancellationToken);

        var runtimeDir = Path.Combine(appPaths.RuntimeProfileDirectory(profile.ProfileId), "launch");
        Directory.CreateDirectory(runtimeDir);
        var wrapperPath = Path.Combine(runtimeDir, "launch.cmd");
        var launchPlan = planner.CreateLaunchPlan(profile);
        if (!string.IsNullOrWhiteSpace(launchPlan.Notes))
        {
            runtimeStateStore.AppendLog(profile.ProfileId, launchPlan.Notes);
            await hubContext.Clients.All.SendAsync("logLine", profile.ProfileId, launchPlan.Notes, cancellationToken);
        }

        var launchCommand = planner.FormatLaunchCommand(launchPlan);
        var scriptContent = $"""
            @echo off
            setlocal
            cd /d "{launchPlan.WorkingDirectory}"
            {(launchPlan.UsesVendorBatch ? "call " : string.Empty)}{launchCommand}
            """;
        await File.WriteAllTextAsync(wrapperPath, scriptContent, cancellationToken);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{wrapperPath}\"",
                WorkingDirectory = launchPlan.WorkingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        process.OutputDataReceived += (_, args) => _ = OnOutputAsync(profile.ProfileId, args.Data);
        process.ErrorDataReceived += (_, args) => _ = OnOutputAsync(profile.ProfileId, args.Data);
        process.Exited += (_, _) => _ = OnExitedAsync(profile.ProfileId, profile.AutoRestartOnCrash);

        _stopRequested[profile.ProfileId] = false;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _processes[profile.ProfileId] = process;
        _commandGates[profile.ProfileId] = new SemaphoreSlim(1, 1);
        runtimeStateStore.Update(new ServerRuntimeStatus(
            profile.ProfileId,
            ServerRuntimeState.Running,
            process.Id,
            DateTimeOffset.UtcNow,
            null,
            null,
            runtimeStateStore.GetOrDefault(profile.ProfileId).LatestLogLine));

        await hubContext.Clients.All.SendAsync("statusChanged", runtimeStateStore.GetOrDefault(profile.ProfileId), cancellationToken);
    }

    public async Task<ProfileLiveOperationsSnapshot> SendBroadcastAsync(string profileId, string message, CancellationToken cancellationToken)
    {
        var normalizedMessage = message.Trim();
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            throw new InvalidOperationException("Broadcast message is required.");
        }

        return await SendCommandInternalAsync(
            profileId,
            $"servermsg \"{normalizedMessage.Replace("\"", "'", StringComparison.Ordinal)}\"",
            "Broadcast",
            $"Broadcast sent: {normalizedMessage}",
            cancellationToken);
    }

    public async Task<ProfileLiveOperationsSnapshot> SendCommandAsync(string profileId, string command, CancellationToken cancellationToken)
    {
        var normalizedCommand = command.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCommand))
        {
            throw new InvalidOperationException("Command text is required.");
        }

        return await SendCommandInternalAsync(
            profileId,
            normalizedCommand,
            "Console",
            $"Console command sent: {normalizedCommand}",
            cancellationToken);
    }

    private async Task<ProfileLiveOperationsSnapshot> SendCommandInternalAsync(
        string profileId,
        string normalizedCommand,
        string kind,
        string summary,
        CancellationToken cancellationToken)
    {
        if (!_processes.TryGetValue(profileId, out var process) || process.HasExited)
        {
            throw new InvalidOperationException("The selected server is not currently running.");
        }

        var gate = _commandGates.GetOrAdd(profileId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException("The selected server exited before the command could be sent.");
            }

            await process.StandardInput.WriteLineAsync(normalizedCommand);
            await process.StandardInput.FlushAsync();
        }
        finally
        {
            gate.Release();
        }

        var liveOperations = runtimeStateStore.RecordOperatorAction(profileId, kind, normalizedCommand, summary);
        await hubContext.Clients.All.SendAsync("statusChanged", runtimeStateStore.GetOrDefault(profileId), cancellationToken);
        await hubContext.Clients.All.SendAsync("liveOperationsChanged", liveOperations, cancellationToken);
        return liveOperations;
    }

    public async Task StopAsync(string profileId, CancellationToken cancellationToken)
    {
        if (!_processes.TryGetValue(profileId, out var process))
        {
            runtimeStateStore.Update(runtimeStateStore.GetOrDefault(profileId) with
            {
                State = ServerRuntimeState.Stopped,
                ProcessId = null,
                StoppedAtUtc = DateTimeOffset.UtcNow,
            });
            return;
        }

        _stopRequested[profileId] = true;
        runtimeStateStore.Update(runtimeStateStore.GetOrDefault(profileId) with
        {
            State = ServerRuntimeState.Stopping,
            ProcessId = process.Id,
        });
        await hubContext.Clients.All.SendAsync("statusChanged", runtimeStateStore.GetOrDefault(profileId), cancellationToken);

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }

        _processes.TryRemove(profileId, out _);
        if (_commandGates.TryRemove(profileId, out var commandGate))
        {
            commandGate.Dispose();
        }
        _stopRequested.TryRemove(profileId, out _);
        var liveOperations = runtimeStateStore.ResetLiveOperations(profileId);
        runtimeStateStore.Update(runtimeStateStore.GetOrDefault(profileId) with
        {
            State = ServerRuntimeState.Stopped,
            ProcessId = null,
            StoppedAtUtc = DateTimeOffset.UtcNow,
            LastExitReason = "Stopped by launcher.",
        });
        await hubContext.Clients.All.SendAsync("statusChanged", runtimeStateStore.GetOrDefault(profileId), cancellationToken);
        await hubContext.Clients.All.SendAsync("liveOperationsChanged", liveOperations, cancellationToken);
    }

    public bool IsRunning(string profileId) =>
        _processes.TryGetValue(profileId, out var process) && !process.HasExited;

    private async Task OnOutputAsync(string profileId, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var liveOperations = runtimeStateStore.AppendLog(profileId, line);
        await hubContext.Clients.All.SendAsync("logLine", profileId, line);
        if (liveOperations is not null)
        {
            await hubContext.Clients.All.SendAsync("statusChanged", runtimeStateStore.GetOrDefault(profileId));
            await hubContext.Clients.All.SendAsync("liveOperationsChanged", liveOperations);
        }
    }

    private async Task OnExitedAsync(string profileId, bool autoRestartOnCrash)
    {
        _processes.TryRemove(profileId, out var process);
        if (_commandGates.TryRemove(profileId, out var commandGate))
        {
            commandGate.Dispose();
        }
        var stopRequested = _stopRequested.TryGetValue(profileId, out var requested) && requested;
        _stopRequested.TryRemove(profileId, out _);
        var liveOperations = runtimeStateStore.ResetLiveOperations(profileId);

        var state = runtimeStateStore.GetOrDefault(profileId) with
        {
            State = stopRequested ? ServerRuntimeState.Stopped : ServerRuntimeState.Crashed,
            ProcessId = null,
            StoppedAtUtc = DateTimeOffset.UtcNow,
            LastExitReason = stopRequested ? "Stopped by launcher." : $"Process exited with code {process?.ExitCode}.",
        };
        runtimeStateStore.Update(state);
        await hubContext.Clients.All.SendAsync("statusChanged", state);
        await hubContext.Clients.All.SendAsync("liveOperationsChanged", liveOperations);

        if (!stopRequested && autoRestartOnCrash)
        {
            await AttemptAutoRestartAsync(profileId);
        }
    }

    private async Task AttemptAutoRestartAsync(string profileId)
    {
        runtimeStateStore.AppendLog(profileId, "Auto-restart requested after crash.");
        runtimeStateStore.Update(runtimeStateStore.GetOrDefault(profileId) with
        {
            State = ServerRuntimeState.Starting,
            LastExitReason = "Auto-restarting after crash.",
        });
        await hubContext.Clients.All.SendAsync("statusChanged", runtimeStateStore.GetOrDefault(profileId));

        await Task.Delay(TimeSpan.FromSeconds(2));

        await using var scope = scopeFactory.CreateAsyncScope();
        var profileStore = scope.ServiceProvider.GetRequiredService<ProfileStore>();
        var profile = await profileStore.GetAsync(profileId, CancellationToken.None);
        if (profile is null)
        {
            runtimeStateStore.Update(runtimeStateStore.GetOrDefault(profileId) with
            {
                State = ServerRuntimeState.Crashed,
                LastExitReason = "Auto-restart failed because the profile no longer exists.",
            });
            await hubContext.Clients.All.SendAsync("statusChanged", runtimeStateStore.GetOrDefault(profileId));
            return;
        }

        try
        {
            await StartAsync(profile, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-restart failed for profile {ProfileId}.", profileId);
            runtimeStateStore.AppendLog(profileId, $"Auto-restart failed: {ex.Message}");
            runtimeStateStore.Update(runtimeStateStore.GetOrDefault(profileId) with
            {
                State = ServerRuntimeState.Crashed,
                LastExitReason = $"Auto-restart failed: {ex.Message}",
            });
            await hubContext.Clients.All.SendAsync("statusChanged", runtimeStateStore.GetOrDefault(profileId));
        }
    }
}
