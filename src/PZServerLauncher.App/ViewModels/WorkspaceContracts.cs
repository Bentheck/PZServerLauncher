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

    void SaveDraft();

    void DiscardDraft();
}
