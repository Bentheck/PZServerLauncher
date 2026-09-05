using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Runtime.Services;

public sealed partial class ModSandboxOptionsService
{
    private const int MaximumDiscoveryCacheEntries = 16;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, WorkshopModIndex> _workshopIndexes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _enabledModIdsByProfile = new(StringComparer.Ordinal);
    private long _workshopIndexRevision;
    private readonly Dictionary<string, Lazy<ModSandboxDiscoveryResult>> _discoveryCache = new(StringComparer.OrdinalIgnoreCase);

    internal long WorkshopIndexBuildCount
    {
        get
        {
            lock (_cacheLock)
            {
                return _workshopIndexRevision;
            }
        }
    }

    public ModSandboxDiscoveryResult Discover(
        ServerProfile profile,
        IReadOnlyList<string> enabledModIds)
    {
        var enabledModIdSnapshot = enabledModIds.ToArray();
        var workshopRoots = ResolveWorkshopRoots(profile.InstallDirectory);
        if (workshopRoots.Count == 0)
        {
            return new ModSandboxDiscoveryResult([], [], enabledModIdSnapshot.Length);
        }

        if (enabledModIdSnapshot.Length == 0)
        {
            RememberEnabledMods(profile.ProfileId, enabledModIdSnapshot);
            return new ModSandboxDiscoveryResult([], [], 0);
        }

        WorkshopModIndex workshopIndex;
        try
        {
            workshopIndex = GetWorkshopIndex(workshopRoots, profile.ProfileId, enabledModIdSnapshot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ModSandboxDiscoveryResult(
                [],
                [$"The local Workshop cache could not be scanned: {exception.Message}"],
                enabledModIdSnapshot.Length);
        }

        var discoveryKey = BuildDiscoveryCacheKey(workshopIndex.Revision, profile.SteamBranch, enabledModIdSnapshot);
        Lazy<ModSandboxDiscoveryResult> cachedDiscovery;
        lock (_cacheLock)
        {
            if (!_discoveryCache.TryGetValue(discoveryKey, out cachedDiscovery!))
            {
                if (_discoveryCache.Count >= MaximumDiscoveryCacheEntries)
                {
                    _discoveryCache.Clear();
                }

                cachedDiscovery = new Lazy<ModSandboxDiscoveryResult>(
                    () => DiscoverCore(profile.SteamBranch, enabledModIdSnapshot, workshopIndex.CandidatesByModId),
                    LazyThreadSafetyMode.ExecutionAndPublication);
                _discoveryCache[discoveryKey] = cachedDiscovery;
            }
        }

        try
        {
            return cachedDiscovery.Value;
        }
        catch
        {
            lock (_cacheLock)
            {
                if (_discoveryCache.TryGetValue(discoveryKey, out var current) && ReferenceEquals(current, cachedDiscovery))
                {
                    _discoveryCache.Remove(discoveryKey);
                }
            }

            throw;
        }
    }

    private void RememberEnabledMods(string profileId, IReadOnlyList<string> enabledModIds)
    {
        lock (_cacheLock)
        {
            _enabledModIdsByProfile[profileId] = enabledModIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static ModSandboxDiscoveryResult DiscoverCore(
        string steamBranch,
        IReadOnlyList<string> enabledModIds,
        IReadOnlyDictionary<string, IReadOnlyList<IndexedModDefinition>> candidatesByModId)
    {
        var diagnostics = new List<string>();
        var sections = new List<StructuredSectionDefinition>();
        var discoveredModCount = 0;

        for (var modIndex = 0; modIndex < enabledModIds.Count; modIndex++)
        {
            var enabledModId = enabledModIds[modIndex];
            var candidates = FindCandidates(candidatesByModId, enabledModId, steamBranch);
            if (candidates.Count == 0)
            {
                diagnostics.Add($"{enabledModId}: no compatible sandbox-options.txt was found in the local Workshop cache.");
                continue;
            }

            var translations = LoadTranslations(candidates);
            var definitions = new Dictionary<string, ParsedModOption>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates.OrderBy(candidate => candidate.Priority))
            {
                foreach (var option in ParseOptions(File.ReadAllText(candidate.OptionsPath), diagnostics, enabledModId))
                {
                    definitions[option.Name] = option;
                }
            }

            if (definitions.Count == 0)
            {
                continue;
            }

            discoveredModCount++;
            var modName = candidates.Last().DisplayName;
            var sourceFilePath = candidates.OrderBy(candidate => candidate.Priority).Last().OptionsPath;
            foreach (var pageGroup in definitions.Values
                         .GroupBy(option => string.IsNullOrWhiteSpace(option.Page) ? enabledModId : option.Page, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => ResolveTranslation(translations, $"Sandbox_{group.Key}") ?? Humanize(group.Key), StringComparer.OrdinalIgnoreCase))
            {
                var pageTitle = ResolveTranslation(translations, $"Sandbox_{pageGroup.Key}") ?? Humanize(pageGroup.Key);
                var sectionTitle = string.Equals(pageTitle, modName, StringComparison.OrdinalIgnoreCase)
                    ? modName
                    : $"{modName}: {pageTitle}";
                var fields = pageGroup
                    .Select(option => BuildField(enabledModId, option, translations))
                    .Where(field => field is not null)
                    .Cast<StructuredFieldDefinition>()
                    .ToArray();
                if (fields.Length == 0)
                {
                    continue;
                }

                sections.Add(new StructuredSectionDefinition(
                    $"mod.{SanitizeId(enabledModId)}.{SanitizeId(pageGroup.Key)}",
                    sectionTitle,
                    fields,
                    "Generated from the installed mod's sandbox option definitions. Changes apply to SandboxVars.lua and require a server restart.",
                    $"mod.{SanitizeId(enabledModId)}",
                    modName,
                    1000 + modIndex,
                    sourceFilePath));
            }
        }

        return new ModSandboxDiscoveryResult(sections, diagnostics, enabledModIds.Count, discoveredModCount);
    }

    internal static IReadOnlyList<ParsedModOption> ParseOptions(
        string content,
        ICollection<string>? diagnostics = null,
        string sourceName = "mod")
    {
        var sanitized = BlockCommentRegex().Replace(content, string.Empty);
        var options = new List<ParsedModOption>();
        foreach (Match match in OptionRegex().Matches(sanitized))
        {
            var name = match.Groups["name"].Value.Trim();
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match propertyMatch in PropertyRegex().Matches(match.Groups["body"].Value))
            {
                properties[propertyMatch.Groups["key"].Value] = Unquote(propertyMatch.Groups["value"].Value.Trim());
            }

            if (!properties.TryGetValue("type", out var type) || string.IsNullOrWhiteSpace(type))
            {
                diagnostics?.Add($"{sourceName}: option '{name}' has no supported type and was skipped.");
                continue;
            }

            options.Add(new ParsedModOption(
                name,
                type.Trim(),
                properties.GetValueOrDefault("default") ?? string.Empty,
                properties.GetValueOrDefault("page") ?? string.Empty,
                properties.GetValueOrDefault("translation") ?? string.Empty,
                properties.GetValueOrDefault("valueTranslation") ?? string.Empty,
                properties.GetValueOrDefault("_tooltip") ?? string.Empty,
                ParseDecimal(properties.GetValueOrDefault("min")),
                ParseDecimal(properties.GetValueOrDefault("max")),
                int.TryParse(properties.GetValueOrDefault("numValues"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numValues)
                    ? Math.Max(0, numValues)
                    : 0));
        }

        return options;
    }

    internal static IReadOnlyDictionary<string, string> ParseTranslations(string content)
    {
        var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in TranslationRegex().Matches(content))
        {
            var value = Regex.Unescape(match.Groups["value"].Value)
                .Replace("<br>", Environment.NewLine, StringComparison.OrdinalIgnoreCase)
                .Trim();
            translations[match.Groups["key"].Value] = value;
        }

        return translations;
    }

    private static StructuredFieldDefinition? BuildField(
        string modId,
        ParsedModOption option,
        IReadOnlyDictionary<string, string> translations)
    {
        var valueKind = option.Type.ToLowerInvariant() switch
        {
            "boolean" => StructuredValueKind.Boolean,
            "integer" => StructuredValueKind.Integer,
            "double" => StructuredValueKind.Number,
            "enum" => StructuredValueKind.Choice,
            "string" => StructuredValueKind.Text,
            _ => (StructuredValueKind?)null,
        };
        if (valueKind is null)
        {
            return null;
        }

        var optionName = option.Name.Split('.').LastOrDefault() ?? option.Name;
        var translationKey = string.IsNullOrWhiteSpace(option.Translation) ? optionName : option.Translation;
        var label = ResolveTranslation(translations, $"Sandbox_{translationKey}") ?? Humanize(optionName);
        var explicitTooltip = string.IsNullOrWhiteSpace(option.TooltipTranslation)
            ? null
            : ResolveTranslation(translations, $"Sandbox_{option.TooltipTranslation}");
        var tooltip = explicitTooltip ?? ResolveTranslation(translations, $"Sandbox_{translationKey}_tooltip");
        var range = option.Minimum is not null || option.Maximum is not null
            ? $"Allowed range: {FormatBound(option.Minimum, "no minimum")} to {FormatBound(option.Maximum, "no maximum")}."
            : null;
        var helpText = string.Join(Environment.NewLine, new[] { tooltip, range }.Where(value => !string.IsNullOrWhiteSpace(value)));

        IReadOnlyList<StructuredFieldOptionDefinition>? choices = null;
        if (valueKind == StructuredValueKind.Choice)
        {
            var valueTranslation = string.IsNullOrWhiteSpace(option.ValueTranslation) ? translationKey : option.ValueTranslation;
            choices = Enumerable.Range(1, option.NumberOfValues)
                .Select(value => new StructuredFieldOptionDefinition(
                    value.ToString(CultureInfo.InvariantCulture),
                    ResolveTranslation(translations, $"Sandbox_{valueTranslation}_option{value}") ?? $"Option {value}",
                    null))
                .ToArray();
        }

        return new StructuredFieldDefinition(
            $"mod.{SanitizeId(modId)}.{SanitizeId(option.Name)}",
            label,
            valueKind.Value,
            new StructuredConfigTarget(ConfigFileKind.SandboxVars, option.Name),
            NormalizeDefault(option.DefaultValue, valueKind.Value),
            true,
            string.IsNullOrWhiteSpace(helpText) ? null : helpText,
            choices,
            option.Minimum,
            option.Maximum);
    }

    private static IReadOnlyList<ModOptionsCandidate> FindCandidates(
        IReadOnlyDictionary<string, IReadOnlyList<IndexedModDefinition>> candidatesByModId,
        string enabledModId,
        string steamBranch)
    {
        if (!candidatesByModId.TryGetValue(enabledModId, out var indexedCandidates))
        {
            return [];
        }

        var matches = indexedCandidates
            .Select(candidate => new ModOptionsCandidate(
                candidate.OptionsPath,
                candidate.ModRoot,
                candidate.DisplayName,
                candidate.Variant,
                GetVariantPriority(candidate.Variant, steamBranch)))
            .Where(candidate => candidate.Priority >= 0)
            .ToArray();

        if (matches.Length == 0)
        {
            return [];
        }

        var rootCandidates = matches.Where(candidate => candidate.Variant == ".").ToArray();
        if (string.Equals(steamBranch, "legacy41", StringComparison.OrdinalIgnoreCase))
        {
            return rootCandidates.Length > 0 ? rootCandidates : [matches.OrderBy(candidate => candidate.Priority).First()];
        }

        var commonCandidates = matches.Where(candidate => string.Equals(candidate.Variant, "common", StringComparison.OrdinalIgnoreCase)).ToArray();
        var versionCandidate = matches
            .Where(candidate => candidate.Variant != "." && !string.Equals(candidate.Variant, "common", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Priority)
            .FirstOrDefault();
        var selected = commonCandidates.Concat(versionCandidate is null ? [] : [versionCandidate]).ToArray();
        return selected.Length > 0 ? selected : rootCandidates;
    }

    private static int GetVariantPriority(string variant, string steamBranch)
    {
        if (variant == ".")
        {
            return 0;
        }

        if (string.Equals(variant, "common", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        var segment = variant.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        if (!TryParseVersion(segment, out var version) || version.Major != 42)
        {
            return -1;
        }

        if (TryParseVersion(steamBranch, out var targetVersion) && version > targetVersion)
        {
            return -1;
        }

        return 1000 + version.Major * 10000 + version.Minor * 100 + Math.Max(0, version.Build);
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        var match = VersionRegex().Match(value);
        return Version.TryParse(match.Success ? match.Groups["version"].Value : string.Empty, out version!);
    }

    private static IReadOnlyDictionary<string, string> LoadTranslations(IEnumerable<ModOptionsCandidate> candidates)
    {
        var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var orderedDirectories = candidates
            .OrderBy(candidate => candidate.Priority)
            .SelectMany(candidate => new[]
            {
                candidate.ModRoot,
                Directory.GetParent(Path.GetDirectoryName(candidate.OptionsPath)!)!.FullName,
            })
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in orderedDirectories)
        {
            var translationDirectory = Path.Combine(directory, "media", "lua", "shared", "Translate");
            if (!Directory.Exists(translationDirectory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(translationDirectory, "Sandbox_EN.txt", SearchOption.AllDirectories))
            {
                foreach (var entry in ParseTranslations(File.ReadAllText(path)))
                {
                    translations[entry.Key] = entry.Value;
                }
            }
        }

        return translations;
    }

    private WorkshopModIndex GetWorkshopIndex(
        IReadOnlyList<string> workshopRoots,
        string profileId,
        IReadOnlyList<string> enabledModIds)
    {
        var cacheKey = string.Join(
            "|",
            workshopRoots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        lock (_cacheLock)
        {
            var enabledSet = enabledModIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var hasNewEnabledMod = false;
            if (_enabledModIdsByProfile.TryGetValue(profileId, out var previousEnabledSet))
            {
                hasNewEnabledMod = enabledSet.Except(previousEnabledSet, StringComparer.OrdinalIgnoreCase).Any();
            }
            else if (_workshopIndexes.TryGetValue(cacheKey, out var existingIndex))
            {
                hasNewEnabledMod = enabledSet.Any(modId => !existingIndex.CandidatesByModId.ContainsKey(modId));
            }

            _enabledModIdsByProfile[profileId] = enabledSet;
            if (_workshopIndexes.TryGetValue(cacheKey, out var cachedIndex) && !hasNewEnabledMod)
            {
                return cachedIndex;
            }

            var candidates = workshopRoots
                .SelectMany(EnumerateWorkshopModInfoPaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(BuildIndexedDefinition)
                .Where(candidate => candidate is not null)
                .Cast<IndexedModDefinition>()
                .GroupBy(candidate => candidate.ModId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<IndexedModDefinition>)group.ToArray(),
                    StringComparer.OrdinalIgnoreCase);

            var refreshedIndex = new WorkshopModIndex(++_workshopIndexRevision, candidates);
            _workshopIndexes[cacheKey] = refreshedIndex;
            return refreshedIndex;
        }
    }

    private static IndexedModDefinition? BuildIndexedDefinition(string modInfoPath)
    {
        var metadata = ReadModInfo(modInfoPath);
        if (string.IsNullOrWhiteSpace(metadata.Id))
        {
            return null;
        }

        var modDirectory = Path.GetDirectoryName(modInfoPath)!;
        var optionsPath = Path.Combine(modDirectory, "media", "sandbox-options.txt");
        if (!File.Exists(optionsPath))
        {
            return null;
        }

        var modRoot = ResolveModRoot(modDirectory);
        return new IndexedModDefinition(
            metadata.Id,
            optionsPath,
            modRoot,
            metadata.Name ?? metadata.Id,
            Path.GetRelativePath(modRoot, modDirectory));
    }

    private static string BuildDiscoveryCacheKey(long revision, string steamBranch, IReadOnlyList<string> enabledModIds) =>
        $"{revision}|{steamBranch}|{string.Join('\u001f', enabledModIds)}";

    private static IEnumerable<string> EnumerateWorkshopModInfoPaths(string workshopRoot)
    {
        foreach (var workshopItemDirectory in Directory.EnumerateDirectories(workshopRoot))
        {
            var modsDirectory = Path.Combine(workshopItemDirectory, "mods");
            if (!Directory.Exists(modsDirectory))
            {
                continue;
            }

            foreach (var modDirectory in Directory.EnumerateDirectories(modsDirectory))
            {
                var rootInfoPath = Path.Combine(modDirectory, "mod.info");
                if (File.Exists(rootInfoPath))
                {
                    yield return rootInfoPath;
                }

                foreach (var variantDirectory in Directory.EnumerateDirectories(modDirectory))
                {
                    var variantInfoPath = Path.Combine(variantDirectory, "mod.info");
                    if (File.Exists(variantInfoPath))
                    {
                        yield return variantInfoPath;
                    }
                }
            }
        }
    }

    private static (string? Id, string? Name) ReadModInfo(string path)
    {
        string? id = null;
        string? name = null;
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("id=", StringComparison.OrdinalIgnoreCase))
            {
                id = line[3..].Trim();
            }
            else if (line.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
            {
                name = line[5..].Trim();
            }
        }

        return (id, name);
    }

    private static string ResolveModRoot(string modDirectory)
    {
        var current = new DirectoryInfo(modDirectory);
        while (current.Parent is not null && !string.Equals(current.Parent.Name, "mods", StringComparison.OrdinalIgnoreCase))
        {
            current = current.Parent;
        }

        return current.FullName;
    }

    private static IReadOnlyList<string> ResolveWorkshopRoots(string installDirectory)
    {
        var workshopRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(installDirectory))
        {
            var current = new DirectoryInfo(installDirectory);
            while (current is not null)
            {
                AddWorkshopRoot(
                    string.Equals(current.Name, "steamapps", StringComparison.OrdinalIgnoreCase)
                        ? current.Parent?.FullName
                        : current.FullName,
                    workshopRoots);
                current = current.Parent;
            }
        }

        foreach (var steamRoot in EnumerateSteamRoots())
        {
            AddWorkshopRoot(steamRoot, workshopRoots);
            var libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersPath))
            {
                continue;
            }

            foreach (var libraryRoot in ParseSteamLibraryRoots(File.ReadAllText(libraryFoldersPath)))
            {
                AddWorkshopRoot(libraryRoot, workshopRoots);
            }
        }

        return workshopRoots.ToArray();
    }

    internal static IReadOnlyList<string> ParseSteamLibraryRoots(string content) =>
        SteamLibraryPathRegex().Matches(content)
            .Select(match => Regex.Unescape(match.Groups["path"].Value))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        if (OperatingSystem.IsWindows())
        {
            string? registeredSteamPath = null;
            try
            {
                using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                registeredSteamPath = steamKey?.GetValue("SteamPath") as string;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A locked or restricted registry should not prevent profile-local discovery.
            }

            if (!string.IsNullOrWhiteSpace(registeredSteamPath))
            {
                yield return registeredSteamPath;
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "Steam");
        }
    }

