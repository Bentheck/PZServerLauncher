using PZServerLauncher.App.Services;

namespace PZServerLauncher.App.ViewModels;

public sealed class ClassicWorkspaceViewModel : ViewModelBase, IWorkspacePageHeader
{
    public ClassicWorkspaceViewModel(MainWindowViewModel legacy)
    {
        Legacy = legacy;
    }

    public MainWindowViewModel Legacy { get; }

    public string PageTitle => "Classic";

    public string PageSummary => "Temporary migration surface that keeps the current monolithic experience reachable while the workspace pages land.";
}
