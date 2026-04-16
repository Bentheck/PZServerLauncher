using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Runtime;

public interface ILauncherRuntime : IAsyncDisposable
{
    event Func<ServerRuntimeStatus, Task>? StatusChanged;

    event Func<OperationJob, Task>? JobChanged;

    event Func<string, string, Task>? LogLineReceived;

    event Func<ProfileLiveOperationsSnapshot, Task>? LiveOperationsChanged;

    Task<RuntimeSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken = default);

    Task<WorkspaceBootstrapDto> GetWorkspaceBootstrapAsync(CancellationToken cancellationToken = default);

    Task<SettingsCatalogDto?> GetSettingsCatalogAsync(string profileId, CancellationToken cancellationToken = default);

    Task<SettingsValueSetDto?> GetSettingsPageAsync(string profileId, string pageId, CancellationToken cancellationToken = default);

    Task<SettingsValidationResultDto?> ValidateSettingsPageAsync(
        string profileId,
        string pageId,
        SettingsValueSetDto payload,
        CancellationToken cancellationToken = default);

    Task<SettingsSaveResultDto?> SaveSettingsPageAsync(
        string profileId,
        string pageId,
        SettingsValueSetDto payload,
        CancellationToken cancellationToken = default);

    Task<SettingsDraftDto?> GetSettingsDraftAsync(string profileId, string pageId, CancellationToken cancellationToken = default);

    Task<SettingsDraftDto?> SaveSettingsDraftAsync(
        string profileId,
        string pageId,
        SettingsDraftDto payload,
        CancellationToken cancellationToken = default);

    Task DeleteSettingsDraftAsync(string profileId, string pageId, CancellationToken cancellationToken = default);

    Task<List<SandboxPresetDto>?> GetSandboxPresetsAsync(string profileId, CancellationToken cancellationToken = default);

    Task<SandboxPresetDto?> SaveSandboxPresetAsync(
        string profileId,
        string name,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default);

    Task DeleteSandboxPresetAsync(string profileId, string presetId, CancellationToken cancellationToken = default);

    Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default);

    Task<OperationResultDto?> UninstallServerAsync(string profileId, CancellationToken cancellationToken = default);

    Task<ProfileDto?> CreateStarterProfileAsync(CancellationToken cancellationToken = default);

    Task<ProfileDto?> CreateStarterProfileAsync(string displayName, int defaultPort, CancellationToken cancellationToken = default);

    Task<ProfileDto?> CreateStarterProfileAsync(
        string displayName,
        int defaultPort,
        int preferredMemoryInGigabytes,
        int maxPlayers,
        CancellationToken cancellationToken = default);

    Task CreateProfileAsync(ProfileUpsertRequestDto request, CancellationToken cancellationToken = default);

    Task<ProfileDto?> UpdateProfilePathsAsync(
        string profileId,
        string installDirectory,
        string cacheDirectory,
        CancellationToken cancellationToken = default);

    Task<OperationResultDto?> InstallAsync(string profileId, CancellationToken cancellationToken = default);

    Task<OperationResultDto?> UpdateAsync(string profileId, CancellationToken cancellationToken = default);

    Task<OperationResultDto?> StartAsync(string profileId, CancellationToken cancellationToken = default);

    Task<OperationResultDto?> StopAsync(string profileId, CancellationToken cancellationToken = default);

    Task<OperationResultDto?> RestartAsync(string profileId, CancellationToken cancellationToken = default);

    Task<OperationResultDto?> BackupAsync(string profileId, CancellationToken cancellationToken = default);

    Task<List<string>?> GetBackupsAsync(string profileId, CancellationToken cancellationToken = default);

    Task<List<ProfileImportCandidateDto>?> DiscoverLocalImportsAsync(CancellationToken cancellationToken = default);

    Task<ProfileDto?> ImportLocalCandidateAsync(string candidateId, CancellationToken cancellationToken = default);

    Task<CommonConfigDto?> UpdateCommonConfigAsync(
        string profileId,
        CommonConfigDto config,
        CancellationToken cancellationToken = default);

    Task<BackupPolicy?> GetBackupPolicyAsync(string profileId, CancellationToken cancellationToken = default);

    Task<BackupPolicy?> UpdateBackupPolicyAsync(string profileId, BackupPolicy policy, CancellationToken cancellationToken = default);

    Task<WorkshopScanResultDto?> ScanWorkshopAsync(string profileId, CancellationToken cancellationToken = default);

    Task<WorkshopCatalogSearchResultDto?> SearchWorkshopCatalogAsync(
        string profileId,
        WorkshopCatalogSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<WorkshopCatalogPreviewDto?> GetWorkshopCatalogPreviewAsync(
        string profileId,
        string workshopId,
        WorkshopCatalogPreviewRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SteamWorkshopBrowserSettingsDto?> GetWorkshopBrowserSettingsAsync(CancellationToken cancellationToken = default);

    Task<SteamWorkshopBrowserSettingsDto?> SetSteamWebApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    Task<SteamWorkshopBrowserSettingsDto?> RemoveSteamWebApiKeyAsync(CancellationToken cancellationToken = default);

    Task<byte[]?> DownloadWorkshopImageAsync(string imageUrl, CancellationToken cancellationToken = default);

    Task<WorkshopPreset?> GetWorkshopPresetAsync(string profileId, CancellationToken cancellationToken = default);

    Task<WorkshopPreset?> UpdateWorkshopPresetAsync(string profileId, WorkshopPreset preset, CancellationToken cancellationToken = default);

    Task<List<NamedWorkshopPresetDto>?> GetNamedWorkshopPresetsAsync(string profileId, CancellationToken cancellationToken = default);

    Task<NamedWorkshopPresetDto?> SaveNamedWorkshopPresetAsync(
        string profileId,
        string name,
        WorkshopPreset preset,
        CancellationToken cancellationToken = default);

    Task DeleteNamedWorkshopPresetAsync(string profileId, Guid presetId, CancellationToken cancellationToken = default);

    Task<List<string>?> GetRecentLogsAsync(string profileId, CancellationToken cancellationToken = default);

    Task<ServerRuntimeStatus?> GetStatusAsync(string profileId, CancellationToken cancellationToken = default);

    Task<ProfileLiveOperationsSnapshot?> GetLiveOperationsAsync(string profileId, CancellationToken cancellationToken = default);

    Task<ProfileLiveOperationsSnapshot?> SendBroadcastAsync(
        string profileId,
        string message,
        CancellationToken cancellationToken = default);

    Task<ProfileLiveOperationsSnapshot?> SendConsoleCommandAsync(
        string profileId,
        string command,
        CancellationToken cancellationToken = default);

    Task<RawConfigFileDto?> GetRawConfigAsync(
        string profileId,
        ConfigFileKind kind,
        CancellationToken cancellationToken = default);

    Task<RawConfigFileDto?> SaveRawConfigAsync(
        string profileId,
        ConfigFileKind kind,
        RawConfigFileDto payload,
        CancellationToken cancellationToken = default);

    Task<OperationResultDto?> RestoreAsync(
        string profileId,
        string backupFileName,
        bool restartAfterRestore,
        CancellationToken cancellationToken = default);

    Task<OperationResultDto?> ResetWorldAsync(
        string profileId,
        bool createBackupBeforeReset,
        bool restartAfterReset,
        CancellationToken cancellationToken = default);

    Task<HostSettings?> GetHostSettingsAsync(CancellationToken cancellationToken = default);

    Task<HostSettings?> UpdateHostSettingsAsync(HostSettings settings, CancellationToken cancellationToken = default);

    Task<OperationResultDto?> StopRuntimeAsync(bool stopRunningServers, CancellationToken cancellationToken = default);
}
