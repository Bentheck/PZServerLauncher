using System.ComponentModel;

namespace PZServerLauncher.App.ViewModels;

public sealed class UsersWorkspaceViewModel : WorkspacePageViewModelBase
{
    public UsersWorkspaceViewModel(MainWindowViewModel legacy)
        : base(
            "Users",
            "Owner bootstrap on desktop, with broader role and account management still available in the optional web admin.",
            "User settings are in sync.",
            ["Owner bootstrap", "Role management", "Desktop handoff to web admin"])
    {
        Legacy = legacy;
        Legacy.PropertyChanged += OnLegacyPropertyChanged;
    }

    public MainWindowViewModel Legacy { get; }

    public bool OwnerBootstrapConfigured => !Legacy.OwnerBootstrapRequired;

    private void OnLegacyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(MainWindowViewModel.OwnerBootstrapRequired))
        {
            OnPropertyChanged(nameof(OwnerBootstrapConfigured));
        }
    }
}
