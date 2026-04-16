using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PZServerLauncher.App.ViewModels;
using PZServerLauncher.App.Views;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Runtime;

namespace PZServerLauncher.App.Services;

public static class ScreenshotCaptureRunner
{
    private static readonly string[] ProfilePageSequence =
    [
        ProfileWorkspacePageIds.Overview,
        ProfileWorkspacePageIds.InstallAndUpdate,
        ProfileWorkspacePageIds.General,
        ProfileWorkspacePageIds.Sandbox,
        ProfileWorkspacePageIds.ModsAndMaps,
        ProfileWorkspacePageIds.NetworkAndAdmin,
        ProfileWorkspacePageIds.Backups,
        ProfileWorkspacePageIds.Logs,
        ProfileWorkspacePageIds.AdvancedFiles,
    ];

    public static async Task RunAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow window,
        WorkspaceShellViewModel shell,
        string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        await WaitForInitialLoadAsync(shell);
        var createdTemporaryProfileId = await EnsureProfileForCaptureAsync(shell);

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                window.Width = 1920;
                window.Height = 1080;
                window.WindowState = WindowState.Normal;
                window.Activate();
            });

            await Task.Delay(1200);

            var sequence = 1;
            foreach (var item in shell.GlobalNavigation.Where(item => item.IsEnabled))
            {
                await shell.SelectGlobalPageCommand.ExecuteAsync(item);
                await Task.Delay(900);

                await CaptureWindowAsync(window, outputDirectory, $"{sequence:00}-{Sanitize(item.Title)}.png");
                sequence++;

                if (!string.Equals(item.Key, WorkspacePageIds.Profiles, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!shell.Profiles.HasProfiles || shell.Profiles.Profiles.Count == 0)
                {
                    continue;
                }

                var profile = shell.Profiles.Profiles[0];
                foreach (var pageId in ProfilePageSequence)
                {
                    shell.Profiles.NavigateToProfile(profile.ProfileId, pageId);
                    await Task.Delay(900);

                    var sectionTitle = shell.Profiles.SectionItems
                        .FirstOrDefault(candidate => string.Equals(candidate.Key, pageId, StringComparison.Ordinal))
                        ?.Title ?? pageId;

                    await CaptureWindowAsync(
                        window,
                        outputDirectory,
                        $"{sequence:00}-profile-{Sanitize(sectionTitle)}.png");
                    sequence++;
                }
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(createdTemporaryProfileId))
            {
                await using var launcherRuntime = new LauncherRuntime(LauncherStorageRootResolver.Resolve());
                await launcherRuntime.DeleteProfileAsync(createdTemporaryProfileId);
                await shell.RefreshLegacyCommand.ExecuteAsync(null);
            }
        }

        desktop.Shutdown(0);
    }

    private static async Task<string?> EnsureProfileForCaptureAsync(WorkspaceShellViewModel shell)
    {
        await shell.RefreshLegacyCommand.ExecuteAsync(null);
        await Task.Delay(1200);

        if (shell.Profiles.HasProfiles && shell.Profiles.Profiles.Count > 0)
        {
            return null;
        }

        await using var launcherRuntime = new LauncherRuntime(LauncherStorageRootResolver.Resolve());
        var createdProfile = await launcherRuntime.CreateStarterProfileAsync(
            "Main Server",
            ServerProfileFactory.DefaultStarterPort,
            ServerProfileFactory.DefaultPreferredMemoryInGigabytes,
            ServerProfileFactory.DefaultMaxPlayers);

        await shell.RefreshLegacyCommand.ExecuteAsync(null);
        await Task.Delay(1200);

        return createdProfile?.ProfileId;
    }

    private static async Task WaitForInitialLoadAsync(WorkspaceShellViewModel shell)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < timeoutAt)
        {
            if (!shell.Legacy.IsBusy &&
                !string.Equals(shell.ActorSummary, "Loading workspace capabilities...", StringComparison.Ordinal) &&
                !string.Equals(shell.Legacy.StatusMessage, "Starting up...", StringComparison.Ordinal))
            {
                await Task.Delay(1200);
                return;
            }

            await Task.Delay(250);
        }

        await Task.Delay(1200);
    }

    private static async Task CaptureWindowAsync(Window window, string outputDirectory, string fileName)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            window.InvalidateVisual();
        }, DispatcherPriority.Render);

        await Task.Delay(150);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var pixelWidth = Math.Max(1, (int)Math.Ceiling(window.Bounds.Width * window.RenderScaling));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(window.Bounds.Height * window.RenderScaling));

            using var bitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight));
            bitmap.Render(window);
            bitmap.Save(Path.Combine(outputDirectory, fileName));
        }, DispatcherPriority.Render);
    }

    private static string Sanitize(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .ToLowerInvariant()
            .Select(character => invalidCharacters.Contains(character)
                ? '-'
                : character is ' ' or '&' or '/'
                    ? '-'
                    : character)
            .ToArray());

        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        return sanitized.Trim('-');
    }
}
