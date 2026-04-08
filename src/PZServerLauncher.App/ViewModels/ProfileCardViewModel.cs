using CommunityToolkit.Mvvm.ComponentModel;
using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.App.ViewModels;

public partial class ProfileCardViewModel : ViewModelBase
{
    public ProfileCardViewModel(
        string profileId,
        string displayName,
        string branch,
        string ports,
        string runtimeState,
        string installDirectory,
        string cacheDirectory,
        string lastBackup,
        string latestLogLine,
        bool hasBackup,
        string editableServerName,
        string editableDefaultPort,
        string editableUdpPort,
        string editableRconPort,
        string editableBindIp,
        string editableAdminUsername,
        string editableMemoryInGigabytes,
        bool editableStartWithHost,
        bool editableAutoRestartOnCrash,
        string workshopSummary,
        string workshopDiagnostics,
        ProjectZomboidProfilePostureSummary posture,
        ProjectZomboidInstallPostureSummary installPosture)
    {
        ProfileId = profileId;
        DisplayName = displayName;
        Branch = branch;
        Ports = ports;
        RuntimeState = runtimeState;
        InstallDirectory = installDirectory;
        CacheDirectory = cacheDirectory;
        LastBackup = lastBackup;
        LatestLogLine = latestLogLine;
        HasBackup = hasBackup;
        EditableServerName = editableServerName;
        EditableDefaultPort = editableDefaultPort;
        EditableUdpPort = editableUdpPort;
        EditableRconPort = editableRconPort;
        EditableBindIp = editableBindIp;
        EditableAdminUsername = editableAdminUsername;
        EditableMemoryInGigabytes = editableMemoryInGigabytes;
        EditableStartWithHost = editableStartWithHost;
        EditableAutoRestartOnCrash = editableAutoRestartOnCrash;
        WorkshopSummary = workshopSummary;
        WorkshopDiagnostics = workshopDiagnostics;
        Posture = posture;
        InstallPosture = installPosture;
        SelectedRawConfigKind = ConfigFileOptionViewModel.All[0];
        RawConfigDiagnostics = "Load a raw config file to run validation.";
        RawConfigStatus = "No raw config file loaded yet.";
    }

    public string ProfileId { get; }

    public string DisplayName { get; }

    public string Branch { get; }

    public string Ports { get; }

    public string InstallDirectory { get; }

    public string CacheDirectory { get; }

    public ProjectZomboidProfilePostureSummary Posture { get; }

    public ProjectZomboidInstallPostureSummary InstallPosture { get; }

    public string CommunitySummary => Posture.CommunitySummary;

    public string ServerRulesSummary => Posture.ServerRulesSummary;

    public string NetworkSummary => Posture.NetworkSummary;

    public string WorldSummary => Posture.WorldSummary;

    public string WelcomeSummary => Posture.WelcomeSummary;

    public bool IsPubliclyListed => Posture.IsPubliclyListed;

    public bool IsOpenAccess => Posture.IsOpenAccess;

    public bool IsPvpEnabled => Posture.IsPvpEnabled;

    public bool IsVoiceEnabled => Posture.IsVoiceEnabled;

    public bool IsSafetyEnabled => Posture.IsSafetyEnabled;

    public bool IsInstallDetected => InstallPosture.InstallDetected;

    public string BranchChannelSummary => InstallPosture.BranchChannelSummary;

    public string SteamCmdCommandSummary => InstallPosture.SteamCmdCommandSummary;

    public string ExpectedLauncherPath => InstallPosture.ExpectedLauncherPath;

    public string InstallFootprintSummary => InstallPosture.InstallFootprintSummary;

    public string CacheFootprintSummary => InstallPosture.CacheFootprintSummary;

    public string LaunchReadinessSummary => InstallPosture.LaunchReadinessSummary;

    public string RuntimePolicySummary => InstallPosture.RuntimePolicySummary;

    public string BackupSafetySummary => InstallPosture.BackupSafetySummary;

    public string InstallPreflightSummary => InstallPosture.PreflightSummary;

    public bool CacheDetected => InstallPosture.CacheDetected;

    public bool LauncherDetected => InstallPosture.LauncherDetected;

    public bool ConfigDirectoryDetected => InstallPosture.ConfigDirectoryDetected;

    public bool IniDetected => InstallPosture.IniDetected;

    public bool SandboxDetected => InstallPosture.SandboxDetected;

    public bool WorldDetected => InstallPosture.WorldDetected;

    public bool UsesDirectJavaTemplate => InstallPosture.UsesDirectJavaTemplate;

    public IReadOnlyList<ConfigFileOptionViewModel> RawConfigKinds { get; } = ConfigFileOptionViewModel.All;

    [ObservableProperty]
    private string runtimeState;

    [ObservableProperty]
    private string lastBackup;

    [ObservableProperty]
    private string latestLogLine;

    [ObservableProperty]
    private bool hasBackup;

    [ObservableProperty]
    private string editableServerName;

    [ObservableProperty]
    private string editableDefaultPort;

    [ObservableProperty]
    private string editableUdpPort;

    [ObservableProperty]
    private string editableRconPort;

    [ObservableProperty]
    private string editableBindIp;

    [ObservableProperty]
    private string editableAdminUsername;

    [ObservableProperty]
    private string editableMemoryInGigabytes;

    [ObservableProperty]
    private bool editableStartWithHost;

    [ObservableProperty]
    private bool editableAutoRestartOnCrash;

    [ObservableProperty]
    private string workshopSummary;

    [ObservableProperty]
    private string workshopDiagnostics;

    [ObservableProperty]
    private ConfigFileOptionViewModel selectedRawConfigKind;

    [ObservableProperty]
    private string rawConfigContent = string.Empty;

    [ObservableProperty]
    private string rawConfigDiagnostics;

    [ObservableProperty]
    private string rawConfigStatus;

    [ObservableProperty]
    private string loadedRawConfigSha256 = string.Empty;

    [ObservableProperty]
    private ConfigFileKind loadedRawConfigKind;

    [ObservableProperty]
    private bool isRawConfigLoaded;

    partial void OnSelectedRawConfigKindChanged(ConfigFileOptionViewModel value)
    {
        if (IsRawConfigLoaded && LoadedRawConfigKind != value.Kind)
        {
            RawConfigStatus = $"Selected {value.Label}. Load it before editing or saving.";
        }
    }
}
