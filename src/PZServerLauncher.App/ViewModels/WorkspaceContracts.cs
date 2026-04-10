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

public interface IProfileWorkspacePage : IWorkspacePageHeader
{
    string PageId { get; }

    void SetSelectedProfile(ProfileCardViewModel? profile);
}
