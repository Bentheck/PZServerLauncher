using CommunityToolkit.Mvvm.ComponentModel;

namespace PZServerLauncher.App.ViewModels;

public partial class WorkspaceNavigationItemViewModel : ViewModelBase
{
    public WorkspaceNavigationItemViewModel(string key, string title, string summary)
    {
        Key = key;
        Title = title;
        Summary = summary;
    }

    public string Key { get; }

    public string Title { get; }

    public string Summary { get; }

    [ObservableProperty]
    private bool isSelected;
}
