using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProjectZomboidHostManagedProfileSnapshot(
    string DisplayName,
    string Branch,
    string RuntimeState,
    bool StartWithHost,
    bool AutoRestartOnCrash,
    bool HasBackup,
    bool InstallDetected,
    string Ports);

public sealed record ProjectZomboidHostOperatorSummary(
    string LifecycleHeadline,
    string FleetHeadline,
    string ExposureHeadline,
    string SecurityHeadline,
    string StartupHeadline,
    string RecoveryHeadline,
    string AutomationHeadline,
    string RuntimeHeadline,
    string OperatorSummary,
    string RiskHeadline,
    string NextStepSummary,
    IReadOnlyList<ProjectZomboidOperatorChecklistItem> Checklist);

public static class ProjectZomboidHostOperatorSummaryBuilder
{
    public static ProjectZomboidHostOperatorSummary Build(
        HostSettings settings,
        IReadOnlyCollection<ProjectZomboidHostManagedProfileSnapshot> profiles)
    {
        var managedCount = profiles.Count;
        var installedCount = profiles.Count(profile => profile.InstallDetected);
        var runningCount = profiles.Count(profile => string.Equals(profile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase));
        var startupCount = profiles.Count(profile => profile.StartWithHost);
        var autoRestartCount = profiles.Count(profile => profile.AutoRestartOnCrash);
        var backupCount = profiles.Count(profile => profile.HasBackup);

        var lifecycleHeadline = $"Loopback {settings.LoopbackPort} | Windows startup {(settings.StartHostWithWindows ? "enabled" : "disabled")}.";
        var fleetHeadline = managedCount == 0
            ? "No Project Zomboid profiles are loaded yet."
            : $"{managedCount} profile(s) loaded | {installedCount} installed | {runningCount} running.";
        var exposureHeadline = settings.RemoteAccess.IsEnabled
            ? $"HTTPS staged for {settings.RemoteAccess.BindAddress}:{settings.RemoteAccess.HttpsPort}."
            : "Loopback-only mode is active.";
        var securityHeadline = settings.OwnerBootstrap.IsConfigured
            ? $"Owner bootstrap is complete for {settings.OwnerBootstrap.OwnerUserName ?? "the local owner"}."
            : "Owner bootstrap is still pending.";
        var startupHeadline = managedCount == 0
            ? "Startup posture appears after the first profile exists."
            : startupCount == 0
                ? "Everything still starts manually."
                : $"{startupCount} profile(s) are staged to start with the host.";
        var recoveryHeadline = managedCount == 0
            ? "Recovery posture appears after the first profile exists."
            : backupCount == 0
                ? "No profiles currently have recovery coverage."
                : $"{backupCount} profile(s) already have at least one recovery archive.";
        var automationHeadline = managedCount == 0
            ? "Automation posture appears after the first profile exists."
            : autoRestartCount == 0
                ? "Crashes currently need manual intervention."
                : $"{autoRestartCount} profile(s) auto-restart after a crash.";
        var runtimeHeadline = managedCount == 0
            ? "No runtime roster yet."
            : runningCount == 0
                ? "Nothing is currently online."
                : $"{runningCount} profile(s) are currently online under this host.";

        return new ProjectZomboidHostOperatorSummary(
            lifecycleHeadline,
            fleetHeadline,
            exposureHeadline,
            securityHeadline,
            startupHeadline,
            recoveryHeadline,
            automationHeadline,
            runtimeHeadline,
            BuildOperatorSummary(settings, managedCount, installedCount, startupCount, autoRestartCount, backupCount),
            BuildRiskHeadline(settings, managedCount, installedCount, startupCount, autoRestartCount, backupCount),
            BuildNextStepSummary(settings, managedCount, backupCount),
            BuildChecklist(settings, managedCount, installedCount, startupCount, autoRestartCount, backupCount));
    }

    public static ProjectZomboidHostOperatorSummary Empty() =>
        Build(new HostSettings(), Array.Empty<ProjectZomboidHostManagedProfileSnapshot>());

