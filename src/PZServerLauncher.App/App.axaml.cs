using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.App.ViewModels;
using PZServerLauncher.App.Services;
using PZServerLauncher.App.Views;

namespace PZServerLauncher.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = new ServiceCollection()
            .AddSingleton<LocalHostApiClient>()
            .AddSingleton<RuntimeEventStream>()
            .AddSingleton<DesktopShellService>()
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<WorkspaceShellViewModel>()
            .BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<WorkspaceShellViewModel>(),
            };
            desktop.MainWindow = mainWindow;
            _serviceProvider.GetRequiredService<DesktopShellService>().Initialize(desktop, mainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
