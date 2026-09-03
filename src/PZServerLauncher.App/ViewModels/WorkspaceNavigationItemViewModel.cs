using CommunityToolkit.Mvvm.ComponentModel;

namespace PZServerLauncher.App.ViewModels;

public partial class WorkspaceNavigationItemViewModel : ViewModelBase
{
    public WorkspaceNavigationItemViewModel(string key, string title, string summary, string iconKey = "Page", string shortStatus = "")
    {
        Key = key;
        Title = title;
        Summary = summary;
        IconKey = iconKey;
        ShortStatus = shortStatus;
    }

    public string Key { get; }

    public string Title { get; }

    public string Summary { get; }

    public string IconKey { get; }

    public string ShortStatus { get; }

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isEnabled = true;
}
