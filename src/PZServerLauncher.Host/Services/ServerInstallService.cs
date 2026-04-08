using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Host.Services;

public sealed class ServerInstallService(
    AppPaths appPaths,
    ProjectZomboidServerPlanner planner,
    SteamCmdToolService steamCmdToolService,
    ProfileStore profileStore,
    AuditStore auditStore)
{
    public async Task ExecuteInstallAsync(
        string profileId,
        OperationJob job,
        JobStore jobStore,
        RuntimeStateStore runtimeStateStore,
        CancellationToken cancellationToken)
    {
        var profile = await profileStore.GetAsync(profileId, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");

        Directory.CreateDirectory(Path.Combine(appPaths.ToolsDirectory, "steamcmd", "scripts"));
        var scriptPlan = planner.CreateInstallScript(profile);
        var scriptPath = Path.Combine(appPaths.ToolsDirectory, "steamcmd", "scripts", $"{profileId}-{job.Kind.ToString().ToLowerInvariant()}.txt");
        await File.WriteAllTextAsync(scriptPath, planner.FormatSteamCmdScript(scriptPlan), cancellationToken);

        await jobStore.UpdateAsync(job with
        {
            Status = OperationJobStatus.Running,
            ProgressPercent = 10,
            StartedAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken);

        var exitCode = await steamCmdToolService.RunScriptAsync(scriptPath, async line =>
        {
            runtimeStateStore.AppendLog(profileId, line);
            await Task.CompletedTask;
        }, cancellationToken);

        var status = exitCode == 0 ? OperationJobStatus.Succeeded : OperationJobStatus.Failed;
        await jobStore.UpdateAsync(job with
        {
            Status = status,
            ProgressPercent = status == OperationJobStatus.Succeeded ? 100 : 100,
            Detail = exitCode == 0 ? "SteamCMD finished successfully." : $"SteamCMD exited with code {exitCode}.",
            StartedAtUtc = job.StartedAtUtc ?? DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken);

        await auditStore.WriteAsync(
            status == OperationJobStatus.Succeeded ? "install.completed" : "install.failed",
            profileId,
            "local",
            exitCode == 0 ? "SteamCMD install/update completed." : $"SteamCMD failed with exit code {exitCode}.",
            cancellationToken: cancellationToken);
    }
}
