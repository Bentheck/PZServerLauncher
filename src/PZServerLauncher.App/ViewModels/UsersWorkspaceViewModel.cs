using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class UsersWorkspaceViewModel : WorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private bool _isInitialized;

    public UsersWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient)
        : base(
            "Users",
            "Owner bootstrap and desktop account management for the local host, with the optional web admin reusing the same users.",
            "User settings are in sync.",
            ["Owner bootstrap", "Create users", "Edit roles", "Delete accounts"])
    {
        Legacy = legacy;
        _hostApiClient = hostApiClient;
        Legacy.PropertyChanged += OnLegacyPropertyChanged;
        Users.CollectionChanged += OnUsersCollectionChanged;

        RoleOptions = Enum.GetValues<UserRole>()
            .Where(role => role != UserRole.LocalSystem)
            .Select(role => role.ToString())
            .ToArray();

        ReloadUsersCommand = new AsyncRelayCommand(ReloadUsersAsync, () => CanManageUsers);
        CreateUserCommand = new AsyncRelayCommand(CreateUserAsync, () => CanCreateUser);
        SaveUserCommand = new AsyncRelayCommand<EditableUserRowViewModel>(SaveUserAsync);
        DeleteUserCommand = new AsyncRelayCommand<EditableUserRowViewModel>(DeleteUserAsync);

        _ = InitializeAsync();
    }

    public MainWindowViewModel Legacy { get; }

    public bool OwnerBootstrapConfigured => !Legacy.OwnerBootstrapRequired;

    public string OwnerSummary => Legacy.OwnerSummary;

    public string UsersPageSummary => OwnerBootstrapConfigured
        ? "Desktop account management is now active. Create operators, viewers, or admins before you expose the optional web admin."
        : "Create the initial owner account first, then return here to manage local web-admin users.";

    public string UserCountSummary => OwnerBootstrapConfigured
        ? $"{Users.Count} managed user account(s)."
        : "No managed user accounts yet.";

    public string TwoFactorSummary => OwnerBootstrapConfigured
        ? $"{Users.Count(user => user.RequiresTwoFactor)} account(s) require TOTP for web sign-in."
        : "TOTP enforcement applies after the owner account is created.";

    public string ActionSummary => OwnerBootstrapConfigured
        ? "Use Create User to add new accounts, then Save or Delete from each row as you refine roles."
        : "Use Owner Bootstrap first so the account manager can become active.";

    public string OwnerBootstrapLabel => OwnerBootstrapConfigured
        ? "Configured: On"
        : "Configured: Off";

    public IReadOnlyList<string> RoleOptions { get; }

    public ObservableCollection<EditableUserRowViewModel> Users { get; } = [];

    public bool CanManageUsers => OwnerBootstrapConfigured && !IsBusy;

    public bool HasUsers => Users.Count > 0;

    public bool HasNoUsers => Users.Count == 0;

    public bool CanCreateUser =>
        CanManageUsers &&
        !string.IsNullOrWhiteSpace(CreateUserName) &&
        !string.IsNullOrWhiteSpace(CreateEmail) &&
        !string.IsNullOrWhiteSpace(CreatePassword);

    public IAsyncRelayCommand ReloadUsersCommand { get; }

    public IAsyncRelayCommand CreateUserCommand { get; }

    public IAsyncRelayCommand<EditableUserRowViewModel> SaveUserCommand { get; }

    public IAsyncRelayCommand<EditableUserRowViewModel> DeleteUserCommand { get; }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Owner bootstrap is required before desktop user management becomes available.";

    [ObservableProperty]
    private string createUserName = string.Empty;

    [ObservableProperty]
    private string createEmail = string.Empty;

    [ObservableProperty]
    private string createPassword = string.Empty;

    [ObservableProperty]
    private string createRoleName = nameof(UserRole.Viewer);

    public override async Task SaveDraftAsync()
    {
        if (Legacy.OwnerBootstrapRequired)
        {
            MarkClean();
            return;
        }

        var dirtyRows = Users.Where(row => row.IsDirty).ToArray();
        if (dirtyRows.Length > 0)
        {
            foreach (var row in dirtyRows)
            {
                await SaveUserAsync(row);
            }
        }

        if (IsCreateFormDirty)
        {
            await CreateUserAsync();
        }

        RefreshDirtyState();
    }

    public override Task DiscardDraftAsync()
    {
        ResetCreateForm();

        foreach (var row in Users)
        {
            row.Reset();
        }

        StatusMessage = OwnerBootstrapConfigured
            ? "Discarded unsaved desktop user edits."
            : "Owner bootstrap is required before desktop user management becomes available.";
        RefreshDirtyState();
        return Task.CompletedTask;
    }

    private bool IsCreateFormDirty =>
        !string.IsNullOrWhiteSpace(CreateUserName) ||
        !string.IsNullOrWhiteSpace(CreateEmail) ||
        !string.IsNullOrWhiteSpace(CreatePassword) ||
        !string.Equals(CreateRoleName, nameof(UserRole.Viewer), StringComparison.Ordinal);

    private async Task InitializeAsync()
    {
        _isInitialized = true;
        if (OwnerBootstrapConfigured)
        {
            await ReloadUsersAsync();
        }
    }

    private async Task ReloadUsersAsync()
    {
        if (!OwnerBootstrapConfigured || IsBusy)
        {
            RefreshDirtyState();
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Loading desktop user management...";
            var users = await _hostApiClient.GetUsersAsync(CancellationToken.None) ?? [];

            foreach (var user in Users)
            {
                user.PropertyChanged -= OnUserRowPropertyChanged;
            }

            Users.Clear();
            foreach (var user in users.Select(MapRow))
            {
                user.PropertyChanged += OnUserRowPropertyChanged;
                Users.Add(user);
            }

            StatusMessage = users.Count == 0
                ? "No local web-admin users exist yet."
                : $"Loaded {users.Count} local user account(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasUsers));
            OnPropertyChanged(nameof(HasNoUsers));
            RefreshCommandStates();
            RefreshDirtyState();
        }
    }

    private async Task CreateUserAsync()
    {
        if (!CanCreateUser)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Creating {CreateUserName.Trim()}...";
            var created = await _hostApiClient.CreateUserAsync(
                new CreateUserRequestDto(
                    CreateUserName.Trim(),
                    CreateEmail.Trim(),
                    CreatePassword,
                    [Enum.Parse<UserRole>(CreateRoleName, ignoreCase: true)]),
                CancellationToken.None);

            if (created is not null)
            {
                AddOrReplaceUserRow(MapRow(created));
                StatusMessage = $"Created {created.UserName}.";
            }
            else
            {
                StatusMessage = "User creation did not return a result.";
            }

            ResetCreateForm();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasUsers));
            OnPropertyChanged(nameof(HasNoUsers));
            RefreshCommandStates();
            RefreshDirtyState();
        }
    }

    private async Task SaveUserAsync(EditableUserRowViewModel? row)
    {
        if (row is null || !OwnerBootstrapConfigured || IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Saving {row.UserName.Trim()}...";
            var updated = await _hostApiClient.UpdateUserAsync(
                row.UserId,
                new UpdateUserRequestDto(
                    row.UserName.Trim(),
                    row.Email.Trim(),
                    [Enum.Parse<UserRole>(row.RoleName, ignoreCase: true)]),
                CancellationToken.None);

            if (updated is not null)
            {
                row.MarkSaved(updated.UserName, updated.Email ?? string.Empty, ResolveRoleName(updated), updated.TwoFactorEnabled, updated.Roles.Any(RoleRequiresTwoFactor));
                StatusMessage = $"Saved {updated.UserName}.";
            }
            else
            {
                StatusMessage = $"Saved {row.UserName}.";
                row.MarkSaved();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RefreshCommandStates();
            RefreshDirtyState();
        }
    }

    private async Task DeleteUserAsync(EditableUserRowViewModel? row)
    {
        if (row is null || !OwnerBootstrapConfigured || IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Deleting {row.UserName.Trim()}...";
            await _hostApiClient.DeleteUserAsync(row.UserId, CancellationToken.None);
            row.PropertyChanged -= OnUserRowPropertyChanged;
            Users.Remove(row);
            StatusMessage = $"Deleted {row.UserName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasUsers));
            OnPropertyChanged(nameof(HasNoUsers));
            RefreshCommandStates();
            RefreshDirtyState();
        }
    }

    private void ResetCreateForm()
    {
        CreateUserName = string.Empty;
        CreateEmail = string.Empty;
        CreatePassword = string.Empty;
        CreateRoleName = nameof(UserRole.Viewer);
        RefreshCommandStates();
    }

    private void RefreshDirtyState()
    {
        if (!OwnerBootstrapConfigured)
        {
            MarkClean("Owner bootstrap is required before desktop user management becomes available.");
            return;
        }

        var dirtyRowCount = Users.Count(row => row.IsDirty);
        if (dirtyRowCount > 0)
        {
            MarkDirty($"{dirtyRowCount} desktop user row(s) have unsaved changes.");
            return;
        }

        if (IsCreateFormDirty)
        {
            MarkDirty("New desktop user details have unsaved changes.");
            return;
        }

        MarkClean("User settings are in sync.");
        OnPropertyChanged(nameof(UserCountSummary));
        OnPropertyChanged(nameof(TwoFactorSummary));
        OnPropertyChanged(nameof(ActionSummary));
        OnPropertyChanged(nameof(OwnerSummary));
        OnPropertyChanged(nameof(UsersPageSummary));
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(CanManageUsers));
        OnPropertyChanged(nameof(CanCreateUser));
        ReloadUsersCommand.NotifyCanExecuteChanged();
        CreateUserCommand.NotifyCanExecuteChanged();
    }

    private void OnLegacyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(MainWindowViewModel.OwnerBootstrapRequired))
        {
            OnPropertyChanged(nameof(OwnerBootstrapConfigured));
            OnPropertyChanged(nameof(UsersPageSummary));
            OnPropertyChanged(nameof(OwnerSummary));
            RefreshCommandStates();
            RefreshDirtyState();

            if (_isInitialized && OwnerBootstrapConfigured && Users.Count == 0)
            {
                _ = ReloadUsersAsync();
            }
        }

        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.OwnerSummary) ||
            e.PropertyName == nameof(MainWindowViewModel.OwnerBootstrapRequired))
        {
            OnPropertyChanged(nameof(OwnerSummary));
            OnPropertyChanged(nameof(UsersPageSummary));
        }
    }

    partial void OnCreateUserNameChanged(string value)
    {
        RefreshCommandStates();
        RefreshDirtyState();
    }

    partial void OnCreateEmailChanged(string value)
    {
        RefreshCommandStates();
        RefreshDirtyState();
    }

    partial void OnCreatePasswordChanged(string value)
    {
        RefreshCommandStates();
        RefreshDirtyState();
    }

    partial void OnCreateRoleNameChanged(string value)
    {
        RefreshCommandStates();
        RefreshDirtyState();
    }

    private void OnUserRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(EditableUserRowViewModel.IsDirty))
        {
            RefreshDirtyState();
            OnPropertyChanged(nameof(UserCountSummary));
            OnPropertyChanged(nameof(TwoFactorSummary));
            OnPropertyChanged(nameof(ActionSummary));
        }
    }

    private void OnUsersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasUsers));
        OnPropertyChanged(nameof(HasNoUsers));
        OnPropertyChanged(nameof(UserCountSummary));
        OnPropertyChanged(nameof(TwoFactorSummary));
        OnPropertyChanged(nameof(ActionSummary));
        RefreshDirtyState();
    }

    private void AddOrReplaceUserRow(EditableUserRowViewModel row)
    {
        var existing = Users.FirstOrDefault(candidate => string.Equals(candidate.UserId, row.UserId, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.PropertyChanged -= OnUserRowPropertyChanged;
            var index = Users.IndexOf(existing);
            row.PropertyChanged += OnUserRowPropertyChanged;
            Users[index] = row;
        }
        else
        {
            row.PropertyChanged += OnUserRowPropertyChanged;
            Users.Add(row);
        }
    }

    private static EditableUserRowViewModel MapRow(UserAccountDto user) =>
        new(
            user.UserId,
            user.UserName,
            user.Email ?? string.Empty,
            ResolveRoleName(user),
            user.TwoFactorEnabled,
            user.Roles.Any(RoleRequiresTwoFactor));

    private static string ResolveRoleName(UserAccountDto user)
    {
        var role = user.Roles.FirstOrDefault(candidate => candidate != UserRole.LocalSystem);
        return role == default ? nameof(UserRole.Viewer) : role.ToString();
    }

    private static bool RoleRequiresTwoFactor(UserRole role) =>
        role is UserRole.Owner or UserRole.Admin;

    public sealed partial class EditableUserRowViewModel : ObservableObject
    {
        private string _originalUserName;
        private string _originalEmail;
        private string _originalRoleName;

        public EditableUserRowViewModel(
            string userId,
            string userName,
            string email,
            string roleName,
            bool twoFactorEnabled,
            bool requiresTwoFactor)
        {
            UserId = userId;
            _originalUserName = userName;
            _originalEmail = email;
            _originalRoleName = roleName;
            this.userName = userName;
            this.email = email;
            this.roleName = roleName;
            TwoFactorEnabled = twoFactorEnabled;
            RequiresTwoFactor = requiresTwoFactor;
        }

        public string UserId { get; }

        public bool TwoFactorEnabled { get; private set; }

        public bool RequiresTwoFactor { get; private set; }

        public bool IsDirty =>
            !string.Equals(UserName, _originalUserName, StringComparison.Ordinal) ||
            !string.Equals(Email, _originalEmail, StringComparison.Ordinal) ||
            !string.Equals(RoleName, _originalRoleName, StringComparison.Ordinal);

        [ObservableProperty]
        private string userName;

        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string roleName;

        public void Reset()
        {
            UserName = _originalUserName;
            Email = _originalEmail;
            RoleName = _originalRoleName;
        }

        public void MarkSaved()
        {
            _originalUserName = UserName;
            _originalEmail = Email;
            _originalRoleName = RoleName;
            OnPropertyChanged(nameof(IsDirty));
        }

        public void MarkSaved(string userName, string email, string roleName, bool twoFactorEnabled, bool requiresTwoFactor)
        {
            UserName = userName;
            Email = email;
            RoleName = roleName;
            TwoFactorEnabled = twoFactorEnabled;
            RequiresTwoFactor = requiresTwoFactor;
            _originalUserName = userName;
            _originalEmail = email;
            _originalRoleName = roleName;
            OnPropertyChanged(nameof(TwoFactorEnabled));
            OnPropertyChanged(nameof(RequiresTwoFactor));
            OnPropertyChanged(nameof(IsDirty));
        }

        partial void OnUserNameChanged(string value) => OnPropertyChanged(nameof(IsDirty));

        partial void OnEmailChanged(string value) => OnPropertyChanged(nameof(IsDirty));

        partial void OnRoleNameChanged(string value) => OnPropertyChanged(nameof(IsDirty));
    }
}
