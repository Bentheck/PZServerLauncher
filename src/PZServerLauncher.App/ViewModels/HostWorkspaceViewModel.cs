using System.ComponentModel;

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
        Legacy.PropertyChanged += OnLegacyPropertyChanged;
    }

    public MainWindowViewModel Legacy { get; }

    public string HostStatusSummary => Legacy.HostSummary;

    public string HostLifecycleSummary => Legacy.StatusMessage;

    public string HostStartupSummary => Legacy.HostStartWithWindows
        ? "Windows will start the host automatically at sign-in."
        : "Windows startup is disabled until you opt in.";

    public string HostStartupLabel => Legacy.HostStartWithWindows
        ? "Start with Windows: On"
        : "Start with Windows: Off";

    public string HostShutdownSummary => "Stop Host ends only the orchestration process. Stop All + Host shuts down managed servers first, then closes the host.";

    public string HostOperatorSummary => Legacy.HostStartWithWindows
        ? "Recommended for always-on operators who want the host ready immediately after login."
        : "Enable startup when you want this machine to behave like a persistent server controller.";

    public string HostActionSummary => "Use Save to persist startup behavior, or shut the host down directly when you are finished administering the machine.";

    private void OnLegacyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.HostSummary) ||
            e.PropertyName == nameof(MainWindowViewModel.StatusMessage) ||
            e.PropertyName == nameof(MainWindowViewModel.HostStartWithWindows))
        {
            OnPropertyChanged(nameof(HostStatusSummary));
            OnPropertyChanged(nameof(HostLifecycleSummary));
            OnPropertyChanged(nameof(HostStartupSummary));
            OnPropertyChanged(nameof(HostStartupLabel));
            OnPropertyChanged(nameof(HostOperatorSummary));
            OnPropertyChanged(nameof(HostActionSummary));
        }
    }
}
