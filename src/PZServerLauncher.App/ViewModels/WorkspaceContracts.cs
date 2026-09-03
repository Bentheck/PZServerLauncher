using System.Windows.Input;

namespace PZServerLauncher.App.ViewModels;

public interface IWorkspacePageHeader
{
    string PageTitle { get; }

    string PageSummary { get; }
}

public interface IWorkspaceDirtyState
{
    bool HasUnsavedChanges { get; }

    string DirtyStateMessage { get; }

    Task SaveDraftAsync();

    Task DiscardDraftAsync();
}

public interface IWorkspaceRefreshable
{
    Task RefreshPageAsync();
}

public sealed class WorkspaceCommandViewModel
{
    public WorkspaceCommandViewModel(string label, ICommand command, string? tooltip = null)
    {
        Label = label;
        Command = command;
        Tooltip = tooltip ?? label;
    }

    public string Label { get; }

    public ICommand Command { get; }

    public string Tooltip { get; }
}

public interface IWorkspaceCommandProvider
{
    IReadOnlyList<WorkspaceCommandViewModel> PrimaryCommands { get; }

    IReadOnlyList<WorkspaceCommandViewModel> SecondaryCommands { get; }

    IReadOnlyList<WorkspaceCommandViewModel> DangerCommands { get; }
}

public interface IProfileWorkspacePage : IWorkspacePageHeader
{
    string PageId { get; }

    void SetSelectedProfile(ProfileCardViewModel? profile);
}
