using Avalonia.Controls;
using Avalonia.Interactivity;
using PZServerLauncher.App.Services;

namespace PZServerLauncher.App.Views;

public partial class ShutdownWarningDialog : Window
{
    public ShutdownWarningDialog()
    {
        InitializeComponent();
    }

    private void KeepRunningButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(ShutdownWarningChoice.KeepRunning);
    }

    private void SendToTrayButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(ShutdownWarningChoice.SendToTray);
    }

    private void CloseEverythingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(ShutdownWarningChoice.CloseEverything);
    }
}
