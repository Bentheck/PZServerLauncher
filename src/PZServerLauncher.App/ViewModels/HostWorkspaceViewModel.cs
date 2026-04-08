namespace PZServerLauncher.App.ViewModels;

public sealed class HostWorkspaceViewModel : WorkspacePageViewModelBase
{
    public HostWorkspaceViewModel(MainWindowViewModel legacy)
        : base(
            "Host",
            "Local host lifecycle controls, startup behavior, and high-level runtime status.",
            "Host settings are in sync.",
            ["Start with Windows", "Stop host", "Stop all + host", "Exit desktop"])
    {
        Legacy = legacy;
    }

    public MainWindowViewModel Legacy { get; }
}
