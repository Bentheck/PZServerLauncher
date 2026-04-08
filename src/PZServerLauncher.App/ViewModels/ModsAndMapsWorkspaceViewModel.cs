using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.App.ViewModels;

public partial class ModsAndMapsWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private bool _isApplyingState;
    private SettingsCatalogDto? _catalog;

    public ModsAndMapsWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient)
        : base(
            ProfileWorkspacePageIds.ModsAndMaps,
            "Mods & Maps",
            "Workshop items, mod IDs, and map folders for the selected profile.",
            "Mods & Maps settings are in sync.",
            legacy,
            ["Workshop IDs or URLs", "Enabled mod IDs", "Map folders", "Scan diagnostics"])
    {
        _hostApiClient = hostApiClient;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
        ScanCommand = new AsyncRelayCommand(RunScanAsync);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to manage workshop items, mods, and maps."
        : $"Workshop, mod, and map settings for {SelectedProfile.DisplayName}.";

    public ObservableCollection<string> Diagnostics { get; } = [];

    public bool HasDiagnostics => Diagnostics.Count > 0;

    public bool HasNoDiagnostics => Diagnostics.Count == 0;

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    public IAsyncRelayCommand ScanCommand { get; }

    public bool CanScan => SelectedProfile is not null && !HasUnsavedChanges;

    [ObservableProperty]
    private string loadStatus = "Select a profile to load workshop, mod, and map settings.";

    [ObservableProperty]
    private string catalogSummary = "No structured catalog loaded.";

    [ObservableProperty]
    private string workshopItemIdsText = string.Empty;

    [ObservableProperty]
    private string enabledModIdsText = string.Empty;

    [ObservableProperty]
    private string mapFoldersText = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        _ = LoadAsync(profile);
    }

    public override async Task SaveDraftAsync()
    {
        if (SelectedProfile is null || _catalog is null)
        {
            return;
        }

        var payload = new SettingsDraftDto(
            SelectedProfile.ProfileId,
            SelectedProfile.Branch.Contains("42", StringComparison.Ordinal) ? ProjectZomboidBranch.Unstable42 : ProjectZomboidBranch.Stable41,
            _catalog.CatalogId,
            _catalog.CatalogVersion,
            ProfileWorkspacePageIds.ModsAndMaps,
            BuildDraftValues(),
            null,
            true,
            DateTimeOffset.UtcNow);

        await _hostApiClient.SaveSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps, payload);
        MarkClean("Saved Mods & Maps draft.");
        LoadStatus = "Saved a Mods & Maps draft. Apply settings to update the active profile preset.";
        OnPropertyChanged(nameof(CanScan));
    }

    public override async Task DiscardDraftAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            await _hostApiClient.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps);
        }
        catch
        {
        }

        await LoadAsync(SelectedProfile);
    }

    private async Task SaveSettingsAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var updatedPreset = await _hostApiClient.UpdateWorkshopPresetAsync(SelectedProfile.ProfileId, BuildPreset());
        if (updatedPreset is null)
        {
            LoadStatus = "Mods & Maps settings could not be saved.";
            return;
        }

        try
        {
            await _hostApiClient.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps);
        }
        catch
        {
        }

        ApplyPreset(updatedPreset);
        MarkClean($"Saved Mods & Maps settings for {SelectedProfile.DisplayName}.");
        await RunScanCoreAsync(SelectedProfile.ProfileId, overwriteEditors: true);
        await Legacy.RefreshCommand.ExecuteAsync(null);
        OnPropertyChanged(nameof(CanScan));
    }

    private async Task ReloadAsync()
    {
        await LoadAsync(SelectedProfile);
    }

    private async Task RunScanAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            LoadStatus = "Apply or discard Mods & Maps changes before running a scan so diagnostics match the saved preset.";
            return;
        }

        await RunScanCoreAsync(SelectedProfile.ProfileId, overwriteEditors: true);
    }

    private async Task LoadAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            Reset();
            return;
        }

        IsLoading = true;
        LoadStatus = $"Loading Mods & Maps settings for {profile.DisplayName}...";

        try
        {
            _catalog = await _hostApiClient.GetSettingsCatalogAsync(profile.ProfileId);
            var preset = await _hostApiClient.GetWorkshopPresetAsync(profile.ProfileId) ?? WorkshopPreset.Empty;
            var draft = await _hostApiClient.GetSettingsDraftAsync(profile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps);

            CatalogSummary = _catalog is null
                ? "No structured catalog available."
                : $"{_catalog.CatalogId} v{_catalog.CatalogVersion} | {_catalog.Branch}";

            ApplyPreset(preset);
            MarkClean("Loaded Mods & Maps settings from the local host.");
            Diagnostics.Clear();
            OnPropertyChanged(nameof(HasDiagnostics));
            OnPropertyChanged(nameof(HasNoDiagnostics));
            LoadStatus = "Loaded the saved workshop preset from the local host. Run a scan after applying changes to validate local workshop content.";

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
            OnPropertyChanged(nameof(CanScan));
        }
    }

    private async Task RunScanCoreAsync(string profileId, bool overwriteEditors)
    {
        var result = await _hostApiClient.ScanWorkshopAsync(profileId);
        if (result is null)
        {
            LoadStatus = "Workshop scan did not return a result.";
            return;
        }

        if (overwriteEditors)
        {
            ApplyPreset(result.Preset);
            MarkClean("Workshop scan normalized the saved preset.");
        }

        Diagnostics.Clear();
        foreach (var diagnostic in result.Diagnostics)
        {
            Diagnostics.Add(diagnostic);
        }

        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(HasNoDiagnostics));
        LoadStatus = result.Diagnostics.Count == 0
            ? "Workshop scan passed. Saved preset is present in the local workshop cache."
            : $"Workshop scan completed with {result.Diagnostics.Count} issue(s).";
    }

    private void ApplyPreset(WorkshopPreset preset)
    {
        _isApplyingState = true;
        try
        {
            WorkshopItemIdsText = string.Join(Environment.NewLine, preset.WorkshopItemIds);
            EnabledModIdsText = string.Join(Environment.NewLine, preset.EnabledModIds);
            MapFoldersText = string.Join(Environment.NewLine, preset.MapFolders);
        }
        finally
        {
            _isApplyingState = false;
        }
    }

    private void ApplyDraft(SettingsDraftDto draft)
    {
        _isApplyingState = true;
        try
        {
            WorkshopItemIdsText = GetDraftValue(draft.Values, ".mods.workshop-items");
            EnabledModIdsText = GetDraftValue(draft.Values, ".mods.enabled-mods");
            MapFoldersText = GetDraftValue(draft.Values, ".mods.map-folders");
        }
        finally
        {
            _isApplyingState = false;
        }

        if (draft.IsDirty)
        {
            MarkDirty("Loaded a saved Mods & Maps draft.");
            LoadStatus = "Loaded a saved Mods & Maps draft from SQLite-backed workspace state.";
        }
        else
        {
            MarkClean("Loaded saved Mods & Maps draft.");
        }
    }

    private IReadOnlyDictionary<string, string?> BuildDraftValues()
    {
        var prefix = SelectedProfile?.Branch.Contains("42", StringComparison.Ordinal) == true ? "b42" : "b41";
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{prefix}.mods.workshop-items"] = WorkshopItemIdsText,
            [$"{prefix}.mods.enabled-mods"] = EnabledModIdsText,
            [$"{prefix}.mods.map-folders"] = MapFoldersText,
        };
    }

    private WorkshopPreset BuildPreset() =>
        new()
        {
            WorkshopItemIds = SplitLines(WorkshopItemIdsText),
            EnabledModIds = SplitLines(EnabledModIdsText),
            MapFolders = SplitLines(MapFoldersText),
        };

    private static IReadOnlyList<string> SplitLines(string text) =>
        text.ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

    private static string GetDraftValue(IReadOnlyDictionary<string, string?> values, string suffix)
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is null ? string.Empty : values[key] ?? string.Empty;
    }

    private void Reset()
    {
        _catalog = null;
        CatalogSummary = "No structured catalog loaded.";
        Diagnostics.Clear();
        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(HasNoDiagnostics));

        _isApplyingState = true;
        try
        {
            WorkshopItemIdsText = string.Empty;
            EnabledModIdsText = string.Empty;
            MapFoldersText = string.Empty;
        }
        finally
        {
            _isApplyingState = false;
        }

        MarkClean("Mods & Maps settings are in sync.");
        OnPropertyChanged(nameof(CanScan));
    }

    partial void OnWorkshopItemIdsTextChanged(string value) => NotifyEdited();
    partial void OnEnabledModIdsTextChanged(string value) => NotifyEdited();
    partial void OnMapFoldersTextChanged(string value) => NotifyEdited();

    private void NotifyEdited()
    {
        if (_isApplyingState)
        {
            return;
        }

        MarkDirty("Unsaved changes in Mods & Maps.");
        LoadStatus = "Mods & Maps changed locally. Save a draft or apply the new preset before scanning.";
        OnPropertyChanged(nameof(CanScan));
    }
}
