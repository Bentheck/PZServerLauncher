using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PZServerLauncher.App.ViewModels;

public abstract partial class WorkspacePageViewModelBase : ViewModelBase, IWorkspacePageHeader, IWorkspaceDirtyState
{
    private readonly string _emptyDirtyMessage;

    protected WorkspacePageViewModelBase(
        string pageTitle,
        string pageSummary,
        string emptyDirtyMessage,
        IEnumerable<string>? highlights = null)
    {
        PageTitle = pageTitle;
        PageSummary = pageSummary;
        _emptyDirtyMessage = emptyDirtyMessage;
        Highlights = highlights?.ToArray() ?? [];
        DirtyStateMessage = emptyDirtyMessage;
        SaveDraftCommand = new RelayCommand(SaveDraft);
        DiscardDraftCommand = new RelayCommand(DiscardDraft);
    }

    public string PageTitle { get; }

    public string PageSummary { get; }

    public IReadOnlyList<string> Highlights { get; }

    [ObservableProperty]
    private string draftNote = string.Empty;

    [ObservableProperty]
    private bool hasUnsavedChanges;

    [ObservableProperty]
    private string dirtyStateMessage = string.Empty;

    public IRelayCommand SaveDraftCommand { get; }

    public IRelayCommand DiscardDraftCommand { get; }

    protected void MarkDirty(string message)
    {
        HasUnsavedChanges = true;
        DirtyStateMessage = message;
    }

    protected void MarkClean(string? message = null)
    {
        HasUnsavedChanges = false;
        DirtyStateMessage = message ?? _emptyDirtyMessage;
    }

    protected virtual void OnDraftEdited(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (HasUnsavedChanges)
            {
                MarkClean();
            }

            return;
        }

        MarkDirty($"Unsaved changes in {PageTitle}.");
    }

    partial void OnDraftNoteChanged(string value)
    {
        OnDraftEdited(value);
    }

    public virtual void SaveDraft()
    {
        MarkClean($"Saved placeholder draft for {PageTitle}.");
    }

    public virtual void DiscardDraft()
    {
        if (!string.IsNullOrEmpty(DraftNote))
        {
            DraftNote = string.Empty;
            return;
        }

        MarkClean();
    }
}
