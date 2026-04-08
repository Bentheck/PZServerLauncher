using CommunityToolkit.Mvvm.ComponentModel;

namespace PZServerLauncher.App.ViewModels;

public partial class ShellItemViewModel : ViewModelBase
{
    public ShellItemViewModel(string key, string title, string detail)
    {
        Key = key;
        this.title = title;
        this.detail = detail;
    }

    public string Key { get; }

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string detail;
}
