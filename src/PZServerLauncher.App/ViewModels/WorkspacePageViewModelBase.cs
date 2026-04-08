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
        SaveDraftCommand = new AsyncRelayCommand(SaveDraftAsync);
        DiscardDraftCommand = new AsyncRelayCommand(DiscardDraftAsync);
    }

    public string PageTitle { get; }

    public virtual string PageSummary { get; }

    public IReadOnlyList<string> Highlights { get; }

    [ObservableProperty]
    private string draftNote = string.Empty;

    [ObservableProperty]
    private bool hasUnsavedChanges;

    [ObservableProperty]
    private string dirtyStateMessage = string.Empty;

    public IAsyncRelayCommand SaveDraftCommand { get; }

    public IAsyncRelayCommand DiscardDraftCommand { get; }

    public void SaveDraft()
    {
        SaveDraftAsync().GetAwaiter().GetResult();
    }

    public void DiscardDraft()
    {
        DiscardDraftAsync().GetAwaiter().GetResult();
    }

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

    public virtual Task SaveDraftAsync()
    {
        MarkClean($"Saved placeholder draft for {PageTitle}.");
        return Task.CompletedTask;
    }

    public virtual Task DiscardDraftAsync()
    {
        if (!string.IsNullOrEmpty(DraftNote))
        {
            DraftNote = string.Empty;
            return Task.CompletedTask;
        }

        MarkClean();
        return Task.CompletedTask;
    }
}