    private static string BuildOperatorSummary(
        HostSettings settings,
        int managedCount,
        int installedCount,
        int startupCount,
        int autoRestartCount,
        int backupCount)
    {
        if (managedCount == 0)
        {
            return "Create or import the first Project Zomboid profile before this machine becomes an always-on controller.";
        }

        if (startupCount > installedCount)
        {
            return "At least one profile is marked to start with the host but does not currently show an install. Fix that before you trust unattended startup.";
        }

        if (backupCount < managedCount)
        {
            return "Some profiles still lack recovery coverage. Capture the first backup before this machine becomes your always-on control plane.";
        }

        if (autoRestartCount == 0)
        {
            return "Auto-restart is disabled across the fleet, so every crash will wait for operator intervention.";
        }

        if (settings.RemoteAccess.IsEnabled && !settings.OwnerBootstrap.IsConfigured)
        {
            return "Remote exposure is staged before owner bootstrap is complete. Finish owner setup and 2FA before you rely on the web surface.";
        }

        if (!settings.StartHostWithWindows && startupCount > 0)
        {
            return "Profiles are staged for host startup, but Windows startup is still off. Decide whether this machine should behave like a persistent controller.";
        }

        return "The host posture is coherent: startup, recovery, automation, and remote exposure all line up for the current fleet.";
    }

    private static string BuildRiskHeadline(
        HostSettings settings,
        int managedCount,
        int installedCount,
        int startupCount,
        int autoRestartCount,
        int backupCount)
    {
        if (managedCount == 0)
        {
            return "No fleet risk is visible yet because the host is not supervising any profiles.";
        }

        if (startupCount > installedCount)
        {
            return "Startup risk is elevated because at least one startup profile does not currently show an install.";
        }

        if (backupCount < managedCount)
        {
            return $"{managedCount - backupCount} profile(s) still need their first backup archive.";
        }

        if (settings.RemoteAccess.IsEnabled && !settings.OwnerBootstrap.IsConfigured)
        {
            return "Remote exposure is staged before owner bootstrap is complete.";
        }

        if (autoRestartCount == 0)
        {
            return "Crash recovery is fully manual right now.";
        }

        return "Host risk is currently steady for the loaded fleet.";
    }

    private static string BuildNextStepSummary(HostSettings settings, int managedCount, int backupCount)
    {
        if (managedCount == 0)
        {
            return "Import or create the first profile, then decide whether this machine should stay manual or become an always-on controller.";
        }

        if (backupCount < managedCount)
        {
            return "Take first backups for uncovered profiles next so startup and crash automation are backed by a real rollback path.";
        }

        return settings.RemoteAccess.IsEnabled
            ? "Review Remote Access and Users next so the optional web surface stays secure before you expose it more broadly."
            : "Decide whether this machine should remain desktop-only or whether you want to prepare the optional remote web surface.";
    }

    private static IReadOnlyList<ProjectZomboidOperatorChecklistItem> BuildChecklist(
        HostSettings settings,
        int managedCount,
        int installedCount,
        int startupCount,
        int autoRestartCount,
        int backupCount)
    {
        var checklist = new List<ProjectZomboidOperatorChecklistItem>();

        if (managedCount == 0)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Follow-up", "Create or import the first profile before you tune startup, recovery, or remote exposure.", false, true));
            return checklist;
        }

        if (startupCount > installedCount)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Blocking", "One or more startup profiles do not currently show an install footprint. Fix install posture before unattended startup.", true, false));
        }

        if (backupCount < managedCount)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Blocking", "Capture the first recovery archive for uncovered profiles before you trust startup or crash automation.", true, false));
        }

        if (autoRestartCount == 0)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Follow-up", "Decide whether crashes should stay manual or whether at least one profile should auto-restart after failure.", false, true));
        }

        if (!settings.StartHostWithWindows && startupCount > 0)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Follow-up", "Enable Windows startup if this machine is meant to behave like a persistent server controller.", false, true));
        }

        if (settings.RemoteAccess.IsEnabled && !settings.OwnerBootstrap.IsConfigured)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Blocking", "Finish owner bootstrap and 2FA before relying on the staged remote listener.", true, false));
        }
        else if (settings.RemoteAccess.IsEnabled)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Follow-up", "Review Remote Access and Users together before exposing the host beyond the local desktop.", false, true));
        }
        else
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Healthy", "The host is still loopback-only, which is the safest state while you keep administration local.", false, false));
        }

        if (checklist.Count == 0)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Healthy", "The host posture is coherent. Use Overview and Logs to verify runtime behavior after the next restart.", false, false));
        }

        return checklist;
    }
}
