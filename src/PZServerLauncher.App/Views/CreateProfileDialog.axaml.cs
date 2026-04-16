using Avalonia.Controls;
using Avalonia.Interactivity;
using PZServerLauncher.App.Services;
using PZServerLauncher.App.ViewModels;

namespace PZServerLauncher.App.Views;

public partial class CreateProfileDialog : Window
{
    public CreateProfileDialog()
    {
        InitializeComponent();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void CreateButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreateProfileDialogViewModel viewModel &&
            viewModel.TryBuildRequest(out var request))
        {
            Close(request);
        }
    }
}
