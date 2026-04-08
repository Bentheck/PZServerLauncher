using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class GeneralWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private SettingsCatalogDto? _catalog;
    private SettingsPageDto? _page;
    private string? _sourceSha256;
    private bool _isApplyingState;

    public GeneralWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient)
        : base(
            ProfileWorkspacePageIds.General,
            "General",
            "Structured server identity, memory, startup behavior, and primary ports.",
            "General settings are in sync.",
            legacy,
            ["Server identity", "Primary ports", "Memory", "Startup behavior"])
    {
        _hostApiClient = hostApiClient;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to edit the first structured settings page."
        : $"Structured General settings for {SelectedProfile.DisplayName}.";

    public ObservableCollection<string> FieldErrors { get; } = [];

    public bool HasFieldErrors => FieldErrors.Count > 0;

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    [ObservableProperty]
    private string loadStatus = "Select a profile to load the structured General editor.";

    [ObservableProperty]
    private string serverName = string.Empty;

    [ObservableProperty]
    private string defaultPort = string.Empty;

    [ObservableProperty]
    private string udpPort = string.Empty;

    [ObservableProperty]
    private string rconPort = string.Empty;

    [ObservableProperty]
    private string memoryInGigabytes = string.Empty;

    [ObservableProperty]
    private bool startWithHost;

    [ObservableProperty]
    private bool autoRestartOnCrash;

    [ObservableProperty]
    private bool requiresAdvancedFilesFallback;

    [ObservableProperty]
    private string fallbackReason = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string catalogSummary = "No structured catalog loaded.";

    [ObservableProperty]
    private bool canEdit;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        _ = LoadAsync(profile);
    }

    public override async Task SaveDraftAsync()
    {
        if (SelectedProfile is null || _catalog is null || !CanEdit)
        {
            return;
        }

        var payload = new SettingsDraftDto(
            SelectedProfile.ProfileId,
            SelectedProfile.Branch.Contains("42", StringComparison.Ordinal) ? PZServerLauncher.Core.Profiles.ProjectZomboidBranch.Unstable42 : PZServerLauncher.Core.Profiles.ProjectZomboidBranch.Stable41,
            _catalog.CatalogId,
            _catalog.CatalogVersion,
            ProfileWorkspacePageIds.General,
            BuildValues(),
            _sourceSha256,
            true,
            DateTimeOffset.UtcNow);

        await _hostApiClient.SaveSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.General, payload);
        MarkClean("Saved General draft.");
        LoadStatus = "Saved a General draft. Apply it to write the server files.";
    }

    public override async Task DiscardDraftAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            await _hostApiClient.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.General);
        }
        catch
        {
        }

        await LoadAsync(SelectedProfile);
    }

    private async Task SaveSettingsAsync()
    {
        if (SelectedProfile is null || _catalog is null || !CanEdit)
        {
            return;
        }

        var payload = new SettingsValueSetDto(
            _catalog.CatalogId,
            _catalog.CatalogVersion,
            ProfileWorkspacePageIds.General,
            BuildValues(),
            _sourceSha256,
            false,
            null);

        var result = await _hostApiClient.SaveSettingsPageAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.General, payload);
        if (result is null)
        {
            LoadStatus = "General settings could not be saved.";
            return;
        }

        ApplyValidation(result.Validation);
        if (!result.Validation.IsValid || result.Validation.RequiresAdvancedFilesFallback)
        {
            LoadStatus = result.Validation.FallbackReason ?? "General settings need attention before they can be saved.";
            return;
        }

        try
        {
            await _hostApiClient.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.General);
        }
        catch
        {
        }

        ApplyValueSet(result.ValueSet, markCleanMessage: $"Saved General settings for {SelectedProfile.DisplayName}.");
        await Legacy.RefreshCommand.ExecuteAsync(null);
    }

    private async Task ReloadAsync()
    {
        await LoadAsync(SelectedProfile);
    }

    private async Task LoadAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            Reset();
            return;
        }

        IsLoading = true;
        LoadStatus = $"Loading General settings for {profile.DisplayName}...";

        try
        {
            _catalog = await _hostApiClient.GetSettingsCatalogAsync(profile.ProfileId);
            _page = _catalog?.Pages.FirstOrDefault(page => string.Equals(page.PageId, ProfileWorkspacePageIds.General, StringComparison.Ordinal));
            var valueSet = await _hostApiClient.GetSettingsPageAsync(profile.ProfileId, ProfileWorkspacePageIds.General);
            var draft = await _hostApiClient.GetSettingsDraftAsync(profile.ProfileId, ProfileWorkspacePageIds.General);

            CatalogSummary = _catalog is null
                ? "No structured catalog available."
                : $"{_catalog.CatalogId} v{_catalog.CatalogVersion} | {_catalog.Branch}";

            if (valueSet is null)
            {
                Reset();
                LoadStatus = "General settings could not be loaded.";
                return;
            }

            ApplyValueSet(valueSet, markCleanMessage: "General settings loaded from the local host.");
            if (draft is not null && draft.Values.Count > 0)
            {
                ApplyDraft(draft);
            }
        }
        catch (Exception ex)
        {
            Reset();
            LoadStatus = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyDraft(SettingsDraftDto draft)
    {
        ApplyValues(draft.Values);
        if (draft.IsDirty)
        {
            MarkDirty("Loaded a saved General draft.");
            LoadStatus = "Loaded a saved General draft from SQLite-backed workspace state.";
        }
        else
        {
            MarkClean("Loaded saved General draft.");
        }
    }

    private void ApplyValueSet(SettingsValueSetDto valueSet, string markCleanMessage)
    {
        _sourceSha256 = valueSet.SourceSha256;
        RequiresAdvancedFilesFallback = valueSet.RequiresAdvancedFilesFallback;
        FallbackReason = valueSet.FallbackReason ?? string.Empty;
        CanEdit = !valueSet.RequiresAdvancedFilesFallback;
        ApplyValues(valueSet.Values);
        MarkClean(markCleanMessage);
        LoadStatus = valueSet.RequiresAdvancedFilesFallback
            ? valueSet.FallbackReason ?? "Structured editing is unavailable for this file."
            : markCleanMessage;
    }

    private void ApplyValues(IReadOnlyDictionary<string, string?> values)
    {
        _isApplyingState = true;
        try
        {
            ServerName = GetValue(values, ".server.name");
            DefaultPort = GetValue(values, ".server.port");
            UdpPort = GetValue(values, ".server.udp-port");
            RconPort = GetValue(values, ".server.rcon-port");
            MemoryInGigabytes = GetValue(values, ".runtime.memory");
            StartWithHost = bool.TryParse(GetValue(values, ".runtime.start-with-host"), out var startWithHost) && startWithHost;
            AutoRestartOnCrash = bool.TryParse(GetValue(values, ".runtime.auto-restart"), out var autoRestart) && autoRestart;
        }
        finally
        {
            _isApplyingState = false;
        }
    }

    private IReadOnlyDictionary<string, string?> BuildValues()
    {
        var prefix = SelectedProfile?.Branch.Contains("42", StringComparison.Ordinal) == true ? "b42" : "b41";
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{prefix}.server.name"] = ServerName,
            [$"{prefix}.server.port"] = DefaultPort,
            [$"{prefix}.server.udp-port"] = UdpPort,
            [$"{prefix}.server.rcon-port"] = RconPort,
            [$"{prefix}.runtime.memory"] = MemoryInGigabytes,
            [$"{prefix}.runtime.start-with-host"] = StartWithHost.ToString(),
            [$"{prefix}.runtime.auto-restart"] = AutoRestartOnCrash.ToString(),
        };
    }

    private void ApplyValidation(SettingsValidationResultDto validation)
    {
        FieldErrors.Clear();
        foreach (var pageError in validation.PageErrors)
        {
            FieldErrors.Add(pageError);
        }

        foreach (var entry in validation.FieldErrors.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            foreach (var error in entry.Value)
            {
                FieldErrors.Add($"{entry.Key}: {error}");
            }
        }

        OnPropertyChanged(nameof(HasFieldErrors));
    }

    private static string GetValue(IReadOnlyDictionary<string, string?> values, string suffix)
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is null ? string.Empty : values[key] ?? string.Empty;
    }

    private void Reset()
    {
        _catalog = null;
        _page = null;
        _sourceSha256 = null;
        CatalogSummary = "No structured catalog loaded.";
        RequiresAdvancedFilesFallback = false;
        FallbackReason = string.Empty;
        CanEdit = false;
        FieldErrors.Clear();
        OnPropertyChanged(nameof(HasFieldErrors));
        _isApplyingState = true;
        try
        {
            ServerName = string.Empty;
            DefaultPort = string.Empty;
            UdpPort = string.Empty;
            RconPort = string.Empty;
            MemoryInGigabytes = string.Empty;
            StartWithHost = false;
            AutoRestartOnCrash = false;
        }
        finally
        {
            _isApplyingState = false;
        }

        MarkClean("General settings are in sync.");
    }

    partial void OnServerNameChanged(string value) => NotifyFieldEdited();
    partial void OnDefaultPortChanged(string value) => NotifyFieldEdited();
    partial void OnUdpPortChanged(string value) => NotifyFieldEdited();
    partial void OnRconPortChanged(string value) => NotifyFieldEdited();
    partial void OnMemoryInGigabytesChanged(string value) => NotifyFieldEdited();
    partial void OnStartWithHostChanged(bool value) => NotifyFieldEdited();
    partial void OnAutoRestartOnCrashChanged(bool value) => NotifyFieldEdited();

    private void NotifyFieldEdited()
    {
        if (_isApplyingState || !CanEdit)
        {
            return;
        }

        MarkDirty("Unsaved changes in General.");
        LoadStatus = "General settings changed locally. Save a draft or apply them to the server files.";
    }
}
