namespace PZServerLauncher.App.ViewModels;

public sealed class RemoteAccessWorkspaceViewModel : WorkspacePageViewModelBase
{
    public RemoteAccessWorkspaceViewModel(MainWindowViewModel legacy)
        : base(
            "Remote Access",
            "Optional HTTPS admin setup, local validation, and Windows Firewall rule control for the web workspace.",
            "Remote access settings are in sync.",
            ["HTTPS binding", "Local self-test", "Firewall rule", "Desktop-driven setup"])
    {
        Legacy = legacy;
    }

    public MainWindowViewModel Legacy { get; }
}
