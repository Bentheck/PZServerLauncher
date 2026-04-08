namespace PZServerLauncher.App.ViewModels;

public sealed class WorkspaceSectionViewModel : WorkspacePageViewModelBase
{
    public WorkspaceSectionViewModel(
        string pageTitle,
        string pageSummary,
        string emptyDirtyMessage,
        IEnumerable<string>? highlights = null)
        : base(pageTitle, pageSummary, emptyDirtyMessage, highlights)
    {
    }
}
