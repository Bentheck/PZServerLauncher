using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class LauncherReleaseServiceTests
{
    [Fact]
    public async Task GetStatusAsync_ParsesLatestReleaseAndDetectsUpdateAvailable()
    {
        var handler = new RecordingHttpMessageHandler((request, _, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"tag_name":"v1.2.1","name":"PZ Server Launcher 1.2.1","html_url":"https://github.com/Bentheck/PZServerLauncher/releases/tag/v1.2.1","published_at":"2026-05-05T12:00:00Z"}
                    """),
            };
            return Task.FromResult(response);
        });
        using var httpClient = new HttpClient(handler);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new LauncherReleaseService(new FakeHttpClientFactory(httpClient), memoryCache, currentVersionOverride: "1.2.0");

        var status = await service.GetStatusAsync();

        Assert.Equal(LauncherUpdateState.UpdateAvailable, status.State);
        Assert.Equal("1.2.0", status.CurrentVersion);
        Assert.Equal("1.2.1", status.LatestVersion);
        Assert.Equal("PZ Server Launcher 1.2.1", status.ReleaseTitle);
        Assert.Equal("https://github.com/Bentheck/PZServerLauncher/releases/tag/v1.2.1", status.ReleasePageUrl);
        Assert.Equal(new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero), status.PublishedAtUtc);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Contains("/releases/latest", request.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStatusAsync_NormalizesVersionLabelsBeforeComparison()
    {
        var handler = new RecordingHttpMessageHandler((_, _, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"tag_name":"1.2.0","name":"PZ Server Launcher 1.2.0","html_url":"https://github.com/Bentheck/PZServerLauncher/releases/tag/v1.2.0","published_at":"2026-05-01T12:00:00Z"}
                    """),
            };
            return Task.FromResult(response);
        });
        using var httpClient = new HttpClient(handler);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new LauncherReleaseService(new FakeHttpClientFactory(httpClient), memoryCache, currentVersionOverride: "v1.2.0+local");

        var status = await service.GetStatusAsync();

        Assert.Equal(LauncherUpdateState.UpToDate, status.State);
        Assert.Equal("1.2.0", status.CurrentVersion);
        Assert.Equal("1.2.0", status.LatestVersion);
    }

    [Fact]
    public async Task GetStatusAsync_CachesSuccessfulResultsUntilForcedRefresh()
    {
        var firstRequestCount = 0;
        var firstHandler = new RecordingHttpMessageHandler((_, _, _) =>
        {
            firstRequestCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"tag_name":"v1.2.0","name":"PZ Server Launcher 1.2.0","html_url":"https://github.com/Bentheck/PZServerLauncher/releases/tag/v1.2.0","published_at":"2026-05-01T12:00:00Z"}
                    """),
            };
            return Task.FromResult(response);
        });
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        using var firstHttpClient = new HttpClient(firstHandler);
        var initialService = new LauncherReleaseService(new FakeHttpClientFactory(firstHttpClient), memoryCache, currentVersionOverride: "1.2.0");

        _ = await initialService.GetStatusAsync();
        Assert.Equal(1, firstRequestCount);

        var secondRequestCount = 0;
        var secondHandler = new RecordingHttpMessageHandler((_, _, _) =>
        {
            secondRequestCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"tag_name":"v1.2.0","name":"PZ Server Launcher 1.2.0","html_url":"https://github.com/Bentheck/PZServerLauncher/releases/tag/v1.2.0","published_at":"2026-05-01T12:00:00Z"}
                    """),
            };
            return Task.FromResult(response);
        });
        using var secondHttpClient = new HttpClient(secondHandler);
        var refreshService = new LauncherReleaseService(new FakeHttpClientFactory(secondHttpClient), memoryCache, currentVersionOverride: "1.2.0");

        _ = await refreshService.GetStatusAsync();
        Assert.Equal(0, secondRequestCount);

        _ = await refreshService.GetStatusAsync(forceRefresh: true);
        Assert.Equal(1, secondRequestCount);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsUnavailableOnNetworkFailure()
    {
        var handler = new RecordingHttpMessageHandler((_, _, _) =>
            throw new HttpRequestException("GitHub is unavailable."));
        using var httpClient = new HttpClient(handler);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new LauncherReleaseService(new FakeHttpClientFactory(httpClient), memoryCache, currentVersionOverride: "1.2.0");

        var status = await service.GetStatusAsync();

        Assert.Equal(LauncherUpdateState.Unavailable, status.State);
        Assert.Equal("1.2.0", status.CurrentVersion);
        Assert.Null(status.LatestVersion);
        Assert.Equal("Unable to check GitHub releases right now.", status.StatusMessage);
        Assert.Equal("https://github.com/Bentheck/PZServerLauncher/releases", status.ReleasePageUrl);
    }
}
