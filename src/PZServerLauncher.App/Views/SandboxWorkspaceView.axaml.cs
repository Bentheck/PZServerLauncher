using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PZServerLauncher.App.ViewModels;

namespace PZServerLauncher.App.Views;

public partial class SandboxWorkspaceView : UserControl
{
    public SandboxWorkspaceView()
    {
        InitializeComponent();
    }

    private void OnOpenCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SandboxWorkspaceViewModel viewModel ||
            sender is not Control control ||
            control.DataContext is not SandboxCategoryViewModel category)
        {
            return;
        }

        viewModel.SelectCategoryCommand.Execute(category);
        Dispatcher.UIThread.Post(() =>
        {
            this.FindControl<Border>("SelectedCategoryAnchor")?.BringIntoView();
        }, DispatcherPriority.Background);
    }
}
