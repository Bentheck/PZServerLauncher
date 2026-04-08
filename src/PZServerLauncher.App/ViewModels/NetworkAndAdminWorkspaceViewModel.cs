using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class NetworkAndAdminWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private SettingsCatalogDto? _catalog;
    private string? _sourceSha256;
    private bool _isApplyingState;

    public NetworkAndAdminWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient)
        : base(
            ProfileWorkspacePageIds.NetworkAndAdmin,
            "Network & Admin",
            "Bind address and admin identity settings for the selected profile.",
            "Network & Admin settings are in sync.",
            legacy,
            ["Bind IP", "Admin username", "Write-only admin password"])
    {
        _hostApiClient = hostApiClient;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to load Network & Admin settings."
        : $"Network and admin settings for {SelectedProfile.DisplayName}.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string WorkspaceSummary => SelectedProfile is null
        ? "Choose a profile to unlock network and admin controls."
        : $"{SelectedProfile.DisplayName} can now be tuned for bind IP, admin identity, and launcher-managed startup.";

    public string ActionSummary => DraftsDisabled
        ? "Drafts are disabled so the write-only admin password never lands in workspace storage."
        : CanEdit
            ? "Apply settings to update the active profile. Reload if you want to discard local edits."
            : IsLoading
                ? "Loading structured network settings from the host..."
                : "Network & Admin settings are not currently editable.";

    public ObservableCollection<string> FieldErrors { get; } = [];

    public bool HasFieldErrors => FieldErrors.Count > 0;

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    [ObservableProperty]
    private string loadStatus = "Select a profile to load Network & Admin settings.";

    [ObservableProperty]
    private string catalogSummary = "No structured catalog loaded.";

    [ObservableProperty]
    private bool requiresAdvancedFilesFallback;

    [ObservableProperty]
    private string fallbackReason = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool canEdit;

    [ObservableProperty]
    private bool supportsDrafts;

    public bool DraftsDisabled => !SupportsDrafts;

    [ObservableProperty]
    private string bindIp = string.Empty;

    [ObservableProperty]
    private string adminUsername = string.Empty;

    [ObservableProperty]
    private string adminPassword = string.Empty;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        _ = LoadAsync(profile);
        NotifyComputedState();
    }

    public override async Task SaveDraftAsync()
    {
        LoadStatus = "Drafts are disabled on Network & Admin so the write-only admin password is never persisted.";
        await Task.CompletedTask;
    }

    public override async Task DiscardDraftAsync()
    {
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
            ProfileWorkspacePageIds.NetworkAndAdmin,
            BuildValues(),
            _sourceSha256,
            false,
            null);

        var result = await _hostApiClient.SaveSettingsPageAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.NetworkAndAdmin, payload);
        if (result is null)
        {
            LoadStatus = "Network & Admin settings could not be saved.";
            return;
        }

        ApplyValidation(result.Validation);
        if (!result.Validation.IsValid || result.Validation.RequiresAdvancedFilesFallback)
        {
            LoadStatus = result.Validation.FallbackReason ?? "Network & Admin settings need attention before they can be saved.";
            return;
        }

        ApplyValueSet(result.ValueSet, $"Saved Network & Admin settings for {SelectedProfile.DisplayName}.");
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
        LoadStatus = $"Loading Network & Admin settings for {profile.DisplayName}...";

        try
        {
            _catalog = await _hostApiClient.GetSettingsCatalogAsync(profile.ProfileId);
            var page = _catalog?.Pages.FirstOrDefault(candidate => string.Equals(candidate.PageId, ProfileWorkspacePageIds.NetworkAndAdmin, StringComparison.Ordinal));
            var valueSet = await _hostApiClient.GetSettingsPageAsync(profile.ProfileId, ProfileWorkspacePageIds.NetworkAndAdmin);

            CatalogSummary = _catalog is null
                ? "No structured catalog available."
                : $"{_catalog.CatalogId} v{_catalog.CatalogVersion} | {_catalog.Branch}";
            SupportsDrafts = page?.SupportsDrafts ?? false;
            OnPropertyChanged(nameof(DraftsDisabled));

            if (valueSet is null)
            {
                Reset();
                LoadStatus = "Network & Admin settings could not be loaded.";
                return;
            }

            ApplyValueSet(valueSet, "Network & Admin settings loaded from the local host.");
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

    private void ApplyValueSet(SettingsValueSetDto valueSet, string cleanMessage)
    {
        _sourceSha256 = valueSet.SourceSha256;
        RequiresAdvancedFilesFallback = valueSet.RequiresAdvancedFilesFallback;
        FallbackReason = valueSet.FallbackReason ?? string.Empty;
        CanEdit = !valueSet.RequiresAdvancedFilesFallback;
        ApplyValues(valueSet.Values);
        MarkClean(cleanMessage);
        LoadStatus = valueSet.RequiresAdvancedFilesFallback
            ? valueSet.FallbackReason ?? "Structured Network & Admin editing is unavailable for this file."
            : cleanMessage;
        NotifyComputedState();
    }

    private void ApplyValues(IReadOnlyDictionary<string, string?> values)
    {
        _isApplyingState = true;
        try
        {
            BindIp = GetValue(values, ".network.bind-ip");
            AdminUsername = GetValue(values, ".network.admin-user");
            AdminPassword = string.Empty;
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
            [$"{prefix}.network.bind-ip"] = BindIp,
            [$"{prefix}.network.admin-user"] = AdminUsername,
            [$"{prefix}.network.admin-password"] = AdminPassword,
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
        SupportsDrafts = false;
        OnPropertyChanged(nameof(DraftsDisabled));
        FieldErrors.Clear();
        OnPropertyChanged(nameof(HasFieldErrors));

        _isApplyingState = true;
        try
        {
            BindIp = string.Empty;
            AdminUsername = string.Empty;
            AdminPassword = string.Empty;
        }
        finally
        {
            _isApplyingState = false;
        }

        MarkClean("Network & Admin settings are in sync.");
        NotifyComputedState();
    }

    partial void OnBindIpChanged(string value) => NotifyFieldEdited();
    partial void OnAdminUsernameChanged(string value) => NotifyFieldEdited();
    partial void OnAdminPasswordChanged(string value) => NotifyFieldEdited();

    private void NotifyFieldEdited()
    {
        if (_isApplyingState || !CanEdit)
        {
            return;
        }

        MarkDirty("Unsaved changes in Network & Admin.");
        LoadStatus = "Network & Admin settings changed locally. Apply them to update the active profile.";
        NotifyComputedState();
    }

    partial void OnSupportsDraftsChanged(bool value)
    {
        OnPropertyChanged(nameof(DraftsDisabled));
    }

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(ActionSummary));
        OnPropertyChanged(nameof(DraftsDisabled));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(RequiresAdvancedFilesFallback));
    }
}
