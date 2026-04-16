using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Runtime;

public sealed partial class LauncherRuntime
{
    public Task<OperationResultDto?> InstallAsync(string profileId, CancellationToken cancellationToken = default) =>
        QueueLifecycleJobAsync(
            OperationJobKind.Install,
            profileId,
            $"Install {profileId}",
            async (services, runningJob, token) =>
            {
                var installer = services.GetRequiredService<ServerInstallService>();
                var jobStore = services.GetRequiredService<JobStore>();
                var runtimeStateStore = services.GetRequiredService<RuntimeStateStore>();
                await installer.ExecuteInstallAsync(profileId, runningJob, jobStore, runtimeStateStore, token);
            },
            "Install queued. SteamCMD may take a few minutes on first bootstrap.",
            cancellationToken);

    public Task<OperationResultDto?> UpdateAsync(string profileId, CancellationToken cancellationToken = default) =>
        QueueLifecycleJobAsync(
            OperationJobKind.Update,
            profileId,
            $"Update {profileId}",
            async (services, runningJob, token) =>
            {
                var installer = services.GetRequiredService<ServerInstallService>();
                var jobStore = services.GetRequiredService<JobStore>();
                var runtimeStateStore = services.GetRequiredService<RuntimeStateStore>();
                var backupService = services.GetRequiredService<ServerBackupService>();
                await backupService.CreateBackupAsync(profileId, BackupTrigger.PreUpdate, token);
                await installer.ExecuteInstallAsync(profileId, runningJob, jobStore, runtimeStateStore, token);
            },
            "Update queued. SteamCMD may take a few minutes on first bootstrap.",
            cancellationToken);