    private static void AddWorkshopRoot(string? steamRoot, ISet<string> workshopRoots)
    {
        if (string.IsNullOrWhiteSpace(steamRoot))
        {
            return;
        }

        var workshopRoot = Path.Combine(steamRoot, "steamapps", "workshop", "content", "108600");
        if (Directory.Exists(workshopRoot))
        {
            workshopRoots.Add(Path.GetFullPath(workshopRoot));
        }
    }

    private static string? ResolveTranslation(IReadOnlyDictionary<string, string> translations, string key) =>
        translations.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string NormalizeDefault(string value, StructuredValueKind kind) =>
        kind == StructuredValueKind.Boolean && bool.TryParse(value, out var parsed)
            ? parsed.ToString().ToLowerInvariant()
            : value.Trim();

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static string FormatBound(decimal? value, string fallback) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? fallback;

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? Regex.Unescape(value[1..^1]) : value;

    private static string SanitizeId(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    internal static string Humanize(string value)
    {
        var tail = value.Split('.').LastOrDefault() ?? value;
        var spaced = Regex.Replace(tail.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced.ToLowerInvariant());
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentRegex();

    [GeneratedRegex(@"\boption\s+(?<name>[A-Za-z0-9_.-]+)\s*(?:=\s*)?\{(?<body>.*?)\}", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex OptionRegex();

    [GeneratedRegex("(?<key>_?[A-Za-z][A-Za-z0-9]*)\\s*=\\s*(?<value>\"(?:\\\\.|[^\"])*\"|[^,\\r\\n}]*)", RegexOptions.IgnoreCase)]
    private static partial Regex PropertyRegex();

    [GeneratedRegex("(?<key>Sandbox_[A-Za-z0-9_.]+)\\s*=\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex TranslationRegex();

    [GeneratedRegex(@"(?<version>\d+(?:\.\d+){0,2})")]
    private static partial Regex VersionRegex();

    [GeneratedRegex("\"path\"\\s*\"(?<path>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex SteamLibraryPathRegex();
}

public sealed record ModSandboxDiscoveryResult(
    IReadOnlyList<StructuredSectionDefinition> Sections,
    IReadOnlyList<string> Diagnostics,
    int EnabledModCount,
    int DiscoveredModCount = 0);

internal sealed record ParsedModOption(
    string Name,
    string Type,
    string DefaultValue,
    string Page,
    string Translation,
    string ValueTranslation,
    string TooltipTranslation,
    decimal? Minimum,
    decimal? Maximum,
    int NumberOfValues);

internal sealed record ModOptionsCandidate(
    string OptionsPath,
    string ModRoot,
    string DisplayName,
    string Variant,
    int Priority);

internal sealed record IndexedModDefinition(
    string ModId,
    string OptionsPath,
    string ModRoot,
    string DisplayName,
    string Variant);

internal sealed record WorkshopModIndex(
    long Revision,
    IReadOnlyDictionary<string, IReadOnlyList<IndexedModDefinition>> CandidatesByModId);
