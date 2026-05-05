using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
    private SettingsCatalogDto? _catalog;
    private WorkshopScanResultDto? _lastScanResult;
    private SteamWorkshopBrowserSettingsDto _workshopBrowserSettings = new(false);
    private WorkshopCatalogSearchResultDto? _workshopSearchResult;
    private WorkshopPreviewViewModel? _selectedWorkshopPreview;
    private WorkshopPreviewViewModel? _selectedEditorPreview;
    private long _searchVersion;
    private bool _isApplyingState;
    private bool _isApplyingWorkshopPreview;
    private bool _hasStoredDraft;
    private string _livePresetHash = string.Empty;
    private List<string> _draftWorkshopItemIds = [];
    private int _nextModRowId = 1;
    private int _nextMapRowId = 1;
    private readonly Dictionary<string, WorkshopMetadata> _metadataByModId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorkshopMetadata> _metadataByMapId = new(StringComparer.OrdinalIgnoreCase);

    public ModsAndMapsWorkspaceViewModel(MainWindowViewModel legacy, ILauncherRuntime runtime)
        : base(
            ProfileWorkspacePageIds.ModsAndMaps,
            "Mods & Maps",
            "Enabled mod IDs and map folders from the real Project Zomboid server config.",
            "Mods & Maps settings are in sync.",
            legacy,
            ["Browse Workshop items", "Keep draft rows in launcher state", "Save only active rows to the server", "Auto-order active mods from dependencies"])
    {
        _runtime = runtime;

        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
        ScanInstalledWorkshopAndUseItCommand = new AsyncRelayCommand(ScanInstalledWorkshopAndUseItAsync);
        SearchWorkshopCatalogCommand = new AsyncRelayCommand(SearchWorkshopCatalogAsync);
        PreviewWorkshopItemCommand = new AsyncRelayCommand<WorkshopCatalogItemViewModel>(PreviewWorkshopItemAsync);
        ApplyWorkshopPreviewCommand = new AsyncRelayCommand(ApplyWorkshopPreviewAsync, () => CanApplyWorkshopPreview);
        ApplyWorkshopPreviewWithDependenciesCommand = new AsyncRelayCommand(ApplyWorkshopPreviewWithDependenciesAsync, () => CanApplyWorkshopPreviewWithDependencies);
        CloseWorkshopPreviewCommand = new RelayCommand(CloseWorkshopPreviewModal);
        SaveSteamWebApiKeyCommand = new AsyncRelayCommand(SaveSteamWebApiKeyAsync);
        RemoveSteamWebApiKeyCommand = new AsyncRelayCommand(RemoveSteamWebApiKeyAsync);
        SetBrowseModeCommand = new RelayCommand(() => SetEditorMode(ModsMapsEditorMode.Browse));
        SetLiveEditorModeCommand = new RelayCommand(() => SetEditorMode(ModsMapsEditorMode.Live));
        SelectModEditorItemCommand = new RelayCommand<ModEditorItemViewModel>(SelectModEditorItem);
        ToggleModEditorItemCommand = new RelayCommand<ModEditorItemViewModel>(ToggleModEditorItem);
        RemoveModEditorItemCommand = new AsyncRelayCommand<ModEditorItemViewModel>(RemoveModEditorItemAsync);
        MoveModEditorItemUpCommand = new RelayCommand<ModEditorItemViewModel>(MoveModEditorItemUp);
        MoveModEditorItemDownCommand = new RelayCommand<ModEditorItemViewModel>(MoveModEditorItemDown);
        AutoOrderModEditorItemsCommand = new RelayCommand(AutoOrderModEditorItems, () => ActiveModEditorItems.Count > 1);
        ToggleMapEditorItemCommand = new RelayCommand<MapEditorItemViewModel>(ToggleMapEditorItem);
        RemoveMapEditorItemCommand = new AsyncRelayCommand<MapEditorItemViewModel>(RemoveMapEditorItemAsync);
        MoveMapEditorItemUpCommand = new RelayCommand<MapEditorItemViewModel>(MoveMapEditorItemUp);
        MoveMapEditorItemDownCommand = new RelayCommand<MapEditorItemViewModel>(MoveMapEditorItemDown);
        SaveNamedPresetCommand = new AsyncRelayCommand(SaveNamedPresetAsync, () => CanSaveNamedPreset);
        LoadNamedPresetCommand = new RelayCommand<SavedPresetViewModel>(LoadNamedPreset);
        DeleteNamedPresetCommand = new AsyncRelayCommand<SavedPresetViewModel>(DeleteNamedPresetAsync);
        ToggleWorkshopTagCommand = new RelayCommand<WorkshopTagChipViewModel>(ToggleWorkshopTag);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to manage Mods & Maps."
        : $"Manage draft and live Mods & Maps for {SelectedProfile.DisplayName}.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string WorkspaceSummary => SelectedProfile is null
        ? "Choose a profile to browse Workshop items and manage the live editor."
        : "Save Draft stores the editor rows in launcher state and keeps inactive entries visible. Save to Server writes only the active mod rows, active map rows, and resolved Workshop IDs into the real server config.";

    public string DraftBannerText => HasUnsavedChanges
        ? "The Mods & Maps editor has unsaved draft changes. Save Draft keeps them in launcher state first. Save to Server writes the active rows to the real server config."
        : "The editor matches the current live server config.";

    public string LoadoutSummary => $"{ActiveModEditorItems.Count} active mod(s) | {ActiveMapEditorItems.Count} active map(s) | {ResolveDraftWorkshopIds().Count} workshop item(s) tracked";

    public string BrowserSummary => _workshopSearchResult is null
        ? "Search the local Workshop cache or Steam, then add mods, maps, and collections into the editor."
        : $"{WorkshopSearchResults.Count} Workshop result(s) ready to inspect.";

    public string DraftStateSummary => HasStoredDraft
        ? "A launcher draft is saved for this profile."
        : "No saved launcher draft exists for this profile yet.";

    public string ScanSummary => _lastScanResult is null
        ? "No installed Workshop scan is loaded."
        : _lastScanResult.Diagnostics.Count == 0
            ? "Installed Workshop content normalized without diagnostics."
            : $"Installed Workshop content loaded with {_lastScanResult.Diagnostics.Count} diagnostic(s).";

    public string CatalogSummaryText => _catalog is null
        ? "No structured catalog loaded."
        : $"{_catalog.CatalogId} v{_catalog.CatalogVersion} | {_catalog.Branch}";

    public ObservableCollection<ModEditorItemViewModel> ModEditorItems { get; } = [];

    public ObservableCollection<MapEditorItemViewModel> MapEditorItems { get; } = [];

    public ObservableCollection<SavedPresetViewModel> SavedPresets { get; } = [];

    public ObservableCollection<WorkshopCatalogItemViewModel> WorkshopSearchResults { get; } = [];

    public ObservableCollection<string> WorkshopSearchDiagnostics { get; } = [];

    public ObservableCollection<WorkshopTagChipViewModel> AvailableWorkshopTags { get; } = [];

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
        WorkshopCatalogSearchFilter.Maps,
        WorkshopCatalogSearchFilter.Collections,
    ];

    public IReadOnlyList<ModEditorItemViewModel> ActiveModEditorItems =>
        ModEditorItems.Where(item => item.IsActive).OrderBy(item => item.SortOrder).ToArray();

    public IReadOnlyList<MapEditorItemViewModel> ActiveMapEditorItems =>
        MapEditorItems.Where(item => item.IsActive).OrderBy(item => item.SortOrder).ToArray();

    public bool HasStoredDraft
    {
        get => _hasStoredDraft;
        private set
        {
            if (_hasStoredDraft == value)
            {
                return;
            }

            _hasStoredDraft = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DraftStateSummary));
        }
    }

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
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedWorkshopPreview));
            OnPropertyChanged(nameof(HasNoSelectedWorkshopPreview));
            OnPropertyChanged(nameof(CanApplyWorkshopPreview));
            OnPropertyChanged(nameof(CanApplyWorkshopPreviewWithDependencies));
            OnPropertyChanged(nameof(SelectedWorkshopApplyLabel));
            OnPropertyChanged(nameof(SelectedWorkshopApplyWithDependenciesLabel));
            ApplyWorkshopPreviewCommand.NotifyCanExecuteChanged();
            ApplyWorkshopPreviewWithDependenciesCommand.NotifyCanExecuteChanged();
        }
    }

    public WorkshopPreviewViewModel? SelectedEditorPreview
    {
        get => _selectedEditorPreview;
        private set
        {
            if (ReferenceEquals(_selectedEditorPreview, value))
            {
                return;
            }

            _selectedEditorPreview?.Dispose();
            _selectedEditorPreview = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedEditorPreview));
            OnPropertyChanged(nameof(HasNoSelectedEditorPreview));
        }
    }

    public bool HasSelectedWorkshopPreview => SelectedWorkshopPreview is not null;

    public bool HasNoSelectedWorkshopPreview => SelectedWorkshopPreview is null;

    public bool HasSelectedEditorPreview => SelectedEditorPreview is not null;

    public bool HasNoSelectedEditorPreview => SelectedEditorPreview is null;

    public bool HasWorkshopSearchResults => WorkshopSearchResults.Count > 0;

    public bool HasNoWorkshopSearchResults => WorkshopSearchResults.Count == 0;

    public bool HasWorkshopSearchDiagnostics => WorkshopSearchDiagnostics.Count > 0;

    public bool HasNoWorkshopSearchDiagnostics => WorkshopSearchDiagnostics.Count == 0;

    public bool HasWorkshopTags => AvailableWorkshopTags.Count > 0;

    public bool HasSavedPresets => SavedPresets.Count > 0;

    public bool HasNoSavedPresets => SavedPresets.Count == 0;

    public bool HasModEditorItems => ModEditorItems.Count > 0;

    public bool HasNoModEditorItems => ModEditorItems.Count == 0;

    public bool HasMapEditorItems => MapEditorItems.Count > 0;

    public bool HasNoMapEditorItems => MapEditorItems.Count == 0;

    public bool HasSteamWebApiKeyConfigured => _workshopBrowserSettings.HasSteamWebApiKeyConfigured;

    public bool ShowSteamApiKeyEditor => !HasSteamWebApiKeyConfigured;

    public bool ShowSteamApiKeyRemoveOnly => HasSteamWebApiKeyConfigured;

    public bool ShowSteamApiKeyHelper => !HasSteamWebApiKeyConfigured && SearchMode is WorkshopCatalogSearchMode.Steam or WorkshopCatalogSearchMode.Both;

    public bool CanApplyWorkshopPreview => SelectedWorkshopPreview is not null && SelectedWorkshopPreview.HasChanges && !_isApplyingWorkshopPreview;

    public bool CanApplyWorkshopPreviewWithDependencies => SelectedWorkshopPreview is not null && SelectedWorkshopPreview.HasDependencyChanges && !_isApplyingWorkshopPreview;

    public bool CanSaveNamedPreset => SelectedProfile is not null && !string.IsNullOrWhiteSpace(NewPresetName);

    public bool IsBrowseMode => EditorMode == ModsMapsEditorMode.Browse;

    public bool IsLiveEditorMode => EditorMode == ModsMapsEditorMode.Live;

    public string SelectedWorkshopApplyLabel => SelectedWorkshopPreview?.ApplyLabel ?? "Add To Editor";

    public string SelectedWorkshopApplyWithDependenciesLabel => "Add With Dependencies";

    public string SelectedModDetailTitle => SelectedModEditorItem?.DisplayTitle ?? "Select a mod row";

    public string SelectedModDetailModId => SelectedModEditorItem?.ModId ?? "None";

    public string SelectedModDetailWorkshopId => string.IsNullOrWhiteSpace(SelectedModEditorItem?.WorkshopId)
        ? "Not resolved"
        : SelectedModEditorItem!.WorkshopId;

    public string SelectedModDetailState => SelectedModEditorItem is null
        ? "No row selected"
        : SelectedModEditorItem.IsActive
            ? "Active and written to Mods="
            : "Inactive and kept only in the editor";

    public string SelectedModDetailInstallState => SelectedModEditorItem is null
        ? "Unknown"
        : SelectedModEditorItem.IsInstalled
            ? "Installed locally"
            : "Not detected in the local Workshop cache";

    public string SelectedModDetailMapFolders => SelectedModEditorItem is null || SelectedModEditorItem.MapFolders.Count == 0
        ? "None"
        : string.Join(", ", SelectedModEditorItem.MapFolders);

    public string SelectedModDetailDependencyIds => SelectedModEditorItem is null || SelectedModEditorItem.DependencyModIds.Count == 0
        ? "None"
        : string.Join(", ", SelectedModEditorItem.DependencyModIds);

    public string SelectedModDetailDescription => SelectedEditorPreview?.Description ?? "Select a mod row to load its Workshop preview details when a Workshop ID is available.";

    public string WorkshopTagSummary => string.IsNullOrWhiteSpace(WorkshopTagInput)
        ? "No tag filters selected."
        : $"Tag filters: {WorkshopTagInput}";

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    public IAsyncRelayCommand ScanInstalledWorkshopAndUseItCommand { get; }

    public IAsyncRelayCommand SearchWorkshopCatalogCommand { get; }

    public IAsyncRelayCommand<WorkshopCatalogItemViewModel> PreviewWorkshopItemCommand { get; }

    public IAsyncRelayCommand ApplyWorkshopPreviewCommand { get; }

    public IAsyncRelayCommand ApplyWorkshopPreviewWithDependenciesCommand { get; }

    public IRelayCommand CloseWorkshopPreviewCommand { get; }

    public IAsyncRelayCommand SaveSteamWebApiKeyCommand { get; }

    public IAsyncRelayCommand RemoveSteamWebApiKeyCommand { get; }

    public IRelayCommand SetBrowseModeCommand { get; }

    public IRelayCommand SetLiveEditorModeCommand { get; }

    public IRelayCommand<ModEditorItemViewModel> SelectModEditorItemCommand { get; }

    public IRelayCommand<ModEditorItemViewModel> ToggleModEditorItemCommand { get; }

    public IAsyncRelayCommand<ModEditorItemViewModel> RemoveModEditorItemCommand { get; }

    public IRelayCommand<ModEditorItemViewModel> MoveModEditorItemUpCommand { get; }

    public IRelayCommand<ModEditorItemViewModel> MoveModEditorItemDownCommand { get; }

    public IRelayCommand AutoOrderModEditorItemsCommand { get; }

    public IRelayCommand<MapEditorItemViewModel> ToggleMapEditorItemCommand { get; }

    public IAsyncRelayCommand<MapEditorItemViewModel> RemoveMapEditorItemCommand { get; }

    public IRelayCommand<MapEditorItemViewModel> MoveMapEditorItemUpCommand { get; }

    public IRelayCommand<MapEditorItemViewModel> MoveMapEditorItemDownCommand { get; }

    public IAsyncRelayCommand SaveNamedPresetCommand { get; }

    public IRelayCommand<SavedPresetViewModel> LoadNamedPresetCommand { get; }

    public IAsyncRelayCommand<SavedPresetViewModel> DeleteNamedPresetCommand { get; }

    public IRelayCommand<WorkshopTagChipViewModel> ToggleWorkshopTagCommand { get; }

    [ObservableProperty]
    private string loadStatus = "Select a profile to load Mods & Maps.";

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private WorkshopCatalogSearchMode searchMode = WorkshopCatalogSearchMode.Both;

    [ObservableProperty]
    private WorkshopCatalogSearchFilter searchFilter = WorkshopCatalogSearchFilter.All;

    [ObservableProperty]
    private string steamWebApiKey = string.Empty;

    [ObservableProperty]
    private string workshopTagInput = string.Empty;

    [ObservableProperty]
    private bool isWorkshopPreviewModalOpen;

    [ObservableProperty]
    private string newPresetName = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private ModsMapsEditorMode editorMode = ModsMapsEditorMode.Browse;

    [ObservableProperty]
    private ModEditorItemViewModel? selectedModEditorItem;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        _ = LoadAsync(profile);
    }

    public override Task RefreshPageAsync() => LoadAsync(SelectedProfile);

    public override async Task SaveDraftAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var saved = await PersistDraftAsync(
            "Saved the Mods & Maps editor draft to launcher state. Save to Server when you are ready to write the active rows to the server config.",
            updateStatus: true);
        if (saved)
        {
            RefreshDirtyState();
        }
    }

    public override async Task DiscardDraftAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            await _runtime.DeleteModsMapsDraftAsync(SelectedProfile.ProfileId);
        }
        catch
        {
        }

        HasStoredDraft = false;
        await LoadAsync(SelectedProfile);
    }

    private async Task SaveSettingsAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var updatedPreset = await _runtime.UpdateWorkshopPresetAsync(SelectedProfile.ProfileId, BuildPresetForSave());
            if (updatedPreset is null)
            {
                LoadStatus = "Mods & Maps settings could not be saved.";
                return;
            }

            try
            {
                await _runtime.DeleteModsMapsDraftAsync(SelectedProfile.ProfileId);
            }
            catch
            {
            }

            HasStoredDraft = false;
            _livePresetHash = BuildPresetHash(updatedPreset);
            await LoadMetadataAsync(SelectedProfile.ProfileId, updatedPreset);
            ApplyPresetToEditor(updatedPreset);
            MarkClean("Mods & Maps settings are in sync.");
            LoadStatus = $"Saved Mods & Maps to the server config for {SelectedProfile.DisplayName}.";
            await Legacy.RefreshCommand.ExecuteAsync(null);
            NotifyComputedState();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
    }

    private async Task ReloadAsync()
    {
        await LoadAsync(SelectedProfile);
    }

    private async Task ScanInstalledWorkshopAndUseItAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var result = await _runtime.ScanWorkshopAsync(SelectedProfile.ProfileId);
            if (result is null)
            {
                LoadStatus = "Installed Workshop scan did not return a result.";
                return;
            }

            _lastScanResult = result;
            await LoadMetadataAsync(SelectedProfile.ProfileId, result.Preset);
            ApplyPresetToEditor(result.Preset);
            HasStoredDraft = false;
            RefreshDirtyState();
            LoadStatus = result.Diagnostics.Count == 0
                ? "Loaded installed Workshop content into the editor. Save Draft to keep it or Save to Server to apply it."
                : $"Loaded installed Workshop content into the editor with {result.Diagnostics.Count} diagnostic(s). Save Draft to keep it or Save to Server to apply it.";
            NotifyComputedState();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
    }

    private async Task LoadAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            Reset();
            return;
        }

        IsLoading = true;
        LoadStatus = $"Loading Mods & Maps for {profile.DisplayName}...";

        try
        {
            _catalog = await _runtime.GetSettingsCatalogAsync(profile.ProfileId);
            _workshopBrowserSettings = await _runtime.GetWorkshopBrowserSettingsAsync() ?? new SteamWorkshopBrowserSettingsDto(false);
            var livePreset = await _runtime.GetWorkshopPresetAsync(profile.ProfileId) ?? WorkshopPreset.Empty;
            var draft = await _runtime.GetModsMapsDraftAsync(profile.ProfileId);
            var presets = await _runtime.GetNamedWorkshopPresetsAsync(profile.ProfileId) ?? [];

            _livePresetHash = BuildPresetHash(livePreset);
            await LoadMetadataAsync(profile.ProfileId, draft is null ? livePreset : BuildPresetFromDraft(draft));
            ReplaceSavedPresets(presets);
            ResetWorkshopBrowserState();
            SelectedWorkshopPreview = null;
            SelectedEditorPreview = null;
            SelectedModEditorItem = null;
            SteamWebApiKey = string.Empty;
            _lastScanResult = null;

            if (draft is not null)
            {
                ApplyDraftToEditor(draft);
                HasStoredDraft = true;
                LoadStatus = $"Loaded a saved Mods & Maps draft from {draft.UpdatedAtUtc.ToLocalTime():g}.";
            }
            else
            {
                ApplyPresetToEditor(livePreset);
                HasStoredDraft = false;
                LoadStatus = "Loaded Mods & Maps from the local host.";
            }

            RefreshDirtyState();
            NotifyComputedState();
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

    private async Task LoadMetadataAsync(string profileId, WorkshopPreset currentPreset)
    {
        _metadataByModId.Clear();
        _metadataByMapId.Clear();

        var search = await _runtime.SearchWorkshopCatalogAsync(
            profileId,
            new WorkshopCatalogSearchRequestDto(string.Empty, WorkshopCatalogSearchMode.Local, 500, currentPreset, WorkshopCatalogSearchFilter.All),
            CancellationToken.None);

        foreach (var item in search?.Results ?? [])
        {
            if (item.Kind is WorkshopCatalogItemKind.Collection)
            {
                continue;
            }

            var dependencyModIds = item.DependencyModIds ?? [];
            foreach (var modId in item.ModIds)
            {
                _metadataByModId[modId] = new WorkshopMetadata(
                    item.Title,
                    item.WorkshopId,
                    item.IsInstalledLocally,
                    dependencyModIds,
                    item.MapFolders);
            }

            foreach (var mapFolder in item.MapFolders)
            {
                _metadataByMapId[mapFolder] = new WorkshopMetadata(
                    mapFolder,
                    item.WorkshopId,
                    item.IsInstalledLocally,
                    dependencyModIds,
                    [mapFolder]);
            }
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
                new WorkshopCatalogSearchRequestDto(SearchQuery, SearchMode, 24, BuildPresetForWorkshopOperations(), SearchFilter, ParseSelectedWorkshopTags()),
                CancellationToken.None);

            _workshopSearchResult = result;
            ReplaceWorkshopSearchResults(result?.Results ?? []);
            ReplaceWorkshopTags(result?.AvailableTags ?? [], result?.SelectedTags ?? ParseSelectedWorkshopTags());
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

        await ApplyWorkshopPreviewCoreAsync(SelectedWorkshopPreview, includeDependencies: false);
    }

    private async Task ApplyWorkshopPreviewWithDependenciesAsync()
    {
        if (SelectedWorkshopPreview is null)
        {
            return;
        }

        await ApplyWorkshopPreviewCoreAsync(SelectedWorkshopPreview, includeDependencies: true);
    }

    private async Task ApplyWorkshopPreviewCoreAsync(WorkshopPreviewViewModel preview, bool includeDependencies)
    {
        _isApplyingWorkshopPreview = true;
        ApplyWorkshopPreviewCommand.NotifyCanExecuteChanged();
        ApplyWorkshopPreviewWithDependenciesCommand.NotifyCanExecuteChanged();

        try
        {
            ApplyPreviewRows(preview, includeDependencies);
            RefreshEditorCollections();
            RefreshDirtyState();

            var saved = await PersistDraftAsync(
                includeDependencies
                    ? $"Added {preview.Title} with dependencies to the editor. Open Live Editor to refresh the list."
                    : "Added to editor. Open Live Editor to refresh the list.",
                updateStatus: true);

            if (!saved)
            {
                LoadStatus = "The editor changed locally, but the draft could not be saved yet.";
            }

            CloseWorkshopPreviewModal();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
        finally
        {
            _isApplyingWorkshopPreview = false;
            ApplyWorkshopPreviewCommand.NotifyCanExecuteChanged();
            ApplyWorkshopPreviewWithDependenciesCommand.NotifyCanExecuteChanged();
            NotifyComputedState();
        }
    }

    private void ApplyPreviewRows(WorkshopPreviewViewModel preview, bool includeDependencies)
    {
        if (preview.Item.IsCollection && preview.CollectionChildren.Count > 0)
        {
            foreach (var child in preview.CollectionChildren)
            {
                ApplyChildToEditor(child);
            }
        }
        else
        {
            ApplyItemToEditor(
                preview.Item.WorkshopId,
                preview.Item.Title,
                preview.Item.ModIds,
                preview.Item.MapFolders,
                preview.Item.DependencyModIds,
                preview.ModNamesById);
        }

        if (includeDependencies)
        {
            foreach (var child in preview.DependencyChildren)
            {
                ApplyChildToEditor(child);
            }
        }
    }

    private void ApplyChildToEditor(WorkshopCatalogPreviewChildDto child)
    {
        ApplyItemToEditor(
            child.WorkshopId,
            child.Title,
            child.ModIds ?? [],
            child.MapFolders ?? [],
            child.DependencyModIds ?? [],
            null);
    }

    private void ApplyItemToEditor(
        string workshopId,
        string fallbackTitle,
        IReadOnlyList<string> modIds,
        IReadOnlyList<string> mapFolders,
        IReadOnlyList<string> dependencyModIds,
        IReadOnlyDictionary<string, string>? modNamesById)
    {
        EnsureWorkshopId(workshopId);

        foreach (var modId in modIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var displayName = modNamesById is not null && modNamesById.TryGetValue(modId, out var named)
                ? named
                : fallbackTitle;
            UpsertMetadata(modId, displayName, workshopId, dependencyModIds, mapFolders, isInstalled: true);
            EnsureModRow(modId, displayName, workshopId, dependencyModIds, mapFolders, activate: true);
        }

        foreach (var mapFolder in mapFolders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            UpsertMapMetadata(mapFolder, workshopId, isInstalled: true);
            EnsureMapRow(mapFolder, mapFolder, workshopId, activate: true);
        }
    }

    private async Task SaveSteamWebApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(SteamWebApiKey))
        {
            LoadStatus = "Paste a Steam Web API key, then save it.";
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

    private void SetEditorMode(ModsMapsEditorMode mode)
    {
        if (EditorMode == mode)
        {
            return;
        }

        EditorMode = mode;
    }

    private void SelectModEditorItem(ModEditorItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedModEditorItem = item;
    }

    private void ToggleModEditorItem(ModEditorItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsActive = !item.IsActive;
        RefreshEditorCollections();
        RefreshDirtyState();
        LoadStatus = item.IsActive
            ? $"Marked {item.DisplayTitle} active. Active rows are written to Mods= when you Save to Server."
            : $"Marked {item.DisplayTitle} inactive. The row stays saved in the editor but will not be written to Mods=.";
    }

    private async Task RemoveModEditorItemAsync(ModEditorItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        ModEditorItems.Remove(item);
        if (SelectedModEditorItem == item)
        {
            SelectedModEditorItem = null;
            SelectedEditorPreview = null;
        }

        await PruneWorkshopIdIfUnusedAsync(item.WorkshopId);
        RefreshEditorCollections();
        RefreshDirtyState();
        LoadStatus = $"Removed {item.DisplayTitle} from the editor.";
    }

    private void MoveModEditorItemUp(ModEditorItemViewModel? item)
    {
        if (item is null || !item.IsActive)
        {
            return;
        }

        var activeRows = ActiveModEditorItems.ToList();
        var index = activeRows.FindIndex(candidate => candidate.RowId == item.RowId);
        if (index <= 0)
        {
            return;
        }

        (activeRows[index - 1], activeRows[index]) = (activeRows[index], activeRows[index - 1]);
        ApplyActiveModOrder(activeRows);
        LoadStatus = $"Moved {item.DisplayTitle} up in the active load order.";
    }

    private void MoveModEditorItemDown(ModEditorItemViewModel? item)
    {
        if (item is null || !item.IsActive)
        {
            return;
        }

        var activeRows = ActiveModEditorItems.ToList();
        var index = activeRows.FindIndex(candidate => candidate.RowId == item.RowId);
        if (index < 0 || index >= activeRows.Count - 1)
        {
            return;
        }

        (activeRows[index + 1], activeRows[index]) = (activeRows[index], activeRows[index + 1]);
        ApplyActiveModOrder(activeRows);
        LoadStatus = $"Moved {item.DisplayTitle} down in the active load order.";
    }

    public void TryReorderActiveModRow(int draggedRowId, int targetRowId)
    {
        if (draggedRowId == targetRowId)
        {
            return;
        }

        var activeRows = ActiveModEditorItems.ToList();
        var draggedIndex = activeRows.FindIndex(candidate => candidate.RowId == draggedRowId);
        var targetIndex = activeRows.FindIndex(candidate => candidate.RowId == targetRowId);
        if (draggedIndex < 0 || targetIndex < 0)
        {
            return;
        }

        var draggedItem = activeRows[draggedIndex];
        activeRows.RemoveAt(draggedIndex);
        if (draggedIndex < targetIndex)
        {
            targetIndex -= 1;
        }

        activeRows.Insert(targetIndex, draggedItem);
        ApplyActiveModOrder(activeRows);
        LoadStatus = $"Moved {draggedItem.DisplayTitle} in the active load order.";
    }

    private void AutoOrderModEditorItems()
    {
        var activeRows = ActiveModEditorItems.ToList();
        if (activeRows.Count <= 1)
        {
            return;
        }

        var byModId = activeRows.ToDictionary(item => item.ModId, StringComparer.OrdinalIgnoreCase);
        var indegree = activeRows.ToDictionary(item => item.ModId, _ => 0, StringComparer.OrdinalIgnoreCase);
        var outgoing = activeRows.ToDictionary(item => item.ModId, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in activeRows)
        {
            foreach (var dependency in item.DependencyModIds)
            {
                if (!byModId.ContainsKey(dependency))
                {
                    continue;
                }

                outgoing[dependency].Add(item.ModId);
                indegree[item.ModId]++;
            }
        }

        var ordered = new List<ModEditorItemViewModel>(activeRows.Count);
        var queue = new Queue<ModEditorItemViewModel>(activeRows.Where(item => indegree[item.ModId] == 0).OrderBy(item => item.SortOrder));
        while (queue.Count > 0)
        {
            var next = queue.Dequeue();
            ordered.Add(next);

            foreach (var modId in outgoing[next.ModId].OrderBy(value => byModId[value].SortOrder))
            {
                indegree[modId]--;
                if (indegree[modId] == 0)
                {
                    queue.Enqueue(byModId[modId]);
                }
            }
        }

        foreach (var remaining in activeRows.Where(item => ordered.All(candidate => candidate.RowId != item.RowId)).OrderBy(item => item.SortOrder))
        {
            ordered.Add(remaining);
        }

        ApplyActiveModOrder(ordered);
        LoadStatus = "Auto-ordered active mods from the saved dependency rows.";
    }

    private void ApplyActiveModOrder(IReadOnlyList<ModEditorItemViewModel> orderedActiveRows)
    {
        var inactiveRows = ModEditorItems.Where(item => !item.IsActive).OrderBy(item => item.SortOrder).ToArray();
        ModEditorItems.Clear();
        foreach (var row in orderedActiveRows)
        {
            ModEditorItems.Add(row);
        }

        foreach (var row in inactiveRows)
        {
            ModEditorItems.Add(row);
        }

        RefreshEditorCollections();
        RefreshDirtyState();
    }

    private void ToggleMapEditorItem(MapEditorItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsActive = !item.IsActive;
        RefreshEditorCollections();
        RefreshDirtyState();
        LoadStatus = item.IsActive
            ? $"Marked {item.Title} active. Active map rows are written to Map= when you Save to Server."
            : $"Marked {item.Title} inactive. The row stays saved in the editor but will not be written to Map=.";
    }

    private async Task RemoveMapEditorItemAsync(MapEditorItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        MapEditorItems.Remove(item);
        await PruneWorkshopIdIfUnusedAsync(item.WorkshopId);
        RefreshEditorCollections();
        RefreshDirtyState();
        LoadStatus = $"Removed map row {item.Title} from the editor.";
    }

    private void MoveMapEditorItemUp(MapEditorItemViewModel? item)
    {
        if (item is null || !item.IsActive)
        {
            return;
        }

        MoveMapRow(item, -1);
    }

    private void MoveMapEditorItemDown(MapEditorItemViewModel? item)
    {
        if (item is null || !item.IsActive)
        {
            return;
        }

        MoveMapRow(item, 1);
    }

    private void MoveMapRow(MapEditorItemViewModel item, int delta)
    {
        var activeRows = ActiveMapEditorItems.ToList();
        var index = activeRows.FindIndex(candidate => candidate.RowId == item.RowId);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= activeRows.Count)
        {
            return;
        }

        (activeRows[target], activeRows[index]) = (activeRows[index], activeRows[target]);
        var inactiveRows = MapEditorItems.Where(candidate => !candidate.IsActive).OrderBy(candidate => candidate.SortOrder).ToArray();
        MapEditorItems.Clear();
        foreach (var row in activeRows)
        {
            MapEditorItems.Add(row);
        }

        foreach (var row in inactiveRows)
        {
            MapEditorItems.Add(row);
        }

        RefreshEditorCollections();
        RefreshDirtyState();
        LoadStatus = delta < 0
            ? $"Moved {item.Title} up in the active map order."
            : $"Moved {item.Title} down in the active map order.";
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

        ApplyPresetToEditor(preset.Preset);
        HasStoredDraft = false;
        RefreshDirtyState();
        LoadStatus = $"Loaded named preset '{preset.Name}' into the editor. Save Draft to keep it or Save to Server to push it live.";
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

    private void ApplyPresetToEditor(WorkshopPreset preset)
    {
        _isApplyingState = true;
        try
        {
            _draftWorkshopItemIds = DistinctNonEmpty(preset.WorkshopItemIds).ToList();
            ReplaceModRows(BuildRowsFromPreset(preset));
            ReplaceMapRows(BuildMapRowsFromPreset(preset));
            AppendDiscoveredInactiveRows();
            SelectedModEditorItem = ModEditorItems.FirstOrDefault(item => item.IsActive) ?? ModEditorItems.FirstOrDefault();
        }
        finally
        {
            _isApplyingState = false;
        }

        RefreshEditorCollections();
    }

    private void ApplyDraftToEditor(ModsMapsDraftDto draft)
    {
        _isApplyingState = true;
        try
        {
            _draftWorkshopItemIds = DistinctNonEmpty(draft.WorkshopItemIds).ToList();
            EditorMode = draft.EditorMode;
            ReplaceModRows(BuildRowsFromDraft(draft));
            ReplaceMapRows(BuildMapRowsFromDraft(draft));
            AppendDiscoveredInactiveRows();
            SelectedModEditorItem = ModEditorItems.FirstOrDefault(item => item.IsActive) ?? ModEditorItems.FirstOrDefault();
        }
        finally
        {
            _isApplyingState = false;
        }

        RefreshEditorCollections();
    }

    private IReadOnlyList<ModEditorItemViewModel> BuildRowsFromPreset(WorkshopPreset preset)
    {
        var rows = new List<ModEditorItemViewModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var modId in DistinctNonEmpty(preset.EnabledModIds))
        {
            seen.Add(modId);
            rows.Add(CreateModRow(
                AllocateModRowId(),
                ResolveModName(modId),
                modId,
                ResolveWorkshopIdForMod(modId, preset.WorkshopItemIds),
                isActive: true,
                _metadataByModId.TryGetValue(modId, out var metadata) ? metadata.DependencyModIds : [],
                _metadataByModId.TryGetValue(modId, out metadata) ? metadata.MapFolders : [],
                _metadataByModId.TryGetValue(modId, out metadata) && metadata.IsInstalled));
        }

        return rows;
    }

    private IReadOnlyList<MapEditorItemViewModel> BuildMapRowsFromPreset(WorkshopPreset preset)
    {
        var rows = new List<MapEditorItemViewModel>();
        foreach (var mapFolder in DistinctNonEmpty(preset.MapFolders))
        {
            rows.Add(CreateMapRow(
                AllocateMapRowId(),
                ResolveMapTitle(mapFolder),
                mapFolder,
                ResolveWorkshopIdForMap(mapFolder, preset.WorkshopItemIds),
                isActive: true,
                _metadataByMapId.TryGetValue(mapFolder, out var metadata) && metadata.IsInstalled));
        }

        return rows;
    }

    private IReadOnlyList<ModEditorItemViewModel> BuildRowsFromDraft(ModsMapsDraftDto draft)
    {
        var rows = new List<ModEditorItemViewModel>();
        foreach (var row in draft.ModRows.OrderBy(item => item.SortOrder).ThenBy(item => item.RowId))
        {
            _nextModRowId = Math.Max(_nextModRowId, row.RowId + 1);
            var metadata = _metadataByModId.GetValueOrDefault(row.ModId);
            rows.Add(CreateModRow(
                row.RowId,
                ChooseBetterName(row.ModName, metadata?.DisplayName, row.ModId),
                row.ModId,
                ChooseBetterWorkshopId(row.WorkshopId, metadata?.WorkshopId),
                row.IsActive,
                row.DependencyModIds.Count > 0 ? DistinctNonEmpty(row.DependencyModIds) : metadata?.DependencyModIds ?? [],
                row.MapFolders.Count > 0 ? DistinctNonEmpty(row.MapFolders) : metadata?.MapFolders ?? [],
                metadata?.IsInstalled ?? false));
        }

        return rows;
    }

    private IReadOnlyList<MapEditorItemViewModel> BuildMapRowsFromDraft(ModsMapsDraftDto draft)
    {
        var rows = new List<MapEditorItemViewModel>();
        foreach (var row in draft.MapRows.OrderBy(item => item.SortOrder).ThenBy(item => item.RowId))
        {
            _nextMapRowId = Math.Max(_nextMapRowId, row.RowId + 1);
            var metadata = _metadataByMapId.GetValueOrDefault(row.MapFolder);
            rows.Add(CreateMapRow(
                row.RowId,
                ChooseBetterName(row.Title, metadata?.DisplayName, row.MapFolder),
                row.MapFolder,
                ChooseBetterWorkshopId(row.WorkshopId, metadata?.WorkshopId),
                row.IsActive,
                metadata?.IsInstalled ?? false));
        }

        return rows;
    }

    private void AppendDiscoveredInactiveRows()
    {
        var existingModIds = ModEditorItems.Select(item => item.ModId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _metadataByModId.OrderBy(entry => entry.Value.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            if (existingModIds.Contains(entry.Key))
            {
                continue;
            }

            ModEditorItems.Add(CreateModRow(
                AllocateModRowId(),
                entry.Value.DisplayName,
                entry.Key,
                entry.Value.WorkshopId ?? string.Empty,
                isActive: false,
                entry.Value.DependencyModIds,
                entry.Value.MapFolders,
                entry.Value.IsInstalled));
        }

        var existingMapFolders = MapEditorItems.Select(item => item.MapFolder).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _metadataByMapId.OrderBy(entry => entry.Value.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            if (existingMapFolders.Contains(entry.Key))
            {
                continue;
            }

            MapEditorItems.Add(CreateMapRow(
                AllocateMapRowId(),
                entry.Value.DisplayName,
                entry.Key,
                entry.Value.WorkshopId ?? string.Empty,
                isActive: false,
                entry.Value.IsInstalled));
        }
    }

    private void ReplaceModRows(IEnumerable<ModEditorItemViewModel> rows)
    {
        ModEditorItems.Clear();
        foreach (var row in rows)
        {
            ModEditorItems.Add(row);
        }
    }

    private void ReplaceMapRows(IEnumerable<MapEditorItemViewModel> rows)
    {
        MapEditorItems.Clear();
        foreach (var row in rows)
        {
            MapEditorItems.Add(row);
        }
    }

    private ModEditorItemViewModel CreateModRow(
        int rowId,
        string modName,
        string modId,
        string workshopId,
        bool isActive,
        IReadOnlyList<string> dependencyModIds,
        IReadOnlyList<string> mapFolders,
        bool isInstalled)
    {
        return new ModEditorItemViewModel(
            rowId,
            modName,
            modId,
            workshopId,
            isActive,
            0,
            DistinctNonEmpty(dependencyModIds),
            DistinctNonEmpty(mapFolders),
            isInstalled);
    }

    private MapEditorItemViewModel CreateMapRow(
        int rowId,
        string title,
        string mapFolder,
        string workshopId,
        bool isActive,
        bool isInstalled)
    {
        return new MapEditorItemViewModel(rowId, title, mapFolder, workshopId, isActive, 0, isInstalled);
    }

    private async Task<bool> PersistDraftAsync(string? successMessage, bool updateStatus)
    {
        if (SelectedProfile is null)
        {
            return false;
        }

        try
        {
            var saved = await _runtime.SaveModsMapsDraftAsync(SelectedProfile.ProfileId, BuildDraftModel());
            if (saved is null)
            {
                return false;
            }

            HasStoredDraft = true;
            _draftWorkshopItemIds = DistinctNonEmpty(saved.WorkshopItemIds).ToList();
            if (updateStatus && !string.IsNullOrWhiteSpace(successMessage))
            {
                LoadStatus = successMessage;
            }

            return true;
        }
        catch (Exception ex)
        {
            if (updateStatus)
            {
                LoadStatus = ex.Message;
            }

            return false;
        }
    }

    private ModsMapsDraftDto BuildDraftModel() =>
        new(
            SelectedProfile?.ProfileId ?? string.Empty,
            ProjectZomboidBranch.Unstable42,
            ResolveDraftWorkshopIds(),
            ModEditorItems
                .OrderBy(item => item.SortOrder)
                .Select(item => new ModsMapsModRowDto(
                    item.RowId,
                    item.ModName,
                    item.ModId,
                    item.WorkshopId,
                    item.IsActive,
                    item.SortOrder,
                    item.DependencyModIds,
                    item.MapFolders))
                .ToArray(),
            MapEditorItems
                .OrderBy(item => item.SortOrder)
                .Select(item => new ModsMapsMapRowDto(
                    item.RowId,
                    item.Title,
                    item.MapFolder,
                    item.WorkshopId,
                    item.IsActive,
                    item.SortOrder))
                .ToArray(),
            EditorMode,
            true,
            DateTimeOffset.UtcNow);

    private WorkshopPreset BuildPresetFromDraft(ModsMapsDraftDto draft) =>
        new()
        {
            WorkshopItemIds = DistinctNonEmpty(draft.WorkshopItemIds),
            EnabledModIds = draft.ModRows.Where(item => item.IsActive).OrderBy(item => item.SortOrder).Select(item => item.ModId).ToArray(),
            MapFolders = draft.MapRows.Where(item => item.IsActive).OrderBy(item => item.SortOrder).Select(item => item.MapFolder).ToArray(),
        };

    private WorkshopPreset BuildPresetForSave() =>
        new()
        {
            WorkshopItemIds = ResolveDraftWorkshopIds(),
            EnabledModIds = ActiveModEditorItems.Select(item => item.ModId).ToArray(),
            MapFolders = ActiveMapEditorItems.Select(item => item.MapFolder).ToArray(),
        };

    private WorkshopPreset BuildPresetForNamedPreset() =>
        new()
        {
            WorkshopItemIds = [],
            EnabledModIds = ActiveModEditorItems.Select(item => item.ModId).ToArray(),
            MapFolders = ActiveMapEditorItems.Select(item => item.MapFolder).ToArray(),
        };

    private WorkshopPreset BuildPresetForWorkshopOperations() => BuildPresetForSave();

    private void RefreshEditorCollections()
    {
        if (_isApplyingState)
        {
            return;
        }

        var activeMods = ModEditorItems.Where(item => item.IsActive).OrderBy(item => item.SortOrder).ToArray();
        var inactiveMods = ModEditorItems.Where(item => !item.IsActive).OrderBy(item => item.SortOrder).ToArray();
        ModEditorItems.Clear();
        foreach (var row in activeMods.Concat(inactiveMods))
        {
            ModEditorItems.Add(row);
        }

        for (var index = 0; index < ModEditorItems.Count; index++)
        {
            var item = ModEditorItems[index];
            item.SortOrder = index;
            item.ActiveOrder = item.IsActive ? ActiveModEditorItems.Count(candidate => candidate.SortOrder <= item.SortOrder) : null;
        }

        var activeMaps = MapEditorItems.Where(item => item.IsActive).OrderBy(item => item.SortOrder).ToArray();
        var inactiveMaps = MapEditorItems.Where(item => !item.IsActive).OrderBy(item => item.SortOrder).ToArray();
        MapEditorItems.Clear();
        foreach (var row in activeMaps.Concat(inactiveMaps))
        {
            MapEditorItems.Add(row);
        }

        for (var index = 0; index < MapEditorItems.Count; index++)
        {
            var item = MapEditorItems[index];
            item.SortOrder = index;
            item.ActiveOrder = item.IsActive ? ActiveMapEditorItems.Count(candidate => candidate.SortOrder <= item.SortOrder) : null;
        }

        SynchronizeTransportState();
        AutoOrderModEditorItemsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasModEditorItems));
        OnPropertyChanged(nameof(HasNoModEditorItems));
        OnPropertyChanged(nameof(HasMapEditorItems));
        OnPropertyChanged(nameof(HasNoMapEditorItems));
        OnPropertyChanged(nameof(ActiveModEditorItems));
        OnPropertyChanged(nameof(ActiveMapEditorItems));
        OnPropertyChanged(nameof(LoadoutSummary));
        OnPropertyChanged(nameof(SelectedModDetailTitle));
        OnPropertyChanged(nameof(SelectedModDetailModId));
        OnPropertyChanged(nameof(SelectedModDetailWorkshopId));
        OnPropertyChanged(nameof(SelectedModDetailState));
        OnPropertyChanged(nameof(SelectedModDetailInstallState));
        OnPropertyChanged(nameof(SelectedModDetailMapFolders));
        OnPropertyChanged(nameof(SelectedModDetailDependencyIds));
        OnPropertyChanged(nameof(SelectedModDetailDescription));
    }

    private void SynchronizeTransportState()
    {
        _draftWorkshopItemIds = ResolveDraftWorkshopIds().ToList();
    }

    private void RefreshDirtyState()
    {
        var livePresetChanged = !string.Equals(_livePresetHash, BuildPresetHash(BuildPresetForSave()), StringComparison.Ordinal);
        if (HasStoredDraft || livePresetChanged)
        {
            MarkDirty("The Mods & Maps editor has unsaved draft changes.");
        }
        else
        {
            MarkClean("Mods & Maps settings are in sync.");
        }

        OnPropertyChanged(nameof(DraftBannerText));
        OnPropertyChanged(nameof(DraftStateSummary));
    }

    private async Task LoadSelectedModPreviewAsync(ModEditorItemViewModel? item)
    {
        if (SelectedProfile is null || item is null || string.IsNullOrWhiteSpace(item.WorkshopId))
        {
            SelectedEditorPreview = null;
            OnPropertyChanged(nameof(SelectedModDetailDescription));
            return;
        }

        try
        {
            var preview = await _runtime.GetWorkshopCatalogPreviewAsync(
                SelectedProfile.ProfileId,
                item.WorkshopId,
                new WorkshopCatalogPreviewRequestDto(WorkshopCatalogSearchMode.Both, BuildPresetForWorkshopOperations()),
                CancellationToken.None);

            SelectedEditorPreview = preview is null
                ? null
                : await WorkshopPreviewViewModel.CreateAsync(preview, _runtime, CancellationToken.None);
            OnPropertyChanged(nameof(SelectedModDetailDescription));
        }
        catch
        {
            SelectedEditorPreview = null;
            OnPropertyChanged(nameof(SelectedModDetailDescription));
        }
    }

    private void EnsureWorkshopId(string workshopId)
    {
        if (string.IsNullOrWhiteSpace(workshopId))
        {
            return;
        }

        if (_draftWorkshopItemIds.All(candidate => !string.Equals(candidate, workshopId, StringComparison.OrdinalIgnoreCase)))
        {
            _draftWorkshopItemIds.Add(workshopId);
        }
    }

    private async Task PruneWorkshopIdIfUnusedAsync(string workshopId)
    {
        if (string.IsNullOrWhiteSpace(workshopId))
        {
            return;
        }

        var stillUsedByMod = ModEditorItems.Any(item =>
            !string.IsNullOrWhiteSpace(item.WorkshopId) &&
            string.Equals(item.WorkshopId, workshopId, StringComparison.OrdinalIgnoreCase));
        var stillUsedByActiveMap = MapEditorItems.Any(item =>
            item.IsActive &&
            !string.IsNullOrWhiteSpace(item.WorkshopId) &&
            string.Equals(item.WorkshopId, workshopId, StringComparison.OrdinalIgnoreCase));

        if (!stillUsedByMod && !stillUsedByActiveMap)
        {
            _draftWorkshopItemIds = _draftWorkshopItemIds
                .Where(candidate => !string.Equals(candidate, workshopId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        await Task.CompletedTask;
    }

    private void EnsureModRow(
        string modId,
        string modName,
        string workshopId,
        IReadOnlyList<string> dependencyModIds,
        IReadOnlyList<string> mapFolders,
        bool activate)
    {
        var existing = ModEditorItems.FirstOrDefault(item => string.Equals(item.ModId, modId, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            ModEditorItems.Add(CreateModRow(
                AllocateModRowId(),
                modName,
                modId,
                workshopId,
                activate,
                dependencyModIds,
                mapFolders,
                isInstalled: true));
            return;
        }

        existing.ModName = ChooseBetterName(existing.ModName, modName, modId);
        existing.WorkshopId = ChooseBetterWorkshopId(existing.WorkshopId, workshopId);
        existing.DependencyModIds = DistinctNonEmpty(existing.DependencyModIds.Concat(dependencyModIds)).ToArray();
        existing.MapFolders = DistinctNonEmpty(existing.MapFolders.Concat(mapFolders)).ToArray();
        existing.IsInstalled = true;
        if (activate)
        {
            existing.IsActive = true;
        }
    }

    private void EnsureMapRow(string mapFolder, string title, string workshopId, bool activate)
    {
        var existing = MapEditorItems.FirstOrDefault(item => string.Equals(item.MapFolder, mapFolder, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            MapEditorItems.Add(CreateMapRow(AllocateMapRowId(), title, mapFolder, workshopId, activate, isInstalled: true));
            return;
        }

        existing.Title = ChooseBetterName(existing.Title, title, mapFolder);
        existing.WorkshopId = ChooseBetterWorkshopId(existing.WorkshopId, workshopId);
        existing.IsInstalled = true;
        if (activate)
        {
            existing.IsActive = true;
        }
    }

    private void UpsertMetadata(
        string modId,
        string displayName,
        string workshopId,
        IReadOnlyList<string> dependencyModIds,
        IReadOnlyList<string> mapFolders,
        bool isInstalled)
    {
        _metadataByModId[modId] = new WorkshopMetadata(
            ChooseBetterName(_metadataByModId.GetValueOrDefault(modId)?.DisplayName, displayName, modId),
            ChooseBetterWorkshopId(_metadataByModId.GetValueOrDefault(modId)?.WorkshopId, workshopId),
            isInstalled || (_metadataByModId.GetValueOrDefault(modId)?.IsInstalled ?? false),
            DistinctNonEmpty((_metadataByModId.GetValueOrDefault(modId)?.DependencyModIds ?? []).Concat(dependencyModIds)),
            DistinctNonEmpty((_metadataByModId.GetValueOrDefault(modId)?.MapFolders ?? []).Concat(mapFolders)));
    }

    private void UpsertMapMetadata(string mapFolder, string workshopId, bool isInstalled)
    {
        _metadataByMapId[mapFolder] = new WorkshopMetadata(
            mapFolder,
            ChooseBetterWorkshopId(_metadataByMapId.GetValueOrDefault(mapFolder)?.WorkshopId, workshopId),
            isInstalled || (_metadataByMapId.GetValueOrDefault(mapFolder)?.IsInstalled ?? false),
            _metadataByMapId.GetValueOrDefault(mapFolder)?.DependencyModIds ?? [],
            [mapFolder]);
    }

    private IReadOnlyList<string> ResolveDraftWorkshopIds()
    {
        var combined = new List<string>(_draftWorkshopItemIds.Count + ModEditorItems.Count + MapEditorItems.Count);
        combined.AddRange(_draftWorkshopItemIds);
        combined.AddRange(ModEditorItems.Select(item => item.WorkshopId));
        combined.AddRange(MapEditorItems.Where(item => item.IsActive).Select(item => item.WorkshopId));
        return DistinctNonEmpty(combined);
    }

    private int AllocateModRowId() => _nextModRowId++;

    private int AllocateMapRowId() => _nextMapRowId++;

    private string ResolveModName(string modId) =>
        _metadataByModId.TryGetValue(modId, out var metadata)
            ? metadata.DisplayName
            : modId;

    private string ResolveMapTitle(string mapFolder) =>
        _metadataByMapId.TryGetValue(mapFolder, out var metadata)
            ? metadata.DisplayName
            : mapFolder;

    private string ResolveWorkshopIdForMod(string modId, IReadOnlyList<string> fallbackWorkshopIds)
    {
        if (_metadataByModId.TryGetValue(modId, out var metadata) && !string.IsNullOrWhiteSpace(metadata.WorkshopId))
        {
            return metadata.WorkshopId!;
        }

        return fallbackWorkshopIds.FirstOrDefault() ?? string.Empty;
    }

    private string ResolveWorkshopIdForMap(string mapFolder, IReadOnlyList<string> fallbackWorkshopIds)
    {
        if (_metadataByMapId.TryGetValue(mapFolder, out var metadata) && !string.IsNullOrWhiteSpace(metadata.WorkshopId))
        {
            return metadata.WorkshopId!;
        }

        return fallbackWorkshopIds.FirstOrDefault() ?? string.Empty;
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
    }

    private void ReplaceWorkshopTags(IReadOnlyList<string> availableTags, IReadOnlyList<string> selectedTags)
    {
        var selected = selectedTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        AvailableWorkshopTags.Clear();
        foreach (var tag in availableTags
                     .Concat(selectedTags)
                     .Where(tag => !string.IsNullOrWhiteSpace(tag))
                     .Select(tag => tag.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
        {
            AvailableWorkshopTags.Add(new WorkshopTagChipViewModel(tag, selected.Contains(tag)));
        }

        SyncWorkshopTagInputFromSelection();
        OnPropertyChanged(nameof(HasWorkshopTags));
        OnPropertyChanged(nameof(WorkshopTagSummary));
    }

    private IReadOnlyList<string> ParseSelectedWorkshopTags() =>
        WorkshopTagInput
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void ToggleWorkshopTag(WorkshopTagChipViewModel? chip)
    {
        if (chip is null)
        {
            return;
        }

        chip.IsSelected = !chip.IsSelected;
        SyncWorkshopTagInputFromSelection();
        if (_workshopSearchResult is not null && SelectedProfile is not null)
        {
            _ = SearchWorkshopCatalogAsync();
        }
    }

    private void SyncWorkshopTagInputFromSelection()
    {
        WorkshopTagInput = string.Join(", ",
            AvailableWorkshopTags
                .Where(tag => tag.IsSelected)
                .Select(tag => tag.Tag)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase));
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
        AvailableWorkshopTags.Clear();
        SelectedWorkshopPreview = null;
        IsWorkshopPreviewModalOpen = false;
        _searchVersion++;
        OnPropertyChanged(nameof(HasWorkshopSearchResults));
        OnPropertyChanged(nameof(HasNoWorkshopSearchResults));
        OnPropertyChanged(nameof(HasWorkshopSearchDiagnostics));
        OnPropertyChanged(nameof(HasNoWorkshopSearchDiagnostics));
        OnPropertyChanged(nameof(HasWorkshopTags));
        OnPropertyChanged(nameof(WorkshopTagSummary));
    }

    private void CloseWorkshopPreviewModal()
    {
        IsWorkshopPreviewModalOpen = false;
    }

    private void Reset()
    {
        _catalog = null;
        _lastScanResult = null;
        _workshopSearchResult = null;
        _draftWorkshopItemIds = [];
        _metadataByModId.Clear();
        _metadataByMapId.Clear();
        _nextModRowId = 1;
        _nextMapRowId = 1;
        HasStoredDraft = false;
        ReplaceModRows([]);
        ReplaceMapRows([]);
        ReplaceSavedPresets([]);
        ResetWorkshopBrowserState();
        SelectedEditorPreview = null;
        SelectedModEditorItem = null;
        SearchQuery = string.Empty;
        SearchMode = WorkshopCatalogSearchMode.Both;
        SearchFilter = WorkshopCatalogSearchFilter.All;
        WorkshopTagInput = string.Empty;
        SteamWebApiKey = string.Empty;
        NewPresetName = string.Empty;
        EditorMode = ModsMapsEditorMode.Browse;
        LoadStatus = "Select a profile to load Mods & Maps.";
        MarkClean("Mods & Maps settings are in sync.");
        NotifyComputedState();
    }

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(DraftBannerText));
        OnPropertyChanged(nameof(LoadoutSummary));
        OnPropertyChanged(nameof(BrowserSummary));
        OnPropertyChanged(nameof(DraftStateSummary));
        OnPropertyChanged(nameof(ScanSummary));
        OnPropertyChanged(nameof(CatalogSummaryText));
        OnPropertyChanged(nameof(HasStoredDraft));
        OnPropertyChanged(nameof(HasSavedPresets));
        OnPropertyChanged(nameof(HasNoSavedPresets));
        OnPropertyChanged(nameof(HasWorkshopSearchResults));
        OnPropertyChanged(nameof(HasNoWorkshopSearchResults));
        OnPropertyChanged(nameof(HasWorkshopSearchDiagnostics));
        OnPropertyChanged(nameof(HasNoWorkshopSearchDiagnostics));
        OnPropertyChanged(nameof(HasWorkshopTags));
        OnPropertyChanged(nameof(HasModEditorItems));
        OnPropertyChanged(nameof(HasNoModEditorItems));
        OnPropertyChanged(nameof(HasMapEditorItems));
        OnPropertyChanged(nameof(HasNoMapEditorItems));
        OnPropertyChanged(nameof(WorkshopTagSummary));
        OnPropertyChanged(nameof(HasSteamWebApiKeyConfigured));
        OnPropertyChanged(nameof(ShowSteamApiKeyEditor));
        OnPropertyChanged(nameof(ShowSteamApiKeyRemoveOnly));
        OnPropertyChanged(nameof(ShowSteamApiKeyHelper));
        OnPropertyChanged(nameof(CanApplyWorkshopPreview));
        OnPropertyChanged(nameof(CanApplyWorkshopPreviewWithDependencies));
        OnPropertyChanged(nameof(CanSaveNamedPreset));
        OnPropertyChanged(nameof(IsBrowseMode));
        OnPropertyChanged(nameof(IsLiveEditorMode));
        OnPropertyChanged(nameof(SelectedWorkshopApplyLabel));
        OnPropertyChanged(nameof(SelectedWorkshopApplyWithDependenciesLabel));
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

    private static string BuildPresetHash(WorkshopPreset preset)
    {
        var source = string.Join("|",
            string.Join(",", DistinctNonEmpty(preset.WorkshopItemIds)),
            string.Join(",", DistinctNonEmpty(preset.EnabledModIds)),
            string.Join(",", DistinctNonEmpty(preset.MapFolders)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private static IReadOnlyList<string> DistinctNonEmpty(IEnumerable<string> values)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var candidate = value?.Trim();
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
            {
                normalized.Add(candidate);
            }
        }

        return normalized;
    }

    private static string ChooseBetterName(string? current, string? fallback, string defaultValue)
    {
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            return current.Trim();
        }

        return defaultValue;
    }

    private static string ChooseBetterWorkshopId(string? current, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }

        return current?.Trim() ?? string.Empty;
    }

    partial void OnSearchModeChanged(WorkshopCatalogSearchMode value)
    {
        NotifyComputedState();
    }

    partial void OnSearchFilterChanged(WorkshopCatalogSearchFilter value)
    {
        NotifyComputedState();
    }

    partial void OnWorkshopTagInputChanged(string value)
    {
        OnPropertyChanged(nameof(WorkshopTagSummary));
    }

    partial void OnNewPresetNameChanged(string value)
    {
        SaveNamedPresetCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSaveNamedPreset));
    }

    partial void OnEditorModeChanged(ModsMapsEditorMode value)
    {
        OnPropertyChanged(nameof(IsBrowseMode));
        OnPropertyChanged(nameof(IsLiveEditorMode));
        if (!_isApplyingState && SelectedProfile is not null)
        {
            HasStoredDraft = true;
            RefreshDirtyState();
            _ = PersistDraftAsync(null, updateStatus: false);
        }
    }

    partial void OnSelectedModEditorItemChanged(ModEditorItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedModDetailTitle));
        OnPropertyChanged(nameof(SelectedModDetailModId));
        OnPropertyChanged(nameof(SelectedModDetailWorkshopId));
        OnPropertyChanged(nameof(SelectedModDetailState));
        OnPropertyChanged(nameof(SelectedModDetailInstallState));
        OnPropertyChanged(nameof(SelectedModDetailMapFolders));
        OnPropertyChanged(nameof(SelectedModDetailDependencyIds));
        OnPropertyChanged(nameof(SelectedModDetailDescription));
        _ = LoadSelectedModPreviewAsync(value);
    }

    private sealed record WorkshopMetadata(
        string DisplayName,
        string? WorkshopId,
        bool IsInstalled,
        IReadOnlyList<string> DependencyModIds,
        IReadOnlyList<string> MapFolders);

    public sealed partial class ModEditorItemViewModel(
        int rowId,
        string modName,
        string modId,
        string workshopId,
        bool isActive,
        int sortOrder,
        IReadOnlyList<string> dependencyModIds,
        IReadOnlyList<string> mapFolders,
        bool isInstalled) : ObservableObject
    {
        public int RowId { get; } = rowId;

        [ObservableProperty]
        private string modName = modName;

        [ObservableProperty]
        private string modId = modId;

        [ObservableProperty]
        private string workshopId = workshopId;

        [ObservableProperty]
        private bool isActive = isActive;

        [ObservableProperty]
        private int sortOrder = sortOrder;

        [ObservableProperty]
        private IReadOnlyList<string> dependencyModIds = dependencyModIds;

        [ObservableProperty]
        private IReadOnlyList<string> mapFolders = mapFolders;

        [ObservableProperty]
        private bool isInstalled = isInstalled;

        [ObservableProperty]
        private int? activeOrder;

        public string DisplayTitle => string.IsNullOrWhiteSpace(ModName) ? ModId : ModName;

        public bool HasWorkshopId => !string.IsNullOrWhiteSpace(WorkshopId);

        public string ActiveStateLabel => IsActive ? "Active" : "Inactive";

        public string ActiveButtonLabel => IsActive ? "[x] Active" : "[ ] Inactive";

        public string InstallStateLabel => IsInstalled ? "Installed locally" : "Not installed";

        public string LoadOrderLabel => ActiveOrder is int value ? $"#{value:00}" : "Saved only";

        partial void OnModNameChanged(string value) => OnPropertyChanged(nameof(DisplayTitle));

        partial void OnWorkshopIdChanged(string value) => OnPropertyChanged(nameof(HasWorkshopId));

        partial void OnIsActiveChanged(bool value)
        {
            OnPropertyChanged(nameof(ActiveStateLabel));
            OnPropertyChanged(nameof(ActiveButtonLabel));
            OnPropertyChanged(nameof(LoadOrderLabel));
        }

        partial void OnIsInstalledChanged(bool value) => OnPropertyChanged(nameof(InstallStateLabel));

        partial void OnActiveOrderChanged(int? value) => OnPropertyChanged(nameof(LoadOrderLabel));
    }

    public sealed partial class MapEditorItemViewModel(
        int rowId,
        string title,
        string mapFolder,
        string workshopId,
        bool isActive,
        int sortOrder,
        bool isInstalled) : ObservableObject
    {
        public int RowId { get; } = rowId;

        [ObservableProperty]
        private string title = title;

        [ObservableProperty]
        private string mapFolder = mapFolder;

        [ObservableProperty]
        private string workshopId = workshopId;

        [ObservableProperty]
        private bool isActive = isActive;

        [ObservableProperty]
        private int sortOrder = sortOrder;

        [ObservableProperty]
        private bool isInstalled = isInstalled;

        [ObservableProperty]
        private int? activeOrder;

        public string ActiveButtonLabel => IsActive ? "[x] Active" : "[ ] Inactive";

        public string InstallStateLabel => IsInstalled ? "Installed locally" : "Not installed";

        public string LoadOrderLabel => ActiveOrder is int value ? $"#{value:00}" : "Saved only";

        partial void OnIsActiveChanged(bool value)
        {
            OnPropertyChanged(nameof(ActiveButtonLabel));
            OnPropertyChanged(nameof(LoadOrderLabel));
        }

        partial void OnIsInstalledChanged(bool value) => OnPropertyChanged(nameof(InstallStateLabel));

        partial void OnActiveOrderChanged(int? value) => OnPropertyChanged(nameof(LoadOrderLabel));
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

    public sealed partial class WorkshopTagChipViewModel(string tag, bool isSelected) : ObservableObject
    {
        public string Tag { get; } = tag;

        [ObservableProperty]
        private bool isSelected = isSelected;

        public string ButtonLabel => IsSelected ? $"[x] {Tag}" : Tag;

        partial void OnIsSelectedChanged(bool value)
        {
            OnPropertyChanged(nameof(ButtonLabel));
        }
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

        public string PreviewLabel => IsCollection ? "Preview Collection" : "Preview";

        public IReadOnlyList<string> ModIds => Item.ModIds;

        public IReadOnlyList<string> MapFolders => Item.MapFolders;

        public IReadOnlyList<string> DependencyModIds => Item.DependencyModIds ?? [];

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

        public Bitmap? PreviewImage => Item.PreviewImage;

        public bool HasPreviewImage => Item.HasPreviewImage;

        public IReadOnlyList<string> ModIds => Item.ModIds;

        public IReadOnlyList<string> MapFolders => Item.MapFolders;

        public IReadOnlyList<string> DependencyModIds => Item.DependencyModIds;

        public IReadOnlyList<WorkshopCatalogPreviewChildDto> CollectionChildren { get; private set; } = [];

        public IReadOnlyList<WorkshopCatalogPreviewChildDto> DependencyChildren { get; private set; } = [];

        public IReadOnlyDictionary<string, string> ModNamesById { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [ObservableProperty]
        private IReadOnlyList<string> workshopItemIdsToAdd = [];

        [ObservableProperty]
        private IReadOnlyList<string> modIdsToAdd = [];

        [ObservableProperty]
        private IReadOnlyList<string> mapFoldersToAdd = [];

        [ObservableProperty]
        private IReadOnlyList<string> dependencyWorkshopItemIdsToAdd = [];

        [ObservableProperty]
        private IReadOnlyList<string> dependencyModIdsToAdd = [];

        [ObservableProperty]
        private IReadOnlyList<string> dependencyMapFoldersToAdd = [];

        public bool HasChanges => WorkshopItemIdsToAdd.Count > 0 || ModIdsToAdd.Count > 0 || MapFoldersToAdd.Count > 0;

        public bool HasDependencyChanges => DependencyWorkshopItemIdsToAdd.Count > 0 || DependencyModIdsToAdd.Count > 0 || DependencyMapFoldersToAdd.Count > 0;

        public string ApplyLabel => Item.IsCollection ? "Add Collection To Editor" : "Add To Editor";

        public static async Task<WorkshopPreviewViewModel> CreateAsync(
            WorkshopCatalogPreviewDto preview,
            ILauncherRuntime runtime,
            CancellationToken cancellationToken)
        {
            var item = new WorkshopCatalogItemViewModel(preview.Item);
            await item.LoadImageAsync(runtime, cancellationToken);

            return new WorkshopPreviewViewModel(item)
            {
                WorkshopItemIdsToAdd = preview.WorkshopItemIdsToAdd,
                ModIdsToAdd = preview.ModIdsToAdd,
                MapFoldersToAdd = preview.MapFoldersToAdd,
                CollectionChildren = preview.CollectionChildren ?? [],
                DependencyChildren = preview.DependencyChildren ?? [],
                DependencyWorkshopItemIdsToAdd = preview.DependencyWorkshopItemIdsToAdd ?? [],
                DependencyModIdsToAdd = preview.DependencyModIdsToAdd ?? [],
                DependencyMapFoldersToAdd = preview.DependencyMapFoldersToAdd ?? [],
                ModNamesById = preview.ModNamesById ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            };
        }

        public void Dispose() => Item.Dispose();
    }
}
