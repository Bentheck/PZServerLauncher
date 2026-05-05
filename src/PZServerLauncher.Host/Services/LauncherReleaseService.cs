using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.Host.Services;

public sealed class LauncherReleaseService
{
    private const string RepositoryOwner = "Bentheck";
    private const string RepositoryName = "PZServerLauncher";
    private static readonly TimeSpan SuccessCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly string? _currentVersionOverride;
    private readonly Func<DateTimeOffset> _utcNow;

    public LauncherReleaseService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        string? currentVersionOverride = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _currentVersionOverride = currentVersionOverride;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<LauncherUpdateStatusDto> GetStatusAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{RepositoryOwner}/{RepositoryName}:launcher-release-status";
        if (!forceRefresh &&
            _memoryCache.TryGetValue(cacheKey, out LauncherUpdateStatusDto? cachedStatus) &&
            cachedStatus is not null)
        {
            return cachedStatus;
        }

        var checkedAtUtc = _utcNow();
        var currentVersion = NormalizeVersion(_currentVersionOverride ?? ResolveCurrentVersion());

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(LauncherReleaseService));
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(10));

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd($"PZServerLauncher/{currentVersion}");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            var release = await JsonSerializer.DeserializeAsync<GitHubLatestRelease>(contentStream, SerializerOptions, timeoutSource.Token)
                ?? throw new InvalidOperationException("GitHub did not return a latest release payload.");

            var latestVersion = NormalizeVersion(release.TagName ?? release.Name ?? string.Empty);
            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                throw new InvalidOperationException("GitHub latest release did not include a recognizable version.");
            }

            var state = CompareVersions(currentVersion, latestVersion) < 0
                ? LauncherUpdateState.UpdateAvailable
                : LauncherUpdateState.UpToDate;

            var status = new LauncherUpdateStatusDto(
                state,
                currentVersion,
                latestVersion,
                string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name.Trim(),
                string.IsNullOrWhiteSpace(release.HtmlUrl) ? BuildReleasesPageUrl() : release.HtmlUrl.Trim(),
                release.PublishedAtUtc,
                checkedAtUtc,
                state == LauncherUpdateState.UpdateAvailable
                    ? $"Version {latestVersion} is available on GitHub. You're on {currentVersion}."
                    : $"You're on {currentVersion}. The latest stable release is {latestVersion}.");

            _memoryCache.Set(cacheKey, status, SuccessCacheDuration);
            return status;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            var unavailableStatus = new LauncherUpdateStatusDto(
                LauncherUpdateState.Unavailable,
                currentVersion,
                null,
                null,
                BuildReleasesPageUrl(),
                null,
                checkedAtUtc,
                "Unable to check GitHub releases right now.");

            _memoryCache.Set(cacheKey, unavailableStatus, FailureCacheDuration);
            return unavailableStatus;
        }
    }

    private static string ResolveCurrentVersion()
    {
        var informationalVersion = typeof(LauncherReleaseService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return typeof(LauncherReleaseService).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    private static string NormalizeVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0.0.0";
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var prereleaseIndex = normalized.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            normalized = normalized[..prereleaseIndex];
        }

        var metadataIndex = normalized.IndexOf('+');
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return normalized.Trim();
    }

    private static int CompareVersions(string currentVersion, string latestVersion)
    {
        if (TryParseComparableVersion(currentVersion, out var currentComparable) &&
            TryParseComparableVersion(latestVersion, out var latestComparable))
        {
            return currentComparable.CompareTo(latestComparable);
        }

        return string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase)
            ? 0
            : string.Compare(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseComparableVersion(string value, out Version comparableVersion)
    {
        comparableVersion = new Version(0, 0, 0, 0);

        var segments = NormalizeVersion(value)
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        var numericSegments = new List<string>(4);
        foreach (var segment in segments.Take(4))
        {
            var digits = new string(segment.TakeWhile(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
            {
                return false;
            }

            numericSegments.Add(digits);
        }

        while (numericSegments.Count < 4)
        {
            numericSegments.Add("0");
        }

        if (Version.TryParse(string.Join('.', numericSegments), out var parsedVersion) &&
            parsedVersion is not null)
        {
            comparableVersion = parsedVersion;
            return true;
        }

        return false;
    }

    private static string BuildReleasesPageUrl() =>
        $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases";

    private sealed record GitHubLatestRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAtUtc);
}
