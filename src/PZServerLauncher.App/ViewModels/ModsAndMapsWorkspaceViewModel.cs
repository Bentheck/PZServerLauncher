using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class ModsAndMapsWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly ILauncherRuntime _runtime;
    private bool _isApplyingState;
    private SettingsCatalogDto? _catalog;
    private WorkshopScanResultDto? _lastScanResult;
    private ProjectZomboidModsAndMapsPostureSummary _postureSummary = ProjectZomboidModsAndMapsPostureSummaryBuilder.Empty();
    private SteamWorkshopBrowserSettingsDto _workshopBrowserSettings = new(false);
    private WorkshopCatalogSearchResultDto? _workshopSearchResult;
    private WorkshopPreviewViewModel? _selectedWorkshopPreview;
    private long _searchVersion;

    public ModsAndMapsWorkspaceViewModel(MainWindowViewModel legacy, ILauncherRuntime runtime)
        : base(
            ProfileWorkspacePageIds.ModsAndMaps,
            "Mods & Maps",
            "Enabled mod IDs and map folders from the real Project Zomboid server INI.",
            "Mods & Maps settings are in sync.",
            legacy,
            ["Enabled mod load order", "Map load order", "Workshop resolution at save time", "Bulk paste and scanner diagnostics"])
    {
        _runtime = runtime;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
        ScanCommand = new AsyncRelayCommand(RunScanAsync);
        AddWorkshopEntryCommand = new RelayCommand(AddWorkshopEntry, () => CanAddWorkshopEntry);
        AddEnabledModEntryCommand = new RelayCommand(AddEnabledModEntry, () => CanAddEnabledModEntry);
        AddMapEntryCommand = new RelayCommand(AddMapEntry, () => CanAddMapEntry);
        SaveNamedPresetCommand = new AsyncRelayCommand(SaveNamedPresetAsync, () => CanSaveNamedPreset);
        LoadNamedPresetCommand = new RelayCommand<SavedPresetViewModel>(LoadNamedPreset);
        DeleteNamedPresetCommand = new AsyncRelayCommand<SavedPresetViewModel>(DeleteNamedPresetAsync);
        MoveEntryUpCommand = new RelayCommand<PresetEntryViewModel>(MoveEntryUp);
        MoveEntryDownCommand = new RelayCommand<PresetEntryViewModel>(MoveEntryDown);
        RemoveEntryCommand = new RelayCommand<PresetEntryViewModel>(RemoveEntry);
        SearchWorkshopCatalogCommand = new AsyncRelayCommand(SearchWorkshopCatalogAsync);
        PreviewWorkshopItemCommand = new AsyncRelayCommand<WorkshopCatalogItemViewModel>(PreviewWorkshopItemAsync);
        ApplyWorkshopPreviewCommand = new AsyncRelayCommand(ApplyWorkshopPreviewAsync, () => CanApplyWorkshopPreview);
        CloseWorkshopPreviewCommand = new RelayCommand(CloseWorkshopPreviewModal);
        SaveSteamWebApiKeyCommand = new AsyncRelayCommand(SaveSteamWebApiKeyAsync);
        RemoveSteamWebApiKeyCommand = new AsyncRelayCommand(RemoveSteamWebApiKeyAsync);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to manage enabled mods and maps."
        : $"Enabled mod and map settings for {SelectedProfile.DisplayName}.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string WorkspaceSummary => SelectedProfile is null
        ? "Choose a profile to unlock workshop and map management."
        : $"{SelectedProfile.DisplayName} now manages the actual Mods and Map keys through an ordered preset editor while WorkshopItems are resolved behind the scenes from the selected server mods.";

    public string ActionSummary => HasUnsavedChanges
        ? QueueIntegritySummary
        : SelectedProfile is null
            ? "Load a profile to inspect workshop, mod, and map settings."
            : ScannerSummary;

    public string WorkshopSummary => SelectedProfile is null
        ? "No enabled mods queued yet."
        : $"{EnabledModEntries.Count} enabled mod ID(s) are staged in load order.";

    public string EnabledModsSummary => SelectedProfile is null
        ? "No enabled mod IDs saved yet."
        : $"{EnabledModEntries.Count} mod ID(s) are staged in load order.";

    public string MapOrderSummary => SelectedProfile is null
        ? "No custom map folders listed."
        : $"{MapEntries.Count} map folder(s) are staged in load order.";

    public string SavedPresetSummary => PresetLibraryHeadline;

    public string ScanReadinessSummary => ValidationHeadline;

    public string ModsNextStepSummary => OperatorSummary;

    public ObservableCollection<string> Diagnostics { get; } = [];

    public ObservableCollection<PresetEntryViewModel> WorkshopEntries { get; } = [];

    public ObservableCollection<PresetEntryViewModel> EnabledModEntries { get; } = [];

    public ObservableCollection<PresetEntryViewModel> MapEntries { get; } = [];

    public ObservableCollection<SavedPresetViewModel> SavedPresets { get; } = [];
    public ObservableCollection<string> WorkshopSearchDiagnostics { get; } = [];
    public ObservableCollection<WorkshopCatalogItemViewModel> WorkshopSearchResults { get; } = [];

    public bool HasDiagnostics => Diagnostics.Count > 0;

    public bool HasNoDiagnostics => Diagnostics.Count == 0;

    public bool HasWorkshopEntries => WorkshopEntries.Count > 0;

    public bool HasNoWorkshopEntries => WorkshopEntries.Count == 0;

    public bool HasEnabledModEntries => EnabledModEntries.Count > 0;

    public bool HasNoEnabledModEntries => EnabledModEntries.Count == 0;

    public bool HasMapEntries => MapEntries.Count > 0;

    public bool HasNoMapEntries => MapEntries.Count == 0;

    public bool HasSavedPresets => SavedPresets.Count > 0;

    public bool HasNoSavedPresets => SavedPresets.Count == 0;
    public bool HasWorkshopSearchResults => WorkshopSearchResults.Count > 0;
    public bool HasNoWorkshopSearchResults => WorkshopSearchResults.Count == 0;
    public bool HasWorkshopSearchDiagnostics => WorkshopSearchDiagnostics.Count > 0;
    public bool HasNoWorkshopSearchDiagnostics => WorkshopSearchDiagnostics.Count == 0;
    public bool HasSelectedWorkshopPreview => SelectedWorkshopPreview is not null;
    public bool HasNoSelectedWorkshopPreview => SelectedWorkshopPreview is null;
    public IReadOnlyList<WorkshopCatalogSearchMode> AvailableSearchModes { get; } =
    [
        WorkshopCatalogSearchMode.Local,
        WorkshopCatalogSearchMode.Steam,
        WorkshopCatalogSearchMode.Both,
    ];
    public IReadOnlyList<WorkshopCatalogSearchFilter> AvailableSearchFilters { get; } =
    [
        WorkshopCatalogSearchFilter.All,
        WorkshopCatalogSearchFilter.Mods,
        WorkshopCatalogSearchFilter.Collections,
    ];

    public string LoadoutHeadline => _postureSummary.LoadoutHeadline;

    public string ValidationHeadline => _postureSummary.ValidationHeadline;

    public string PresetLibraryHeadline => _postureSummary.PresetLibraryHeadline;

    public string MapChainHeadline => _postureSummary.MapChainHeadline;

    public string QueueIntegritySummary => _postureSummary.QueueIntegritySummary;

    public string ScannerSummary => _postureSummary.ScannerSummary;

    public string RecoverySummary => _postureSummary.RecoverySummary;

    public string OperatorSummary => _postureSummary.OperatorSummary;

    public IReadOnlyList<ProjectZomboidModsAndMapsDiagnosticBucket> DiagnosticBuckets => _postureSummary.DiagnosticBuckets;

    public bool HasDiagnosticBuckets => DiagnosticBuckets.Count > 0;

    public bool HasNoDiagnosticBuckets => DiagnosticBuckets.Count == 0;

    public IReadOnlyList<string> ModsChecklist => _postureSummary.Checklist;

    public SavedPresetViewModel? LatestSavedPreset => SavedPresets.FirstOrDefault();

    public bool HasLatestSavedPreset => LatestSavedPreset is not null;

    public bool HasNoLatestSavedPreset => LatestSavedPreset is null;

    public string LatestSavedPresetHeadline => LatestSavedPreset is null
        ? "No named fallback captured yet."
        : $"{LatestSavedPreset.Name} saved {LatestSavedPreset.UpdatedLabel}";

    public string LatestSavedPresetComposition => LatestSavedPreset?.CompositionSummary
        ?? "Save a named preset once the live stack looks right so rollback is immediate.";

    public bool HasScanPreview => _lastScanResult is not null;

    public bool HasNoScanPreview => _lastScanResult is null;

    public string ScanPreviewHeadline => _lastScanResult is null
        ? "No normalized scan result yet."
        : $"{_lastScanResult.Preset.EnabledModIds.Count} mods | {_lastScanResult.Preset.MapFolders.Count} maps in the latest normalized snapshot.";
    public string WorkshopBrowserSummary => _workshopSearchResult is null
        ? "Search the local Workshop cache or Steam before adding new entries, including Workshop collections."
        : $"{WorkshopSearchResults.Count} result(s) ready to inspect.";
    public string WorkshopCollectionImportSummary => "To import a Workshop collection: paste the collection URL or ID into Search, click Search, open the collection preview modal, then use Add Collection To Editor and Apply Settings.";
    public string WorkshopBrowserModeSummary => SearchMode switch
    {
        WorkshopCatalogSearchMode.Local => "Local cache search",
        WorkshopCatalogSearchMode.Steam => "Steam Workshop search",
        _ => "Local + Steam search",
    };
    public bool HasSteamWebApiKeyConfigured => _workshopBrowserSettings.HasSteamWebApiKeyConfigured;
    public bool ShowSteamApiKeyEditor => !HasSteamWebApiKeyConfigured;
    public bool ShowSteamApiKeyRemoveOnly => HasSteamWebApiKeyConfigured;
    public bool ShowSteamApiKeyHelper => !HasSteamWebApiKeyConfigured && SearchMode is WorkshopCatalogSearchMode.Steam or WorkshopCatalogSearchMode.Both;
    public bool ShowSteamApiKeyConfiguredBanner => HasSteamWebApiKeyConfigured;
    public bool CanApplyWorkshopPreview => SelectedWorkshopPreview is not null && SelectedWorkshopPreview.HasChanges;
    public string SelectedWorkshopApplyLabel => SelectedWorkshopPreview?.Item.IsCollection == true ? "Add Collection To Editor" : "Add To Editor";
    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    public IAsyncRelayCommand ScanCommand { get; }

    public IRelayCommand AddWorkshopEntryCommand { get; }

    public IRelayCommand AddEnabledModEntryCommand { get; }

    public IRelayCommand AddMapEntryCommand { get; }

    public IAsyncRelayCommand SaveNamedPresetCommand { get; }

    public IRelayCommand<SavedPresetViewModel> LoadNamedPresetCommand { get; }

    public IAsyncRelayCommand<SavedPresetViewModel> DeleteNamedPresetCommand { get; }

    public IRelayCommand<PresetEntryViewModel> MoveEntryUpCommand { get; }

    public IRelayCommand<PresetEntryViewModel> MoveEntryDownCommand { get; }

    public IRelayCommand<PresetEntryViewModel> RemoveEntryCommand { get; }
    public IAsyncRelayCommand SearchWorkshopCatalogCommand { get; }
    public IAsyncRelayCommand<WorkshopCatalogItemViewModel> PreviewWorkshopItemCommand { get; }
    public IAsyncRelayCommand ApplyWorkshopPreviewCommand { get; }
    public IRelayCommand CloseWorkshopPreviewCommand { get; }
    public IAsyncRelayCommand SaveSteamWebApiKeyCommand { get; }
    public IAsyncRelayCommand RemoveSteamWebApiKeyCommand { get; }

    public bool CanScan => SelectedProfile is not null && !HasUnsavedChanges;

    public bool CanAddWorkshopEntry => !string.IsNullOrWhiteSpace(NewWorkshopEntry);

    public bool CanAddEnabledModEntry => !string.IsNullOrWhiteSpace(NewEnabledModEntry);

    public bool CanAddMapEntry => !string.IsNullOrWhiteSpace(NewMapEntry);

    public bool CanSaveNamedPreset => SelectedProfile is not null && !string.IsNullOrWhiteSpace(NewPresetName);

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
    private string newWorkshopEntry = string.Empty;

    [ObservableProperty]
    private string newEnabledModEntry = string.Empty;

    [ObservableProperty]
    private string newMapEntry = string.Empty;

    [ObservableProperty]
    private string newPresetName = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private WorkshopCatalogSearchMode searchMode = WorkshopCatalogSearchMode.Both;

    [ObservableProperty]
    private WorkshopCatalogSearchFilter searchFilter = WorkshopCatalogSearchFilter.All;

    [ObservableProperty]
    private bool isWorkshopPreviewModalOpen;

    [ObservableProperty]
    private string steamWebApiKey = string.Empty;

    public WorkshopPreviewViewModel? SelectedWorkshopPreview
    {
        get => _selectedWorkshopPreview;
        private set
        {
            if (ReferenceEquals(_selectedWorkshopPreview, value))
            {
                return;
            }

            _selectedWorkshopPreview?.Dispose();
            _selectedWorkshopPreview = value;
            if (value is null)
            {
                IsWorkshopPreviewModalOpen = false;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedWorkshopPreview));
            OnPropertyChanged(nameof(HasNoSelectedWorkshopPreview));
            OnPropertyChanged(nameof(CanApplyWorkshopPreview));
            OnPropertyChanged(nameof(SelectedWorkshopApplyLabel));
            ApplyWorkshopPreviewCommand.NotifyCanExecuteChanged();
        }
    }

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        _ = LoadAsync(profile);
        NotifyComputedState();
    }

    public override async Task RefreshPageAsync()
    {
        await LoadAsync(SelectedProfile);
    }

    public override async Task SaveDraftAsync()
    {
        if (SelectedProfile is null || _catalog is null)
        {
            return;
        }

        var payload = new SettingsDraftDto(
            SelectedProfile.ProfileId,
            ProjectZomboidBranch.Unstable42,
            _catalog.CatalogId,
            _catalog.CatalogVersion,
            ProfileWorkspacePageIds.ModsAndMaps,
            BuildDraftValues(),
            null,
            true,
            DateTimeOffset.UtcNow);

        await _runtime.SaveSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps, payload);
        MarkClean("Saved Mods & Maps draft.");
        LoadStatus = "Saved a Mods & Maps draft. Apply settings to update the active profile preset.";
        NotifyComputedState();
    }

    public override async Task DiscardDraftAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            await _runtime.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps);
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

        var updatedPreset = await _runtime.UpdateWorkshopPresetAsync(SelectedProfile.ProfileId, BuildPresetForSave());
        if (updatedPreset is null)
        {
            LoadStatus = "Mods & Maps settings could not be saved.";
            return;
        }

        try
        {
            await _runtime.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps);
        }
        catch
        {
        }

        ApplyPreset(updatedPreset);
        MarkClean($"Saved Mods & Maps settings for {SelectedProfile.DisplayName}.");
        await RunScanCoreAsync(SelectedProfile.ProfileId, overwriteEditors: true);
        await Legacy.RefreshCommand.ExecuteAsync(null);
        NotifyComputedState();
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
            _catalog = await _runtime.GetSettingsCatalogAsync(profile.ProfileId);
            _workshopBrowserSettings = await _runtime.GetWorkshopBrowserSettingsAsync() ?? new SteamWorkshopBrowserSettingsDto(false);
            var preset = await _runtime.GetWorkshopPresetAsync(profile.ProfileId) ?? WorkshopPreset.Empty;
            var savedPresets = await _runtime.GetNamedWorkshopPresetsAsync(profile.ProfileId) ?? [];
            var draft = await _runtime.GetSettingsDraftAsync(profile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps);

            CatalogSummary = _catalog is null
                ? "No structured catalog available."
                : $"{_catalog.CatalogId} v{_catalog.CatalogVersion} | {_catalog.Branch}";

            ApplyPreset(preset);
            SteamWebApiKey = string.Empty;
            _lastScanResult = null;
            ReplaceSavedPresets(savedPresets);
            ResetWorkshopBrowserState();
            MarkClean("Loaded Mods & Maps settings from the local host.");
            Diagnostics.Clear();
            OnPropertyChanged(nameof(HasDiagnostics));
            OnPropertyChanged(nameof(HasNoDiagnostics));
            LoadStatus = "Loaded Mods and Map from the local host. Run a scan after applying changes to validate local workshop content.";
            NotifyComputedState();

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
        var result = await _runtime.ScanWorkshopAsync(profileId);
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

        _lastScanResult = result;
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
        NotifyComputedState();
    }

    private async Task SaveNamedPresetAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var saved = await _runtime.SaveNamedWorkshopPresetAsync(SelectedProfile.ProfileId, NewPresetName, BuildPresetForNamedPreset());
            if (saved is null)
            {
                LoadStatus = "Named preset could not be saved.";
                return;
            }

            UpsertSavedPreset(saved);
            NewPresetName = string.Empty;
            LoadStatus = $"Saved named preset '{saved.Name}'.";
            NotifyComputedState();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
    }

    private void LoadNamedPreset(SavedPresetViewModel? preset)
    {
        if (preset is null)
        {
            return;
        }

        ApplyPreset(preset.Preset);
        NotifyEdited($"Loaded named preset '{preset.Name}' into the editor. Apply settings to push it live.");
    }

    private async Task DeleteNamedPresetAsync(SavedPresetViewModel? preset)
    {
        if (SelectedProfile is null || preset is null)
        {
            return;
        }

        try
        {
            await _runtime.DeleteNamedWorkshopPresetAsync(SelectedProfile.ProfileId, preset.PresetId);
            SavedPresets.Remove(preset);
            LoadStatus = $"Deleted named preset '{preset.Name}'.";
            NotifyComputedState();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
    }

    private async Task SearchWorkshopCatalogAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var result = await _runtime.SearchWorkshopCatalogAsync(
                SelectedProfile.ProfileId,
                new WorkshopCatalogSearchRequestDto(SearchQuery, SearchMode, 12, BuildPresetForWorkshopOperations(), SearchFilter),
                CancellationToken.None);

            _workshopSearchResult = result;
            ReplaceWorkshopSearchResults(result?.Results ?? []);
            WorkshopSearchDiagnostics.Clear();
            foreach (var diagnostic in result?.Diagnostics ?? [])
            {
                WorkshopSearchDiagnostics.Add(diagnostic);
            }

            SelectedWorkshopPreview = null;
            IsWorkshopPreviewModalOpen = false;
            LoadStatus = result is null
                ? "Workshop search did not return a result."
                : result.Results.Count == 0
                    ? "No Workshop results matched the current search."
                    : $"Loaded {result.Results.Count} Workshop result(s).";
            RefreshWorkshopBrowserState();
            NotifyComputedState();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
    }

    private async Task PreviewWorkshopItemAsync(WorkshopCatalogItemViewModel? item)
    {
        if (SelectedProfile is null || item is null)
        {
            return;
        }

        try
        {
            var preview = await _runtime.GetWorkshopCatalogPreviewAsync(
                SelectedProfile.ProfileId,
                item.WorkshopId,
                new WorkshopCatalogPreviewRequestDto(SearchMode, BuildPresetForWorkshopOperations()),
                CancellationToken.None);

            SelectedWorkshopPreview = preview is null
                ? null
                : await WorkshopPreviewViewModel.CreateAsync(preview, _runtime, CancellationToken.None);
            IsWorkshopPreviewModalOpen = SelectedWorkshopPreview is not null;
            LoadStatus = SelectedWorkshopPreview is null
                ? $"Workshop item {item.WorkshopId} could not be resolved."
                : SelectedWorkshopPreview.Item.IsCollection
                    ? $"Opened collection preview for {SelectedWorkshopPreview.Title}."
                    : $"Loaded Workshop preview for {SelectedWorkshopPreview.Title}.";
            RefreshWorkshopBrowserState();
            NotifyComputedState();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
    }

    private async Task ApplyWorkshopPreviewAsync()
    {
        if (SelectedWorkshopPreview is null)
        {
            return;
        }

        var merged = WorkshopPresetMergeHelper.Append(
            BuildPresetForWorkshopOperations(),
            SelectedWorkshopPreview.WorkshopItemIdsToAdd,
            SelectedWorkshopPreview.ModIdsToAdd,
            SelectedWorkshopPreview.MapFoldersToAdd);

        ApplyPreset(merged);
        CloseWorkshopPreviewModal();
        NotifyEdited(
            SelectedWorkshopPreview.Item.IsCollection
                ? $"Added collection {SelectedWorkshopPreview.Title} to the editor. Apply settings to push it live."
                : $"Added {SelectedWorkshopPreview.Title} to the editor. Apply settings to push it live.");
        await Task.CompletedTask;
    }

    private void CloseWorkshopPreviewModal()
    {
        IsWorkshopPreviewModalOpen = false;
    }

    private async Task SaveSteamWebApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(SteamWebApiKey))
        {
            LoadStatus = "Paste a Steam Web API key, then save it.";
            NotifyComputedState();
            return;
        }

        try
        {
            _workshopBrowserSettings = await _runtime.SetSteamWebApiKeyAsync(SteamWebApiKey, CancellationToken.None)
                ?? new SteamWorkshopBrowserSettingsDto(false);
            SteamWebApiKey = string.Empty;
            LoadStatus = "Steam Web API key saved for Workshop search.";
            NotifyComputedState();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
    }

    private async Task RemoveSteamWebApiKeyAsync()
    {
        try
        {
            _workshopBrowserSettings = await _runtime.RemoveSteamWebApiKeyAsync(CancellationToken.None)
                ?? new SteamWorkshopBrowserSettingsDto(false);
            SteamWebApiKey = string.Empty;
            LoadStatus = "Steam Web API key removed. Local/manual Workshop browsing is still available.";
            NotifyComputedState();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
    }

    private void ApplyPreset(WorkshopPreset preset)
    {
        _isApplyingState = true;
        try
        {
            WorkshopItemIdsText = string.Join(Environment.NewLine, preset.WorkshopItemIds);
            EnabledModIdsText = string.Join(Environment.NewLine, preset.EnabledModIds);
            MapFoldersText = string.Join(Environment.NewLine, preset.MapFolders);
            NewWorkshopEntry = string.Empty;
            NewEnabledModEntry = string.Empty;
            NewMapEntry = string.Empty;
        }
        finally
        {
            _isApplyingState = false;
        }

        RebuildEntryCollections();
        RefreshWorkshopBrowserState();
    }

    private void ApplyDraft(SettingsDraftDto draft)
    {
        _isApplyingState = true;
        try
        {
            EnabledModIdsText = GetDraftValue(draft.Values, ".mods.enabled-mods");
            MapFoldersText = GetDraftValue(draft.Values, ".mods.map-folders");
            NewWorkshopEntry = string.Empty;
            NewEnabledModEntry = string.Empty;
            NewMapEntry = string.Empty;
        }
        finally
        {
            _isApplyingState = false;
        }

        RebuildEntryCollections();

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
        const string prefix = "b42";
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{prefix}.mods.enabled-mods"] = EnabledModIdsText,
            [$"{prefix}.mods.map-folders"] = MapFoldersText,
        };
    }

    private WorkshopPreset BuildPresetForSave() =>
        new()
        {
            WorkshopItemIds = SplitLines(WorkshopItemIdsText),
            EnabledModIds = SplitLines(EnabledModIdsText),
            MapFolders = SplitLines(MapFoldersText),
        };

    private WorkshopPreset BuildPresetForNamedPreset() =>
        new()
        {
            WorkshopItemIds = [],
            EnabledModIds = SplitLines(EnabledModIdsText),
            MapFolders = SplitLines(MapFoldersText),
        };

    private WorkshopPreset BuildPresetForWorkshopOperations() =>
        new()
        {
            WorkshopItemIds = SplitLines(WorkshopItemIdsText),
            EnabledModIds = SplitLines(EnabledModIdsText),
            MapFolders = SplitLines(MapFoldersText),
        };

    private static IReadOnlyList<string> SplitLines(string text) =>
        text.ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(line => line.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

    private static string GetDraftValue(IReadOnlyDictionary<string, string?> values, string suffix)
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is null ? string.Empty : values[key] ?? string.Empty;
    }

    private void AddWorkshopEntry()
    {
        AddEntry(PresetEntryKind.Workshop, NewWorkshopEntry);
        NewWorkshopEntry = string.Empty;
        AddWorkshopEntryCommand.NotifyCanExecuteChanged();
    }

    private void AddEnabledModEntry()
    {
        AddEntry(PresetEntryKind.EnabledMod, NewEnabledModEntry);
        NewEnabledModEntry = string.Empty;
        AddEnabledModEntryCommand.NotifyCanExecuteChanged();
    }

    private void AddMapEntry()
    {
        AddEntry(PresetEntryKind.MapFolder, NewMapEntry);
        NewMapEntry = string.Empty;
        AddMapEntryCommand.NotifyCanExecuteChanged();
    }

    private void AddEntry(PresetEntryKind kind, string rawValue)
    {
        var value = rawValue.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var values = GetValues(kind).ToList();
        values.Add(value);
        ReplaceEntries(kind, values);
        NotifyEdited($"Added a new {GetKindLabel(kind).ToLowerInvariant()} entry.");
    }

    private void MoveEntryUp(PresetEntryViewModel? entry)
    {
        if (entry is null || entry.Position <= 0)
        {
            return;
        }

        var values = GetValues(entry.Kind).ToList();
        (values[entry.Position - 1], values[entry.Position]) = (values[entry.Position], values[entry.Position - 1]);
        ReplaceEntries(entry.Kind, values);
        NotifyEdited($"Moved {GetKindLabel(entry.Kind).ToLowerInvariant()} entry up.");
    }

    private void MoveEntryDown(PresetEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        var values = GetValues(entry.Kind).ToList();
        if (entry.Position < 0 || entry.Position >= values.Count - 1)
        {
            return;
        }

        (values[entry.Position + 1], values[entry.Position]) = (values[entry.Position], values[entry.Position + 1]);
        ReplaceEntries(entry.Kind, values);
        NotifyEdited($"Moved {GetKindLabel(entry.Kind).ToLowerInvariant()} entry down.");
    }

    private void RemoveEntry(PresetEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        var values = GetValues(entry.Kind).ToList();
        if (entry.Position < 0 || entry.Position >= values.Count)
        {
            return;
        }

        values.RemoveAt(entry.Position);
        ReplaceEntries(entry.Kind, values);
        NotifyEdited($"Removed a {GetKindLabel(entry.Kind).ToLowerInvariant()} entry.");
    }

    private void ReplaceEntries(PresetEntryKind kind, IReadOnlyList<string> values)
    {
        _isApplyingState = true;
        try
        {
            SetText(kind, string.Join(Environment.NewLine, values));
        }
        finally
        {
            _isApplyingState = false;
        }

        RebuildEntryCollections();
    }

    private IReadOnlyList<string> GetValues(PresetEntryKind kind) =>
        kind switch
        {
            PresetEntryKind.Workshop => SplitLines(WorkshopItemIdsText),
            PresetEntryKind.EnabledMod => SplitLines(EnabledModIdsText),
            PresetEntryKind.MapFolder => SplitLines(MapFoldersText),
            _ => [],
        };

    private void SetText(PresetEntryKind kind, string text)
    {
        switch (kind)
        {
            case PresetEntryKind.Workshop:
                WorkshopItemIdsText = text;
                break;
            case PresetEntryKind.EnabledMod:
                EnabledModIdsText = text;
                break;
            case PresetEntryKind.MapFolder:
                MapFoldersText = text;
                break;
        }
    }

    private void RebuildEntryCollections()
    {
        ReplaceCollection(WorkshopEntries, SplitLines(WorkshopItemIdsText), PresetEntryKind.Workshop);
        ReplaceCollection(EnabledModEntries, SplitLines(EnabledModIdsText), PresetEntryKind.EnabledMod);
        ReplaceCollection(MapEntries, SplitLines(MapFoldersText), PresetEntryKind.MapFolder);

        OnPropertyChanged(nameof(HasWorkshopEntries));
        OnPropertyChanged(nameof(HasNoWorkshopEntries));
        OnPropertyChanged(nameof(HasEnabledModEntries));
        OnPropertyChanged(nameof(HasNoEnabledModEntries));
        OnPropertyChanged(nameof(HasMapEntries));
        OnPropertyChanged(nameof(HasNoMapEntries));
        OnPropertyChanged(nameof(WorkshopSummary));
        OnPropertyChanged(nameof(EnabledModsSummary));
        OnPropertyChanged(nameof(MapOrderSummary));
        OnPropertyChanged(nameof(ScanReadinessSummary));
        OnPropertyChanged(nameof(ModsNextStepSummary));
    }

    private static void ReplaceCollection(
        ObservableCollection<PresetEntryViewModel> target,
        IReadOnlyList<string> values,
        PresetEntryKind kind)
    {
        target.Clear();
        for (var index = 0; index < values.Count; index++)
        {
            target.Add(new PresetEntryViewModel(kind, index, values[index]));
        }
    }

    private void ReplaceWorkshopSearchResults(IReadOnlyList<WorkshopCatalogItemDto> items)
    {
        DisposeWorkshopSearchResults();
        WorkshopSearchResults.Clear();
        foreach (var item in items)
        {
            WorkshopSearchResults.Add(new WorkshopCatalogItemViewModel(item));
        }

        _searchVersion++;
        _ = LoadWorkshopSearchImagesAsync(_searchVersion);
        OnPropertyChanged(nameof(HasWorkshopSearchResults));
        OnPropertyChanged(nameof(HasNoWorkshopSearchResults));
        OnPropertyChanged(nameof(HasWorkshopSearchDiagnostics));
        OnPropertyChanged(nameof(HasNoWorkshopSearchDiagnostics));
    }

    private async Task LoadWorkshopSearchImagesAsync(long searchVersion)
    {
        foreach (var item in WorkshopSearchResults.ToArray())
        {
            if (searchVersion != _searchVersion)
            {
                return;
            }

            await item.LoadImageAsync(_runtime, CancellationToken.None);
        }
    }

    private void DisposeWorkshopSearchResults()
    {
        foreach (var item in WorkshopSearchResults)
        {
            item.Dispose();
        }
    }

    private void ResetWorkshopBrowserState()
    {
        _workshopSearchResult = null;
        DisposeWorkshopSearchResults();
        WorkshopSearchResults.Clear();
        WorkshopSearchDiagnostics.Clear();
        SelectedWorkshopPreview = null;
        _searchVersion++;
        OnPropertyChanged(nameof(HasWorkshopSearchResults));
        OnPropertyChanged(nameof(HasNoWorkshopSearchResults));
        OnPropertyChanged(nameof(HasWorkshopSearchDiagnostics));
        OnPropertyChanged(nameof(HasNoWorkshopSearchDiagnostics));
    }

    private void RefreshWorkshopBrowserState()
    {
        var preset = BuildPresetForWorkshopOperations();
        foreach (var item in WorkshopSearchResults)
        {
            item.IsQueued = IsWorkshopCatalogItemQueued(preset, item.Item);
        }

        if (SelectedWorkshopPreview is not null)
        {
            SelectedWorkshopPreview.RefreshAgainstPreset(preset);
        }

        OnPropertyChanged(nameof(CanApplyWorkshopPreview));
        ApplyWorkshopPreviewCommand.NotifyCanExecuteChanged();
    }

    private static bool IsWorkshopCatalogItemQueued(WorkshopPreset preset, WorkshopCatalogItemDto item) =>
        item.Kind is WorkshopCatalogItemKind.Collection
            ? item.CollectionChildWorkshopIds is { Count: > 0 } children &&
              children.All(id => preset.WorkshopItemIds.Any(value => string.Equals(value, id, StringComparison.OrdinalIgnoreCase)))
            : WorkshopPresetMergeHelper.IsQueued(preset, item.WorkshopId, item.ModIds, item.MapFolders);

    private void Reset()
    {
        _catalog = null;
        CatalogSummary = "No structured catalog loaded.";
        Diagnostics.Clear();
        WorkshopEntries.Clear();
        EnabledModEntries.Clear();
        MapEntries.Clear();
        SavedPresets.Clear();
        ResetWorkshopBrowserState();
        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(HasNoDiagnostics));

        _isApplyingState = true;
        try
        {
            WorkshopItemIdsText = string.Empty;
            EnabledModIdsText = string.Empty;
            MapFoldersText = string.Empty;
            NewWorkshopEntry = string.Empty;
            NewEnabledModEntry = string.Empty;
            NewMapEntry = string.Empty;
            NewPresetName = string.Empty;
            SearchQuery = string.Empty;
            SteamWebApiKey = string.Empty;
            SearchMode = WorkshopCatalogSearchMode.Both;
            SearchFilter = WorkshopCatalogSearchFilter.All;
        }
        finally
        {
            _isApplyingState = false;
        }

        MarkClean("Mods & Maps settings are in sync.");
        NotifyComputedState();
    }

    partial void OnWorkshopItemIdsTextChanged(string value) => NotifyTextEdited();
    partial void OnEnabledModIdsTextChanged(string value) => NotifyTextEdited();
    partial void OnMapFoldersTextChanged(string value) => NotifyTextEdited();

    partial void OnNewWorkshopEntryChanged(string value)
    {
        AddWorkshopEntryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddWorkshopEntry));
    }

    partial void OnNewEnabledModEntryChanged(string value)
    {
        AddEnabledModEntryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddEnabledModEntry));
    }

    partial void OnNewMapEntryChanged(string value)
    {
        AddMapEntryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddMapEntry));
    }

    partial void OnNewPresetNameChanged(string value)
    {
        SaveNamedPresetCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSaveNamedPreset));
    }

    partial void OnSearchModeChanged(WorkshopCatalogSearchMode value)
    {
        OnPropertyChanged(nameof(WorkshopBrowserModeSummary));
        OnPropertyChanged(nameof(ShowSteamApiKeyEditor));
        OnPropertyChanged(nameof(ShowSteamApiKeyRemoveOnly));
        OnPropertyChanged(nameof(ShowSteamApiKeyHelper));
        OnPropertyChanged(nameof(ShowSteamApiKeyConfiguredBanner));
    }

    partial void OnSearchFilterChanged(WorkshopCatalogSearchFilter value)
    {
    }

    partial void OnSteamWebApiKeyChanged(string value)
    {
    }

    private void NotifyTextEdited()
    {
        if (_isApplyingState)
        {
            return;
        }

        RebuildEntryCollections();
        RefreshWorkshopBrowserState();
        NotifyEdited("Mods & Maps changed locally. Save a draft or apply the new preset before scanning.");
    }

    private void NotifyEdited(string statusMessage)
    {
        MarkDirty("Unsaved changes in Mods & Maps.");
        LoadStatus = statusMessage;
        NotifyComputedState();
    }

    private void NotifyComputedState()
    {
        RefreshPosture();
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(ActionSummary));
        OnPropertyChanged(nameof(WorkshopSummary));
        OnPropertyChanged(nameof(EnabledModsSummary));
        OnPropertyChanged(nameof(MapOrderSummary));
        OnPropertyChanged(nameof(SavedPresetSummary));
        OnPropertyChanged(nameof(ScanReadinessSummary));
        OnPropertyChanged(nameof(ModsNextStepSummary));
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanAddWorkshopEntry));
        OnPropertyChanged(nameof(CanAddEnabledModEntry));
        OnPropertyChanged(nameof(CanAddMapEntry));
        OnPropertyChanged(nameof(CanSaveNamedPreset));
        OnPropertyChanged(nameof(HasWorkshopEntries));
        OnPropertyChanged(nameof(HasNoWorkshopEntries));
        OnPropertyChanged(nameof(HasEnabledModEntries));
        OnPropertyChanged(nameof(HasNoEnabledModEntries));
        OnPropertyChanged(nameof(HasMapEntries));
        OnPropertyChanged(nameof(HasNoMapEntries));
        OnPropertyChanged(nameof(HasSavedPresets));
        OnPropertyChanged(nameof(HasNoSavedPresets));
        OnPropertyChanged(nameof(LoadoutHeadline));
        OnPropertyChanged(nameof(ValidationHeadline));
        OnPropertyChanged(nameof(PresetLibraryHeadline));
        OnPropertyChanged(nameof(MapChainHeadline));
        OnPropertyChanged(nameof(QueueIntegritySummary));
        OnPropertyChanged(nameof(ScannerSummary));
        OnPropertyChanged(nameof(RecoverySummary));
        OnPropertyChanged(nameof(OperatorSummary));
        OnPropertyChanged(nameof(DiagnosticBuckets));
        OnPropertyChanged(nameof(HasDiagnosticBuckets));
        OnPropertyChanged(nameof(HasNoDiagnosticBuckets));
        OnPropertyChanged(nameof(ModsChecklist));
        OnPropertyChanged(nameof(LatestSavedPreset));
        OnPropertyChanged(nameof(HasLatestSavedPreset));
        OnPropertyChanged(nameof(HasNoLatestSavedPreset));
        OnPropertyChanged(nameof(LatestSavedPresetHeadline));
        OnPropertyChanged(nameof(LatestSavedPresetComposition));
        OnPropertyChanged(nameof(HasScanPreview));
        OnPropertyChanged(nameof(HasNoScanPreview));
        OnPropertyChanged(nameof(ScanPreviewHeadline));
        OnPropertyChanged(nameof(WorkshopBrowserSummary));
        OnPropertyChanged(nameof(WorkshopCollectionImportSummary));
        OnPropertyChanged(nameof(WorkshopBrowserModeSummary));
        OnPropertyChanged(nameof(HasSteamWebApiKeyConfigured));
        OnPropertyChanged(nameof(ShowSteamApiKeyEditor));
        OnPropertyChanged(nameof(ShowSteamApiKeyRemoveOnly));
        OnPropertyChanged(nameof(ShowSteamApiKeyHelper));
        OnPropertyChanged(nameof(ShowSteamApiKeyConfiguredBanner));
        OnPropertyChanged(nameof(HasWorkshopSearchResults));
        OnPropertyChanged(nameof(HasNoWorkshopSearchResults));
        OnPropertyChanged(nameof(HasWorkshopSearchDiagnostics));
        OnPropertyChanged(nameof(HasNoWorkshopSearchDiagnostics));
        OnPropertyChanged(nameof(CanApplyWorkshopPreview));
        OnPropertyChanged(nameof(SelectedWorkshopApplyLabel));
    }

    private void ReplaceSavedPresets(IReadOnlyList<NamedWorkshopPresetDto> presets)
    {
        SavedPresets.Clear();
        foreach (var preset in presets.OrderByDescending(item => item.UpdatedAtUtc))
        {
            SavedPresets.Add(new SavedPresetViewModel(
                preset.PresetId,
                preset.Name,
                preset.Preset,
                preset.UpdatedAtUtc,
                $"{preset.Preset.EnabledModIds.Count} mods | {preset.Preset.MapFolders.Count} maps"));
        }
    }

    private void UpsertSavedPreset(NamedWorkshopPresetDto preset)
    {
        var existing = SavedPresets.FirstOrDefault(item => item.PresetId == preset.PresetId);
        if (existing is not null)
        {
            SavedPresets.Remove(existing);
        }

        SavedPresets.Insert(0, new SavedPresetViewModel(
            preset.PresetId,
            preset.Name,
            preset.Preset,
            preset.UpdatedAtUtc,
            $"{preset.Preset.EnabledModIds.Count} mods | {preset.Preset.MapFolders.Count} maps"));
    }

    private void RefreshPosture()
    {
        _postureSummary = ProjectZomboidModsAndMapsPostureSummaryBuilder.Build(
            BuildPresetForWorkshopOperations(),
            _lastScanResult,
            SavedPresets.Count,
            installDetected: SelectedProfile is not null && Directory.Exists(SelectedProfile.InstallDirectory),
            cacheDetected: SelectedProfile is not null && Directory.Exists(SelectedProfile.CacheDirectory),
            hasUnsavedChanges: HasUnsavedChanges);
    }

    private static string GetKindLabel(PresetEntryKind kind) =>
        kind switch
        {
            PresetEntryKind.Workshop => "Workshop",
            PresetEntryKind.EnabledMod => "Mod",
            PresetEntryKind.MapFolder => "Map",
            _ => "Preset",
        };

    public sealed class PresetEntryViewModel(PresetEntryKind kind, int position, string value)
    {
        public PresetEntryKind Kind { get; } = kind;

        public int Position { get; } = position;

        public string OrderLabel => $"{Position + 1:00}";

        public string Value { get; } = value;
    }

    public sealed class SavedPresetViewModel(
        Guid presetId,
        string name,
        WorkshopPreset preset,
        DateTimeOffset updatedAtUtc,
        string compositionSummary)
    {
        public Guid PresetId { get; } = presetId;

        public string Name { get; } = name;

        public WorkshopPreset Preset { get; } = preset;

        public DateTimeOffset UpdatedAtUtc { get; } = updatedAtUtc;

        public string UpdatedLabel => UpdatedAtUtc.ToLocalTime().ToString("g");

        public string CompositionSummary { get; } = compositionSummary;
    }

    public sealed partial class WorkshopCatalogItemViewModel : ObservableObject, IDisposable
    {
        private readonly string? _previewImageUrl;

        public WorkshopCatalogItemViewModel(WorkshopCatalogItemDto item)
        {
            Item = item;
            _previewImageUrl = item.PreviewImageUrl;
            IsQueued = item.IsQueued;
        }

        public WorkshopCatalogItemDto Item { get; }

        public string WorkshopId => Item.WorkshopId;

        public string Title => Item.Title;

        public string Description => Item.Description;

        public string SourceLabel => Item.Source.ToString();

        public bool IsInstalledLocally => Item.IsInstalledLocally;

        public WorkshopCatalogItemKind Kind => Item.Kind;

        public bool IsCollection => Item.Kind is WorkshopCatalogItemKind.Collection;

        public string KindLabel => IsCollection ? "Collection" : "Item";

        public int CollectionItemCount => Item.CollectionItemCount;

        public IReadOnlyList<string> CollectionChildWorkshopIds => Item.CollectionChildWorkshopIds ?? [];
        public string PreviewLabel => IsCollection ? "Preview Collection" : "Preview";

        public IReadOnlyList<string> ModIds => Item.ModIds;

        public IReadOnlyList<string> MapFolders => Item.MapFolders;

        [ObservableProperty]
        private Bitmap? previewImage;

        [ObservableProperty]
        private bool isQueued;

        public bool HasPreviewImage => PreviewImage is not null;

        public async Task LoadImageAsync(ILauncherRuntime runtime, CancellationToken cancellationToken)
        {
            if (PreviewImage is not null || string.IsNullOrWhiteSpace(_previewImageUrl))
            {
                return;
            }

            try
            {
                var bytes = await runtime.DownloadWorkshopImageAsync(_previewImageUrl, cancellationToken);
                if (bytes is null || bytes.Length == 0)
                {
                    return;
                }

                await using var stream = new MemoryStream(bytes);
                PreviewImage = new Bitmap(stream);
                OnPropertyChanged(nameof(HasPreviewImage));
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            PreviewImage?.Dispose();
            PreviewImage = null;
        }
    }

    public sealed partial class WorkshopPreviewViewModel : ObservableObject, IDisposable
    {
        private WorkshopPreviewViewModel(WorkshopCatalogItemViewModel item)
        {
            Item = item;
        }

        public WorkshopCatalogItemViewModel Item { get; }

        public string Title => Item.Title;

        public string Description => Item.Description;

        public string WorkshopId => Item.WorkshopId;

        public string SourceLabel => Item.SourceLabel;

        public Bitmap? PreviewImage => Item.PreviewImage;

        public bool HasPreviewImage => Item.HasPreviewImage;

        public IReadOnlyList<string> ModIds => Item.ModIds;

        public IReadOnlyList<string> MapFolders => Item.MapFolders;

        public IReadOnlyList<WorkshopCatalogPreviewChildDto> CollectionChildren { get; private set; } = [];

        public bool HasCollectionChildren => CollectionChildren.Count > 0;

        [ObservableProperty]
        private IReadOnlyList<string> workshopItemIdsToAdd = [];

        [ObservableProperty]
        private IReadOnlyList<string> modIdsToAdd = [];

        [ObservableProperty]
        private IReadOnlyList<string> mapFoldersToAdd = [];

        public bool HasChanges => WorkshopItemIdsToAdd.Count > 0 || ModIdsToAdd.Count > 0 || MapFoldersToAdd.Count > 0;
        public string ApplyLabel => Item.IsCollection ? "Add Collection To Editor" : "Add To Editor";

        public static async Task<WorkshopPreviewViewModel> CreateAsync(
            WorkshopCatalogPreviewDto preview,
            ILauncherRuntime runtime,
            CancellationToken cancellationToken)
        {
            var item = new WorkshopCatalogItemViewModel(preview.Item);
            await item.LoadImageAsync(runtime, cancellationToken);
            var viewModel = new WorkshopPreviewViewModel(item)
            {
                WorkshopItemIdsToAdd = preview.WorkshopItemIdsToAdd,
                ModIdsToAdd = preview.ModIdsToAdd,
                MapFoldersToAdd = preview.MapFoldersToAdd,
                CollectionChildren = preview.CollectionChildren ?? [],
            };
            return viewModel;
        }

        public void RefreshAgainstPreset(WorkshopPreset preset)
        {
            var collectionChildWorkshopIds = CollectionChildren
                .Select(child => child.WorkshopId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            WorkshopItemIdsToAdd = Item.IsCollection
                ? collectionChildWorkshopIds
                    .Where(id => !preset.WorkshopItemIds.Any(value => string.Equals(value, id, StringComparison.OrdinalIgnoreCase)))
                    .ToArray()
                : preset.WorkshopItemIds.Any(value => string.Equals(value, Item.WorkshopId, StringComparison.OrdinalIgnoreCase))
                    ? []
                    : [Item.WorkshopId];
            ModIdsToAdd = Item.ModIds
                .Where(value => !preset.EnabledModIds.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            MapFoldersToAdd = Item.MapFolders
                .Where(value => !preset.MapFolders.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            CollectionChildren = CollectionChildren
                .Select(child => child with
                {
                    IsQueued = preset.WorkshopItemIds.Any(value => string.Equals(value, child.WorkshopId, StringComparison.OrdinalIgnoreCase)),
                })
                .ToArray();
            OnPropertyChanged(nameof(CollectionChildren));
            OnPropertyChanged(nameof(HasCollectionChildren));
            OnPropertyChanged(nameof(HasChanges));
        }

        public void Dispose() => Item.Dispose();
    }

    public enum PresetEntryKind
    {
        Workshop,
        EnabledMod,
        MapFolder,
    }
}
