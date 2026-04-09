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
    private string? _sourceSha256;
    private bool _isApplyingState;

    public GeneralWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient)
        : base(
            ProfileWorkspacePageIds.General,
            "General",
            "Public listing, core world access, server browser identity, and launcher runtime controls.",
            "General settings are in sync.",
            legacy,
            ["Public identity", "World access", "Spawn and loot", "Respawn and cleanup", "Survival rules", "Safehouses", "Factions and trade", "Ports", "Runtime controls"])
    {
        _hostApiClient = hostApiClient;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to edit real Project Zomboid server settings."
        : $"Basic server settings for {SelectedProfile.DisplayName}.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string ProfileNamespace => SelectedProfile?.EditableServerName ?? "No namespace selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string WorkspaceSummary => SelectedProfile is null
        ? "Choose a server to edit the settings most hosts care about first."
        : $"{SelectedProfile.DisplayName} saves real Project Zomboid .ini values for name, player access, gameplay basics, safehouses, factions, and core ports.";

    public string ActionSummary => RequiresAdvancedFilesFallback
        ? "Structured editing is temporarily unavailable for this file. Use Advanced Files for raw recovery."
        : CanEdit
            ? "Change the fields you need, then apply them to write the active server .ini. Use drafts if you are not ready yet."
            : IsLoading
                ? "Loading structured General settings from the host..."
                : "General settings are not currently editable.";

    public ObservableCollection<string> FieldErrors { get; } = [];

    public bool HasFieldErrors => FieldErrors.Count > 0;

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    [ObservableProperty]
    private string loadStatus = "Select a profile to load the structured General editor.";

    [ObservableProperty]
    private string publicName = string.Empty;

    [ObservableProperty]
    private string publicDescription = string.Empty;

    [ObservableProperty]
    private bool isPublic;

    [ObservableProperty]
    private bool isOpen;

    [ObservableProperty]
    private string maxPlayers = string.Empty;

    [ObservableProperty]
    private bool pvpEnabled;

    [ObservableProperty]
    private bool pauseWhenEmpty;

    [ObservableProperty]
    private bool globalChatEnabled;

    [ObservableProperty]
    private string welcomeMessage = string.Empty;

    [ObservableProperty]
    private string spawnItems = string.Empty;

    [ObservableProperty]
    private string lootRespawnHours = string.Empty;

    [ObservableProperty]
    private string lootRespawnMaxItems = string.Empty;

    [ObservableProperty]
    private bool constructionPreventsLootRespawn;

    [ObservableProperty]
    private bool respawnWithSelf;

    [ObservableProperty]
    private bool respawnWithOther;

    [ObservableProperty]
    private string worldItemRemovalHours = string.Empty;

    [ObservableProperty]
    private string worldItemRemovalList = string.Empty;

    [ObservableProperty]
    private bool sleepAllowed;

    [ObservableProperty]
    private bool sleepNeeded;

    [ObservableProperty]
    private bool noFire;

    [ObservableProperty]
    private bool announceDeath;

    [ObservableProperty]
    private bool dropWhitelistOnDeath;

    [ObservableProperty]
    private bool allowSledgehammerDestruction;

    [ObservableProperty]
    private bool playerSafehouse;

    [ObservableProperty]
    private bool adminSafehouse;

    [ObservableProperty]
    private bool safehouseAllowTrespass;

    [ObservableProperty]
    private bool safehouseAllowFire;

    [ObservableProperty]
    private bool safehouseAllowLoot;

    [ObservableProperty]
    private bool safehouseAllowRespawn;

    [ObservableProperty]
    private bool safehouseAllowNonResidential;

    [ObservableProperty]
    private bool disableSafehouseWhenPlayerConnected;

    [ObservableProperty]
    private bool disableSafehouseWhenPlayerDisconnected;

    [ObservableProperty]
    private string safehouseDaysToClaim = string.Empty;

    [ObservableProperty]
    private string safehouseRemovalHours = string.Empty;

    [ObservableProperty]
    private bool factionEnabled;

    [ObservableProperty]
    private string factionDaysToCreate = string.Empty;

    [ObservableProperty]
    private string factionPlayersForTag = string.Empty;

    [ObservableProperty]
    private bool allowTradeUi;

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
        NotifyComputedState();
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
        NotifyComputedState();
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
        NotifyComputedState();
    }

    private void ApplyValues(IReadOnlyDictionary<string, string?> values)
    {
        _isApplyingState = true;
        try
        {
            PublicName = GetValue(values, ".server.public-name");
            PublicDescription = GetValue(values, ".server.public-description");
            IsPublic = bool.TryParse(GetValue(values, ".server.public"), out var isPublic) && isPublic;
            IsOpen = bool.TryParse(GetValue(values, ".server.open"), out var isOpen) && isOpen;
            MaxPlayers = GetValue(values, ".server.max-players");
            PvpEnabled = bool.TryParse(GetValue(values, ".server.pvp"), out var pvp) && pvp;
            PauseWhenEmpty = bool.TryParse(GetValue(values, ".server.pause-empty"), out var pause) && pause;
            GlobalChatEnabled = bool.TryParse(GetValue(values, ".server.global-chat"), out var globalChat) && globalChat;
            WelcomeMessage = GetValue(values, ".server.welcome-message");
            SpawnItems = GetValue(values, ".server.spawn-items");
            LootRespawnHours = GetValue(values, ".server.loot-respawn-hours");
            LootRespawnMaxItems = GetValue(values, ".server.loot-respawn-max-items");
            ConstructionPreventsLootRespawn = bool.TryParse(GetValue(values, ".server.construction-prevents-loot-respawn"), out var constructionPreventsLootRespawn) && constructionPreventsLootRespawn;
            RespawnWithSelf = bool.TryParse(GetValue(values, ".server.respawn-with-self"), out var respawnWithSelf) && respawnWithSelf;
            RespawnWithOther = bool.TryParse(GetValue(values, ".server.respawn-with-other"), out var respawnWithOther) && respawnWithOther;
            WorldItemRemovalHours = GetValue(values, ".server.world-item-removal-hours");
            WorldItemRemovalList = GetValue(values, ".server.world-item-removal-list");
            SleepAllowed = bool.TryParse(GetValue(values, ".server.sleep-allowed"), out var sleepAllowed) && sleepAllowed;
            SleepNeeded = bool.TryParse(GetValue(values, ".server.sleep-needed"), out var sleepNeeded) && sleepNeeded;
            NoFire = bool.TryParse(GetValue(values, ".server.no-fire"), out var noFire) && noFire;
            AnnounceDeath = bool.TryParse(GetValue(values, ".server.announce-death"), out var announceDeath) && announceDeath;
            DropWhitelistOnDeath = bool.TryParse(GetValue(values, ".server.drop-whitelist-on-death"), out var dropWhitelistOnDeath) && dropWhitelistOnDeath;
            AllowSledgehammerDestruction = bool.TryParse(GetValue(values, ".server.allow-sledgehammer-destruction"), out var allowSledgehammerDestruction) && allowSledgehammerDestruction;
            PlayerSafehouse = bool.TryParse(GetValue(values, ".server.player-safehouse"), out var playerSafehouse) && playerSafehouse;
            AdminSafehouse = bool.TryParse(GetValue(values, ".server.admin-safehouse"), out var adminSafehouse) && adminSafehouse;
            SafehouseAllowTrespass = bool.TryParse(GetValue(values, ".server.safehouse-allow-trespass"), out var safehouseAllowTrespass) && safehouseAllowTrespass;
            SafehouseAllowFire = bool.TryParse(GetValue(values, ".server.safehouse-allow-fire"), out var safehouseAllowFire) && safehouseAllowFire;
            SafehouseAllowLoot = bool.TryParse(GetValue(values, ".server.safehouse-allow-loot"), out var safehouseAllowLoot) && safehouseAllowLoot;
            SafehouseAllowRespawn = bool.TryParse(GetValue(values, ".server.safehouse-allow-respawn"), out var safehouseAllowRespawn) && safehouseAllowRespawn;
            SafehouseAllowNonResidential = bool.TryParse(GetValue(values, ".server.safehouse-allow-non-residential"), out var safehouseAllowNonResidential) && safehouseAllowNonResidential;
            DisableSafehouseWhenPlayerConnected = bool.TryParse(GetValue(values, ".server.disable-safehouse-when-player-connected"), out var disableSafehouseWhenPlayerConnected) && disableSafehouseWhenPlayerConnected;
            DisableSafehouseWhenPlayerDisconnected = bool.TryParse(GetValue(values, ".server.disable-safehouse-when-player-disconnected"), out var disableSafehouseWhenPlayerDisconnected) && disableSafehouseWhenPlayerDisconnected;
            SafehouseDaysToClaim = GetValue(values, ".server.safehouse-days-to-claim");
            SafehouseRemovalHours = GetValue(values, ".server.safehouse-removal-hours");
            FactionEnabled = bool.TryParse(GetValue(values, ".server.faction-enabled"), out var factionEnabled) && factionEnabled;
            FactionDaysToCreate = GetValue(values, ".server.faction-days-to-create");
            FactionPlayersForTag = GetValue(values, ".server.faction-players-for-tag");
            AllowTradeUi = bool.TryParse(GetValue(values, ".server.allow-trade-ui"), out var allowTradeUi) && allowTradeUi;
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
            [$"{prefix}.server.public-name"] = PublicName,
            [$"{prefix}.server.public-description"] = PublicDescription,
            [$"{prefix}.server.public"] = IsPublic.ToString(),
            [$"{prefix}.server.open"] = IsOpen.ToString(),
            [$"{prefix}.server.max-players"] = MaxPlayers,
            [$"{prefix}.server.pvp"] = PvpEnabled.ToString(),
            [$"{prefix}.server.pause-empty"] = PauseWhenEmpty.ToString(),
            [$"{prefix}.server.global-chat"] = GlobalChatEnabled.ToString(),
            [$"{prefix}.server.welcome-message"] = WelcomeMessage,
            [$"{prefix}.server.spawn-items"] = SpawnItems,
            [$"{prefix}.server.loot-respawn-hours"] = LootRespawnHours,
            [$"{prefix}.server.loot-respawn-max-items"] = LootRespawnMaxItems,
            [$"{prefix}.server.construction-prevents-loot-respawn"] = ConstructionPreventsLootRespawn.ToString(),
            [$"{prefix}.server.respawn-with-self"] = RespawnWithSelf.ToString(),
            [$"{prefix}.server.respawn-with-other"] = RespawnWithOther.ToString(),
            [$"{prefix}.server.world-item-removal-hours"] = WorldItemRemovalHours,
            [$"{prefix}.server.world-item-removal-list"] = WorldItemRemovalList,
            [$"{prefix}.server.sleep-allowed"] = SleepAllowed.ToString(),
            [$"{prefix}.server.sleep-needed"] = SleepNeeded.ToString(),
            [$"{prefix}.server.no-fire"] = NoFire.ToString(),
            [$"{prefix}.server.announce-death"] = AnnounceDeath.ToString(),
            [$"{prefix}.server.drop-whitelist-on-death"] = DropWhitelistOnDeath.ToString(),
            [$"{prefix}.server.allow-sledgehammer-destruction"] = AllowSledgehammerDestruction.ToString(),
            [$"{prefix}.server.player-safehouse"] = PlayerSafehouse.ToString(),
            [$"{prefix}.server.admin-safehouse"] = AdminSafehouse.ToString(),
            [$"{prefix}.server.safehouse-allow-trespass"] = SafehouseAllowTrespass.ToString(),
            [$"{prefix}.server.safehouse-allow-fire"] = SafehouseAllowFire.ToString(),
            [$"{prefix}.server.safehouse-allow-loot"] = SafehouseAllowLoot.ToString(),
            [$"{prefix}.server.safehouse-allow-respawn"] = SafehouseAllowRespawn.ToString(),
            [$"{prefix}.server.safehouse-allow-non-residential"] = SafehouseAllowNonResidential.ToString(),
            [$"{prefix}.server.disable-safehouse-when-player-connected"] = DisableSafehouseWhenPlayerConnected.ToString(),
            [$"{prefix}.server.disable-safehouse-when-player-disconnected"] = DisableSafehouseWhenPlayerDisconnected.ToString(),
            [$"{prefix}.server.safehouse-days-to-claim"] = SafehouseDaysToClaim,
            [$"{prefix}.server.safehouse-removal-hours"] = SafehouseRemovalHours,
            [$"{prefix}.server.faction-enabled"] = FactionEnabled.ToString(),
            [$"{prefix}.server.faction-days-to-create"] = FactionDaysToCreate,
            [$"{prefix}.server.faction-players-for-tag"] = FactionPlayersForTag,
            [$"{prefix}.server.allow-trade-ui"] = AllowTradeUi.ToString(),
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
            PublicName = string.Empty;
            PublicDescription = string.Empty;
            IsPublic = false;
            IsOpen = false;
            MaxPlayers = string.Empty;
            PvpEnabled = false;
            PauseWhenEmpty = false;
            GlobalChatEnabled = false;
            WelcomeMessage = string.Empty;
            SpawnItems = string.Empty;
            LootRespawnHours = string.Empty;
            LootRespawnMaxItems = string.Empty;
            ConstructionPreventsLootRespawn = false;
            RespawnWithSelf = false;
            RespawnWithOther = false;
            WorldItemRemovalHours = string.Empty;
            WorldItemRemovalList = string.Empty;
            SleepAllowed = false;
            SleepNeeded = false;
            NoFire = false;
            AnnounceDeath = false;
            DropWhitelistOnDeath = false;
            AllowSledgehammerDestruction = false;
            PlayerSafehouse = false;
            AdminSafehouse = false;
            SafehouseAllowTrespass = false;
            SafehouseAllowFire = false;
            SafehouseAllowLoot = false;
            SafehouseAllowRespawn = false;
            SafehouseAllowNonResidential = false;
            DisableSafehouseWhenPlayerConnected = false;
            DisableSafehouseWhenPlayerDisconnected = false;
            SafehouseDaysToClaim = string.Empty;
            SafehouseRemovalHours = string.Empty;
            FactionEnabled = false;
            FactionDaysToCreate = string.Empty;
            FactionPlayersForTag = string.Empty;
            AllowTradeUi = false;
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
        NotifyComputedState();
    }

    partial void OnPublicNameChanged(string value) => NotifyFieldEdited();
    partial void OnPublicDescriptionChanged(string value) => NotifyFieldEdited();
    partial void OnIsPublicChanged(bool value) => NotifyFieldEdited();
    partial void OnIsOpenChanged(bool value) => NotifyFieldEdited();
    partial void OnMaxPlayersChanged(string value) => NotifyFieldEdited();
    partial void OnPvpEnabledChanged(bool value) => NotifyFieldEdited();
    partial void OnPauseWhenEmptyChanged(bool value) => NotifyFieldEdited();
    partial void OnGlobalChatEnabledChanged(bool value) => NotifyFieldEdited();
    partial void OnWelcomeMessageChanged(string value) => NotifyFieldEdited();
    partial void OnSpawnItemsChanged(string value) => NotifyFieldEdited();
    partial void OnLootRespawnHoursChanged(string value) => NotifyFieldEdited();
    partial void OnLootRespawnMaxItemsChanged(string value) => NotifyFieldEdited();
    partial void OnConstructionPreventsLootRespawnChanged(bool value) => NotifyFieldEdited();
    partial void OnRespawnWithSelfChanged(bool value) => NotifyFieldEdited();
    partial void OnRespawnWithOtherChanged(bool value) => NotifyFieldEdited();
    partial void OnWorldItemRemovalHoursChanged(string value) => NotifyFieldEdited();
    partial void OnWorldItemRemovalListChanged(string value) => NotifyFieldEdited();
    partial void OnSleepAllowedChanged(bool value) => NotifyFieldEdited();
    partial void OnSleepNeededChanged(bool value) => NotifyFieldEdited();
    partial void OnNoFireChanged(bool value) => NotifyFieldEdited();
    partial void OnAnnounceDeathChanged(bool value) => NotifyFieldEdited();
    partial void OnDropWhitelistOnDeathChanged(bool value) => NotifyFieldEdited();
    partial void OnAllowSledgehammerDestructionChanged(bool value) => NotifyFieldEdited();
    partial void OnPlayerSafehouseChanged(bool value) => NotifyFieldEdited();
    partial void OnAdminSafehouseChanged(bool value) => NotifyFieldEdited();
    partial void OnSafehouseAllowTrespassChanged(bool value) => NotifyFieldEdited();
    partial void OnSafehouseAllowFireChanged(bool value) => NotifyFieldEdited();
    partial void OnSafehouseAllowLootChanged(bool value) => NotifyFieldEdited();
    partial void OnSafehouseAllowRespawnChanged(bool value) => NotifyFieldEdited();
    partial void OnSafehouseAllowNonResidentialChanged(bool value) => NotifyFieldEdited();
    partial void OnDisableSafehouseWhenPlayerConnectedChanged(bool value) => NotifyFieldEdited();
    partial void OnDisableSafehouseWhenPlayerDisconnectedChanged(bool value) => NotifyFieldEdited();
    partial void OnSafehouseDaysToClaimChanged(string value) => NotifyFieldEdited();
    partial void OnSafehouseRemovalHoursChanged(string value) => NotifyFieldEdited();
    partial void OnFactionEnabledChanged(bool value) => NotifyFieldEdited();
    partial void OnFactionDaysToCreateChanged(string value) => NotifyFieldEdited();
    partial void OnFactionPlayersForTagChanged(string value) => NotifyFieldEdited();
    partial void OnAllowTradeUiChanged(bool value) => NotifyFieldEdited();
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
        NotifyComputedState();
    }

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(ProfileNamespace));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(ActionSummary));
        OnPropertyChanged(nameof(HasFieldErrors));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(RequiresAdvancedFilesFallback));
    }
}
