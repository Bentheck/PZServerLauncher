using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Services;

public sealed class ProfileRetirementService(
    ApplicationDbContext dbContext,
    ProfileStore profileStore,
    JobStore jobStore,
    AuditStore auditStore,
    RuntimeStateStore runtimeStateStore,
    PersistentLogService persistentLogService,
    ServerProcessSupervisor processSupervisor,
    AppPaths appPaths)
{
    public async Task<ProfileRetirementResult> UninstallServerAsync(
        string profileId,
        string actorType,
        string? actorId = null,
        CancellationToken cancellationToken = default)
    {
        var profile = await RequireProfileReadyForRetirementAsync(profileId, cancellationToken);
        if (!ServerProfileFactory.IsManagedInstallDirectory(profile.InstallDirectory))
        {
            throw new InvalidOperationException("Uninstall only removes launcher-managed installs under PZServers\\Installs. This profile is using a custom or imported install directory.");
        }

        var removedManagedInstall = FileSystemCleanup.DeleteDirectoryIfExists(profile.InstallDirectory);
        var removedRuntimeState = FileSystemCleanup.DeleteDirectoryIfExists(appPaths.RuntimeProfileDirectory(profile.ProfileId));

        await auditStore.WriteAsync(
            "profile.install.uninstalled",
            profile.ProfileId,
            actorType,
            $"Removed the launcher-managed install footprint for {profile.DisplayName}.",
            actorId,
            cancellationToken);

        return new ProfileRetirementResult(
            profile.ProfileId,
            removedManagedInstall,
            RemovedManagedCache: false,
            RemovedBackups: false,
            removedRuntimeState,
            RemovedLogs: false,
            DeletedProfile: false,
            $"Uninstalled the managed server files for {profile.DisplayName}. The profile, backups, and profile data were left intact.");
    }

    public async Task<ProfileRetirementResult> DeleteProfileAsync(
        string profileId,
        string actorType,
        string? actorId = null,
        CancellationToken cancellationToken = default)
    {
        var profile = await RequireProfileReadyForRetirementAsync(profileId, cancellationToken);

        var removedManagedInstall = ServerProfileFactory.IsManagedInstallDirectory(profile.InstallDirectory) &&
                                    FileSystemCleanup.DeleteDirectoryIfExists(profile.InstallDirectory);
        var removedManagedCache = ServerProfileFactory.IsManagedCacheDirectory(profile.CacheDirectory) &&
                                  FileSystemCleanup.DeleteDirectoryIfExists(profile.CacheDirectory);
        var removedBackups = FileSystemCleanup.DeleteDirectoryIfExists(Path.Combine(appPaths.BackupsDirectory, profile.ProfileId));
        var removedRuntimeState = FileSystemCleanup.DeleteDirectoryIfExists(appPaths.RuntimeProfileDirectory(profile.ProfileId));
        var removedLogs = persistentLogService.DeleteProfileLogs(profile.ProfileId);

        runtimeStateStore.ClearProfile(profile.ProfileId);

        var entity = await dbContext.ServerProfiles.SingleOrDefaultAsync(x => x.ProfileId == profile.ProfileId, cancellationToken);
        if (entity is null)
        {
            throw new KeyNotFoundException($"Profile '{profile.ProfileId}' was not found.");
        }

        var relatedJobs = await dbContext.OperationJobs
            .Where(job => job.ProfileId == profile.ProfileId)
            .ToListAsync(cancellationToken);
        var relatedDrafts = await dbContext.SettingsDrafts
            .Where(draft => draft.ProfileId == profile.ProfileId)
            .ToListAsync(cancellationToken);
        var relatedModsMapsDrafts = await dbContext.ModsMapsDrafts
            .Where(draft => draft.ProfileId == profile.ProfileId)
            .ToListAsync(cancellationToken);
        var relatedModsMapsModRows = await dbContext.ModsMapsDraftModRows
            .Where(row => row.ProfileId == profile.ProfileId)
            .ToListAsync(cancellationToken);
        var relatedModsMapsMapRows = await dbContext.ModsMapsDraftMapRows
            .Where(row => row.ProfileId == profile.ProfileId)
            .ToListAsync(cancellationToken);
        var relatedPresets = await dbContext.NamedWorkshopPresets
            .Where(preset => preset.ProfileId == profile.ProfileId)
            .ToListAsync(cancellationToken);
        var relatedAudits = await dbContext.AuditEntries
            .Where(entry => entry.Subject == profile.ProfileId)
            .ToListAsync(cancellationToken);

        dbContext.OperationJobs.RemoveRange(relatedJobs);
        dbContext.SettingsDrafts.RemoveRange(relatedDrafts);
        dbContext.ModsMapsDraftModRows.RemoveRange(relatedModsMapsModRows);
        dbContext.ModsMapsDraftMapRows.RemoveRange(relatedModsMapsMapRows);
        dbContext.ModsMapsDrafts.RemoveRange(relatedModsMapsDrafts);
        dbContext.NamedWorkshopPresets.RemoveRange(relatedPresets);
        dbContext.AuditEntries.RemoveRange(relatedAudits);
        dbContext.ServerProfiles.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditStore.WriteAsync(
            "profile.deleted",
            profile.ProfileId,
            actorType,
            $"Deleted profile {profile.DisplayName}.",
            actorId,
            cancellationToken);

        return new ProfileRetirementResult(
            profile.ProfileId,
            removedManagedInstall,
            removedManagedCache,
            removedBackups,
            removedRuntimeState,
            removedLogs,
            DeletedProfile: true,
            $"Deleted profile {profile.DisplayName}. Launcher-managed files, backups, logs, and runtime artifacts were cleaned up. External install or cache folders were left alone.");
    }

    private async Task<ServerProfile> RequireProfileReadyForRetirementAsync(
        string profileId,
        CancellationToken cancellationToken)
    {
        var profile = await profileStore.GetAsync(profileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Profile '{profileId}' was not found.");

        var blockingJob = await jobStore.GetActiveProfileJobAsync(profileId, cancellationToken);
        if (blockingJob is not null)
        {
            throw new InvalidOperationException($"Wait for the active {blockingJob.Kind.ToString().ToLowerInvariant()} job to finish before uninstalling or deleting {profile.DisplayName}.");
        }

        var runtimeStatus = runtimeStateStore.GetOrDefault(profileId);
        if (processSupervisor.IsRunning(profileId) ||
            runtimeStatus.State is ServerRuntimeState.Starting or ServerRuntimeState.Running or ServerRuntimeState.Stopping)
        {
            throw new InvalidOperationException($"Stop {profile.DisplayName} before uninstalling or deleting it.");
        }

        return profile;
    }
}

public sealed record ProfileRetirementResult(
    string ProfileId,
    bool RemovedManagedInstall,
    bool RemovedManagedCache,
    bool RemovedBackups,
    bool RemovedRuntimeState,
    bool RemovedLogs,
    bool DeletedProfile,
    string Message);
