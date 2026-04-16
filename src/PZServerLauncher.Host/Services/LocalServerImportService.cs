using System.Globalization;
using System.Text.RegularExpressions;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Host.Services;

public sealed partial class LocalServerImportService(
    ProfileStore profileStore,
    WorkshopPresetScannerService workshopScannerService,
    string? cacheRootOverride = null,
    string? installDirectoryOverride = null,
    ProjectZomboidBranch? branchOverride = null,
    string? launcherRootOverride = null)
{
    public async Task<IReadOnlyList<ProfileImportCandidateDto>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var cacheRoot = cacheRootOverride ?? GetDefaultCacheRoot();
        var serverDirectory = Path.Combine(cacheRoot, "Server");
        if (!Directory.Exists(serverDirectory))
        {
            return [];
        }

        var installProbe = DetectInstall(installDirectoryOverride, branchOverride);
        var existingProfiles = await profileStore.ListAsync(cancellationToken);
        var existingKeys = existingProfiles
            .Select(profile => BuildExistingKey(profile.ServerName, profile.CacheDirectory))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<ProfileImportCandidateDto>();
        foreach (var iniPath in Directory.GetFiles(serverDirectory, "*.ini", SearchOption.TopDirectoryOnly))
        {
            var serverName = Path.GetFileNameWithoutExtension(iniPath);
            if (string.IsNullOrWhiteSpace(serverName))
            {
                continue;
            }

            var settings = ParseIni(iniPath);
            var preset = new WorkshopPreset
            {
                WorkshopItemIds = GetListValue(settings, "WorkshopItems", allowCommaFallback: true),
                EnabledModIds = GetListValue(settings, "Mods", allowCommaFallback: true),
                MapFolders = GetListValue(settings, "Map", allowCommaFallback: false),
            };

            var scanResult = workshopScannerService.Scan(installProbe.InstallDirectory, preset);
            var diagnostics = new List<string>(scanResult.Diagnostics);
            if (string.IsNullOrWhiteSpace(installProbe.InstallDirectory))
            {
                diagnostics.Add("No dedicated server install was detected. Import will stage this server in the launcher's managed install folder until you run install or update.");
            }

            results.Add(new ProfileImportCandidateDto(
                CreateCandidateId(serverName),
                HumanizeName(serverName),
                serverName,
                cacheRoot,
                installProbe.InstallDirectory,
                installProbe.Branch,
                scanResult.Preset,
                diagnostics,
                existingKeys.Contains(BuildExistingKey(serverName, cacheRoot))));
        }

        return results
            .OrderBy(candidate => candidate.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<ServerProfile> ImportAsync(string candidateId, CancellationToken cancellationToken = default)
    {
        var candidate = (await DiscoverAsync(cancellationToken))
            .FirstOrDefault(x => string.Equals(x.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Import candidate '{candidateId}' was not found.");

        var settings = ParseIni(Path.Combine(candidate.CacheDirectory, "Server", $"{candidate.ServerName}.ini"));
        var existingProfiles = await profileStore.ListAsync(cancellationToken);
        var profileId = EnsureUniqueProfileId(candidate.ServerName, existingProfiles.Select(x => x.ProfileId));

        var defaultPort = ParseInt(settings, "DefaultPort", 16261);
        var fallbackUdpPort = defaultPort < 65535 ? defaultPort + 1 : 65534;

        var profile = new ServerProfile
        {
            ProfileId = profileId,
            DisplayName = candidate.DisplayName,
            ServerName = candidate.ServerName,
            CacheDirectory = candidate.CacheDirectory,
            InstallDirectory = candidate.InstallDirectory ?? GetFallbackInstallDirectory(profileId),
            Branch = candidate.Branch,
            DefaultPort = defaultPort,
            UdpPort = ParseInt(settings, "UDPPort", fallbackUdpPort),
            RconPort = ParseInt(settings, "RCONPort", 27015),
            UseSteam = true,
            AdminUsername = GetValue(settings, "AdminUsername"),
            BindIp = GetValue(settings, "BindIP"),
            PreferredMemoryInGigabytes = 6,
            StartWithHost = false,
            AutoRestartOnCrash = true,
            WorkshopPreset = candidate.WorkshopPreset,
            BackupPolicy = BackupPolicy.Default,
        };

        return await profileStore.UpsertAsync(profile, cancellationToken);
    }

    private static string GetDefaultCacheRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid");

    private string GetFallbackInstallDirectory(string profileId) =>
        ServerProfileFactory.BuildInstallDirectory(profileId, launcherRootOverride);

    private static IReadOnlyDictionary<string, string> ParseIni(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) ||
                trimmed.StartsWith(';') ||
                trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        return values;
    }

    private static IReadOnlyList<string> GetListValue(IReadOnlyDictionary<string, string> values, string key, bool allowCommaFallback)
    {
        if (!values.TryGetValue(key, out var rawValue))
        {
            return [];
        }

        if (rawValue.Contains(';'))
        {
            return rawValue.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        if (allowCommaFallback)
        {
            return rawValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        return string.IsNullOrWhiteSpace(rawValue)
            ? []
            : [rawValue.Trim()];
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static int ParseInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string BuildExistingKey(string serverName, string cacheDirectory) =>
        $"{cacheDirectory}|{serverName}";

    private static string EnsureUniqueProfileId(string serverName, IEnumerable<string> existingProfileIds)
    {
        var baseId = Slugify(serverName);
        var candidate = baseId;
        var index = 2;
        var existing = existingProfileIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        while (existing.Contains(candidate))
        {
            candidate = $"{baseId}-{index}";
            index++;
        }

        return candidate;
    }

    private static string HumanizeName(string serverName)
    {
        var words = Regex.Split(serverName, @"[-_]+")
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(part.ToLowerInvariant()));

        return string.Join(" ", words);
    }

    private static string CreateCandidateId(string serverName) => Slugify(serverName);

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = NonSlugCharactersRegex().Replace(normalized, "-");
        normalized = MultipleDashRegex().Replace(normalized, "-");
        return normalized.Trim('-');
    }

    private static InstallProbeResult DetectInstall(string? installDirectoryOverride, ProjectZomboidBranch? branchOverride)
    {
        if (!string.IsNullOrWhiteSpace(installDirectoryOverride))
        {
            return new InstallProbeResult(installDirectoryOverride, branchOverride ?? ProjectZomboidBranch.Unstable42);
        }

        foreach (var libraryRoot in DiscoverSteamLibraries())
        {
            var installDirectory = Path.Combine(libraryRoot, "steamapps", "common", "Project Zomboid Dedicated Server");
            if (!File.Exists(Path.Combine(installDirectory, "StartServer64.bat")))
            {
                continue;
            }

            return new InstallProbeResult(installDirectory, ProjectZomboidBranch.Unstable42);
        }

        return new InstallProbeResult(null, ProjectZomboidBranch.Unstable42);
    }

    private static IReadOnlyList<string> DiscoverSteamLibraries()
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var steamRoot in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
                 })
        {
            if (!Directory.Exists(steamRoot))
            {
                continue;
            }

            results.Add(steamRoot);

            var libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersPath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(libraryFoldersPath))
            {
                var match = LibraryPathRegex().Match(line);
                if (match.Success)
                {
                    results.Add(match.Groups["path"].Value.Replace(@"\\", @"\"));
                }
            }
        }

        return results.ToList();
    }

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonSlugCharactersRegex();

    [GeneratedRegex(@"-+", RegexOptions.Compiled)]
    private static partial Regex MultipleDashRegex();

    [GeneratedRegex("\"path\"\\s+\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LibraryPathRegex();

    private sealed record InstallProbeResult(string? InstallDirectory, ProjectZomboidBranch Branch);
}
