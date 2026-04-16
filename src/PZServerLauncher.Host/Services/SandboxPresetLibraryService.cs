using System.Text;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Settings;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Services;

public sealed class SandboxPresetLibraryService(
    AppPaths appPaths,
    ISettingsCatalogResolver catalogResolver,
    ISandboxPresetDocumentService sandboxPresetDocumentService)
{
    private static readonly string[] BuiltInPresetOrder =
    [
        "Apocalypse",
        "Outbreak",
        "Rising",
        "Extinction",
        "SixMonthsLater",
    ];

    public IReadOnlyList<SandboxPresetDto> List(ServerProfile profile)
    {
        var fields = ResolveSandboxFields(profile.Branch);
        var presets = new List<SandboxPresetDto>();

        foreach (var path in EnumerateBuiltInPresetPaths(profile.Branch))
        {
            if (TryLoadPreset(path, isBuiltIn: true, fields, out var preset))
            {
                presets.Add(preset);
            }
        }

        foreach (var path in EnumerateUserPresetPaths(profile.Branch))
        {
            if (TryLoadPreset(path, isBuiltIn: false, fields, out var preset))
            {
                presets.Add(preset);
            }
        }

        return presets;
    }

    public SandboxPresetDto Save(
        ServerProfile profile,
        string name,
        IReadOnlyDictionary<string, string?> values)
    {
        var fileStem = SanitizeFileStem(name);
        var builtInStemConflict = EnumerateBuiltInPresetPaths(profile.Branch)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Any(candidate => string.Equals(candidate, fileStem, StringComparison.OrdinalIgnoreCase));
        if (builtInStemConflict)
        {
            throw new InvalidOperationException($"'{fileStem}' is already reserved by a shipped sandbox preset.");
        }

        var fields = ResolveSandboxFields(profile.Branch);
        var rawValues = LoadBaseRawValues(profile.Branch);
        foreach (var field in fields)
        {
            var editorValue = values.TryGetValue(field.FieldId, out var value) ? value : field.DefaultValue;
            rawValues[field.Target.KeyPath] = SandboxValueNormalizer.ToPersistedValue(field, editorValue);
        }

        if (!rawValues.ContainsKey("Version"))
        {
            rawValues["Version"] = "6";
        }

        var directory = EnsureUserPresetDirectory(profile.Branch);
        var path = Path.Combine(directory, $"{fileStem}.lua");
        File.WriteAllText(path, sandboxPresetDocumentService.WriteValues(rawValues), Encoding.UTF8);

        if (!TryLoadPreset(path, isBuiltIn: false, fields, out var preset))
        {
            throw new InvalidOperationException($"Sandbox preset '{fileStem}' could not be loaded after it was saved.");
        }

        return preset;
    }

    public bool Delete(ServerProfile profile, string presetId)
    {
        if (!TryParsePresetId(presetId, out var isBuiltIn, out var fileStem) || isBuiltIn)
        {
            throw new InvalidOperationException("Only custom sandbox presets can be deleted.");
        }

        var path = Path.Combine(EnsureUserPresetDirectory(profile.Branch), $"{fileStem}.lua");
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private IReadOnlyList<StructuredFieldDefinition> ResolveSandboxFields(ProjectZomboidBranch branch) =>
        catalogResolver.Resolve(branch)
            .Pages
            .Single(page => string.Equals(page.PageId, $"{ProjectZomboidBranchSupport.CurrentFieldPrefix}.sandbox", StringComparison.Ordinal))
            .Sections
            .SelectMany(section => section.Fields)
            .ToArray();

    private IEnumerable<string> EnumerateBuiltInPresetPaths(ProjectZomboidBranch branch)
    {
        var directory = ResolveBuiltInPresetDirectory(branch);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        var order = BuiltInPresetOrder
            .Select((name, index) => new { name, index })
            .ToDictionary(entry => entry.name, entry => entry.index, StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(directory, "*.lua", SearchOption.TopDirectoryOnly)
                     .OrderBy(path =>
                     {
                         var fileStem = Path.GetFileNameWithoutExtension(path);
                         return order.TryGetValue(fileStem, out var rank) ? rank : int.MaxValue;
                     })
                     .ThenBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase))
        {
            yield return path;
        }
    }

    private IEnumerable<string> EnumerateUserPresetPaths(ProjectZomboidBranch branch)
    {
        var directory = EnsureUserPresetDirectory(branch);
        return Directory.EnumerateFiles(directory, "*.lua", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string?> LoadBaseRawValues(ProjectZomboidBranch branch)
    {
        var apocalypsePath = EnumerateBuiltInPresetPaths(branch)
            .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), "Apocalypse", StringComparison.OrdinalIgnoreCase));
        var sourcePath = apocalypsePath ?? EnumerateBuiltInPresetPaths(branch).FirstOrDefault();
        if (sourcePath is null)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        return new Dictionary<string, string?>(
            sandboxPresetDocumentService.ReadValues(File.ReadAllText(sourcePath)),
            StringComparer.Ordinal);
    }

    private bool TryLoadPreset(
        string path,
        bool isBuiltIn,
        IReadOnlyList<StructuredFieldDefinition> fields,
        out SandboxPresetDto preset)
    {
        preset = default!;

        try
        {
            var rawValues = sandboxPresetDocumentService.ReadValues(File.ReadAllText(path));
            var editorValues = new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (var field in fields)
            {
                if (!rawValues.TryGetValue(field.Target.KeyPath, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
                {
                    continue;
                }

                editorValues[field.FieldId] = SandboxValueNormalizer.ToEditorValue(field, rawValue);
            }

            var fileStem = Path.GetFileNameWithoutExtension(path);
            preset = new SandboxPresetDto(
                BuildPresetId(isBuiltIn, fileStem),
                isBuiltIn ? HumanizeBuiltInLabel(fileStem) : fileStem,
                isBuiltIn,
                editorValues);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string ResolveBuiltInPresetDirectory(ProjectZomboidBranch branch)
    {
        var branchFolder = GetBranchFolder(branch);
        var installedDirectory = Path.Combine(appPaths.RootDirectory, "sandbox-presets", branchFolder);
        if (Directory.Exists(installedDirectory))
        {
            return installedDirectory;
        }

        return Path.Combine(appPaths.RootDirectory, "src", "PZServerLauncher.Runtime", "Assets", "ProjectZomboid", "SandboxPresets", branchFolder);
    }

    private string EnsureUserPresetDirectory(ProjectZomboidBranch branch)
    {
        var directory = Path.Combine(appPaths.DataDirectory, "sandbox-presets", GetBranchFolder(branch), "custom");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string GetBranchFolder(ProjectZomboidBranch branch) =>
        ProjectZomboidBranchSupport.CurrentFieldPrefix;

    private static string BuildPresetId(bool isBuiltIn, string fileStem) =>
        $"{(isBuiltIn ? "builtin" : "user")}:{fileStem}";

    private static bool TryParsePresetId(string presetId, out bool isBuiltIn, out string fileStem)
    {
        isBuiltIn = false;
        fileStem = string.Empty;

        if (string.IsNullOrWhiteSpace(presetId))
        {
            return false;
        }

        var separatorIndex = presetId.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == presetId.Length - 1)
        {
            return false;
        }

        isBuiltIn = string.Equals(presetId[..separatorIndex], "builtin", StringComparison.OrdinalIgnoreCase);
        fileStem = presetId[(separatorIndex + 1)..];
        return true;
    }

    private static string HumanizeBuiltInLabel(string fileStem)
    {
        var builder = new StringBuilder(fileStem.Length + 8);
        for (var index = 0; index < fileStem.Length; index++)
        {
            var current = fileStem[index];
            if (index > 0 &&
                char.IsUpper(current) &&
                (char.IsLower(fileStem[index - 1]) || char.IsDigit(fileStem[index - 1])))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static string SanitizeFileStem(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Preset name is required.");
        }

        var invalidCharacters = Path.GetInvalidFileNameChars()
            .Append(':')
            .ToHashSet();
        var normalized = new string(trimmed
            .Select(character => invalidCharacters.Contains(character) ? ' ' : character)
            .ToArray());
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim().TrimEnd('.');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Preset name must contain at least one usable letter or number.");
        }

        return normalized;
    }
}
