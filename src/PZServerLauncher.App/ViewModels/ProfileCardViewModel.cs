using CommunityToolkit.Mvvm.ComponentModel;

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
        string workshopDiagnostics)
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
    }

    public string ProfileId { get; }

    public string DisplayName { get; }

    public string Branch { get; }

    public string Ports { get; }

    public string InstallDirectory { get; }

    public string CacheDirectory { get; }

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
}
