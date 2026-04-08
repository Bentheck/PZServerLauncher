using CommunityToolkit.Mvvm.ComponentModel;

namespace PZServerLauncher.App.ViewModels;

public abstract partial class ProfileWorkspacePageViewModelBase(
    string pageId,
    string pageTitle,
    string pageSummary,
    string emptyDirtyMessage,
    MainWindowViewModel legacy,
    IEnumerable<string>? highlights = null)
    : WorkspacePageViewModelBase(pageTitle, pageSummary, emptyDirtyMessage, highlights), IProfileWorkspacePage
{
    protected MainWindowViewModel Legacy { get; } = legacy;

    public string PageId { get; } = pageId;

    public bool HasSelectedProfile => SelectedProfile is not null;

    [ObservableProperty]
    private ProfileCardViewModel? selectedProfile;

    public void SetSelectedProfile(ProfileCardViewModel? profile)
    {
        SelectedProfile = profile;
        OnPropertyChanged(nameof(HasSelectedProfile));
        OnPropertyChanged(nameof(PageSummary));
        OnSelectedProfileChangedCore(profile);
    }

    partial void OnSelectedProfileChanged(ProfileCardViewModel? value)
    {
        OnSelectedProfileChangedCore(value);
    }

    protected virtual void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
    }
}