    public Task<OperationResultDto?> StartAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                await services.GetRequiredService<ServerProcessSupervisor>().StartAsync(profile, cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "runtime.started",
                    profileId,
                    "desktop",
                    "Started server process.",
                    cancellationToken: cancellationToken);
                return new OperationResultDto(true, "Server started.");
            },
            cancellationToken);

    public Task<OperationResultDto?> StopAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                await services.GetRequiredService<ServerProcessSupervisor>().StopAsync(profileId, cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "runtime.stopped",
                    profileId,
                    "desktop",
                    "Stopped server process.",
                    cancellationToken: cancellationToken);
                return new OperationResultDto(true, "Server stopped.");
            },
            cancellationToken);

    public Task<OperationResultDto?> RestartAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profileStore = services.GetRequiredService<ProfileStore>();
                var profile = await profileStore.GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var supervisor = services.GetRequiredService<ServerProcessSupervisor>();
                await supervisor.StopAsync(profileId, cancellationToken);
                await supervisor.StartAsync(profile, cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "runtime.restarted",
                    profileId,
                    "desktop",
                    "Restarted server process.",
                    cancellationToken: cancellationToken);
                return new OperationResultDto(true, "Server restarted.");
            },
            cancellationToken);

    public Task<OperationResultDto?> BackupAsync(string profileId, CancellationToken cancellationToken = default) =>
        QueueLifecycleJobAsync(
            OperationJobKind.Backup,
            profileId,
            $"Backup {profileId}",
            async (services, runningJob, token) =>
            {
                var backupService = services.GetRequiredService<ServerBackupService>();
                var jobStore = services.GetRequiredService<JobStore>();
                var zipPath = await backupService.CreateBackupAsync(profileId, BackupTrigger.Manual, token);
                await jobStore.UpdateAsync(runningJob with
                {
                    Status = OperationJobStatus.Succeeded,
                    ProgressPercent = 100,
                    Detail = $"Created backup {Path.GetFileName(zipPath)}.",
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                }, token);
            },
            "Backup queued.",
            cancellationToken);

    public Task<List<string>?> GetBackupsAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            services => Task.FromResult<List<string>?>(services.GetRequiredService<ServerBackupService>().ListBackups(profileId).ToList()),
            cancellationToken);

    public Task<List<string>?> GetRecentLogsAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            services => Task.FromResult<List<string>?>(services.GetRequiredService<RuntimeStateStore>().GetRecentLogs(profileId).ToList()),
            cancellationToken);

    public Task<ServerRuntimeStatus?> GetStatusAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            services => Task.FromResult<ServerRuntimeStatus?>(services.GetRequiredService<RuntimeStateStore>().GetOrDefault(profileId)),
            cancellationToken);

    public Task<ProfileLiveOperationsSnapshot?> GetLiveOperationsAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            services => Task.FromResult<ProfileLiveOperationsSnapshot?>(services.GetRequiredService<RuntimeStateStore>().GetLiveOperations(profileId)),
            cancellationToken);

    public Task<ProfileLiveOperationsSnapshot?> SendBroadcastAsync(
        string profileId,
        string message,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var liveOperations = await services.GetRequiredService<ServerProcessSupervisor>()
                    .SendBroadcastAsync(profileId, message, cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "runtime.broadcast",
                    profileId,
                    "desktop",
                    $"Broadcast sent: {message.Trim()}",
                    cancellationToken: cancellationToken);
                return liveOperations;
            },
            cancellationToken);

    public Task<ProfileLiveOperationsSnapshot?> SendConsoleCommandAsync(
        string profileId,
        string command,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var liveOperations = await services.GetRequiredService<ServerProcessSupervisor>()
                    .SendCommandAsync(profileId, command, cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "runtime.command",
                    profileId,
                    "desktop",
                    $"Console command sent: {command.Trim()}",
                    cancellationToken: cancellationToken);
                return liveOperations;
            },
            cancellationToken);

    public Task<OperationResultDto?> RestoreAsync(
        string profileId,
        string backupFileName,
        bool restartAfterRestore,
        CancellationToken cancellationToken = default) =>
        QueueLifecycleJobAsync(
            OperationJobKind.Restore,
            profileId,
            $"Restore {profileId}",
            async (services, runningJob, token) =>
            {
                var supervisor = services.GetRequiredService<ServerProcessSupervisor>();
                var backupService = services.GetRequiredService<ServerBackupService>();
                var profileStore = services.GetRequiredService<ProfileStore>();
                var jobStore = services.GetRequiredService<JobStore>();

                if (supervisor.IsRunning(profileId))
                {
                    await supervisor.StopAsync(profileId, token);
                }

                await backupService.RestoreBackupAsync(profileId, backupFileName, token);

                if (restartAfterRestore)
                {
                    var profile = await profileStore.GetAsync(profileId, token)
                        ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
                    await supervisor.StartAsync(profile, token);
                }

                await jobStore.UpdateAsync(runningJob with
                {
                    Status = OperationJobStatus.Succeeded,
                    ProgressPercent = 100,
                    Detail = $"Restored backup {backupFileName}.",
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                }, token);
            },
            "Restore queued.",
            cancellationToken);

    public Task<OperationResultDto?> ResetWorldAsync(
        string profileId,
        bool createBackupBeforeReset,
        bool restartAfterReset,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profileStore = services.GetRequiredService<ProfileStore>();
                var profile = await profileStore.GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var supervisor = services.GetRequiredService<ServerProcessSupervisor>();
                var worldResetService = services.GetRequiredService<ServerWorldResetService>();
                var auditStore = services.GetRequiredService<AuditStore>();

                var wasRunning = supervisor.IsRunning(profileId);
                if (wasRunning)
                {
                    await supervisor.StopAsync(profileId, cancellationToken);
                    await auditStore.WriteAsync(
                        "runtime.stopped",
                        profileId,
                        "desktop",
                        "Stopped server process for world reset.",
                        cancellationToken: cancellationToken);
                }

                var result = await worldResetService.ResetWorldAsync(profileId, createBackupBeforeReset, cancellationToken);

                if (restartAfterReset)
                {
                    await supervisor.StartAsync(profile, cancellationToken);
                    await auditStore.WriteAsync(
                        "runtime.started",
                        profileId,
                        "desktop",
                        "Started server process after world reset.",
                        cancellationToken: cancellationToken);
                }

                return new OperationResultDto(
                    true,
                    ServerWorldResetService.BuildUserMessage(result, restartAfterReset));
            },
            cancellationToken);

    public async Task<OperationResultDto?> StopRuntimeAsync(bool stopRunningServers, CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var host = _host ?? throw new InvalidOperationException("Integrated runtime is not available.");
            await using var scope = host.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var runtimeStateStore = services.GetRequiredService<RuntimeStateStore>();
            var supervisor = services.GetRequiredService<ServerProcessSupervisor>();
            var auditStore = services.GetRequiredService<AuditStore>();

            var runningProfiles = runtimeStateStore.ListStatuses()
                .Where(status => status.State is ServerRuntimeState.Starting or ServerRuntimeState.Running or ServerRuntimeState.Stopping)
                .Select(status => status.ProfileId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (runningProfiles.Count > 0 && !stopRunningServers)
            {
                throw new InvalidOperationException(
                    $"{runningProfiles.Count} managed server(s) are still active. Closing the app will stop the integrated runtime. Choose the shutdown option that stops the running servers first.");
            }

            if (stopRunningServers)
            {
                foreach (var runningProfileId in runningProfiles)
                {
                    await supervisor.StopAsync(runningProfileId, cancellationToken);
                }
            }

            await auditStore.WriteAsync(
                "host.stopped",
                "host",
                "desktop",
                stopRunningServers && runningProfiles.Count > 0
                    ? $"Stopped {runningProfiles.Count} server(s) and shut down the integrated runtime."
                    : "Shut down the integrated runtime.",
                cancellationToken: cancellationToken);

            await host.StopAsync(cancellationToken);
            await DisposeHostAsync(host);
            _host = null;
            _startedAtUtc = null;

            return new OperationResultDto(
                true,
                stopRunningServers && runningProfiles.Count > 0
                    ? $"Stopping {runningProfiles.Count} server(s) and shutting down the integrated runtime."
                    : "Shutting down the integrated runtime.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
