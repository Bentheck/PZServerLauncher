using System.Text.RegularExpressions;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Infrastructure.Settings;

public sealed partial class IniDocumentService : IIniDocumentService
{
    public StructuredConfigDocument Parse(string text)
    {
        var parsed = ParseInternal(text);
        return new StructuredConfigDocument(text, parsed.IsSupported, parsed.Issues);
    }

    public string Format(StructuredConfigDocument document) => document.SourceText;

    public IReadOnlyDictionary<string, string?> ReadValues(string text, IEnumerable<string> keyPaths)
    {
        var parsed = ParseInternal(text);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var keyPath in keyPaths)
        {
            if (parsed.Values.TryGetValue(keyPath, out var value))
            {
                values[keyPath] = value;
            }
        }

        return values;
    }

    public string ApplyValues(string text, IReadOnlyDictionary<string, string?> values)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BuildDefaultDocument(values);
        }

        var parsed = ParseInternal(text);
        if (!parsed.IsSupported)
        {
            var message = parsed.Issues.Count == 0
                ? "The Project Zomboid server INI contains unsupported syntax."
                : string.Join(" ", parsed.Issues.Select(issue => issue.Message));
            throw new InvalidOperationException(message);
        }

        var newline = DetectLineEnding(text);
        var lines = text.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.None).ToList();
        var missingEntries = new List<KeyValuePair<string, string?>>();

        foreach (var entry in values)
        {
            if (parsed.Entries.TryGetValue(entry.Key, out var lineEntry))
            {
                lines[lineEntry.LineIndex] = RewriteLine(lineEntry, entry.Key, entry.Value);
                continue;
            }

            missingEntries.Add(entry);
        }

        if (missingEntries.Count > 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            foreach (var entry in missingEntries)
            {
                lines.Add($"{entry.Key}={FormatValue(entry.Value)}");
            }
        }

        return string.Join(newline, lines);
    }

    private static string BuildDefaultDocument(IReadOnlyDictionary<string, string?> values)
    {
        var lines = values
            .Select(entry => $"{entry.Key}={FormatValue(entry.Value)}")
            .ToList();

        return string.Join(Environment.NewLine, lines);
    }

    private static string RewriteLine(IniLineEntry entry, string key, string? updatedValue)
    {
        var comment = string.IsNullOrWhiteSpace(entry.Comment)
            ? string.Empty
            : $" {entry.Comment}";
        return $"{entry.Indent}{key}{entry.Separator}{FormatValue(updatedValue)}{comment}";
    }

    private static string FormatValue(string? value) =>
        value?.Trim() ?? string.Empty;

    private static string DetectLineEnding(string text) =>
        text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static ParsedIniDocument ParseInternal(string text)
    {
        var issues = new List<StructuredConfigIssue>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ParsedIniDocument(
                true,
                issues,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, IniLineEntry>(StringComparer.OrdinalIgnoreCase));
        }

        var lines = text.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.None);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var entries = new Dictionary<string, IniLineEntry>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (SectionRegex().IsMatch(line))
            {
                continue;
            }

            var valueMatch = ValueLineRegex().Match(line);
            if (!valueMatch.Success)
            {
                issues.Add(new StructuredConfigIssue("Expected a key=value entry.", index + 1));
                continue;
            }

            var key = valueMatch.Groups["key"].Value.Trim();
            var value = valueMatch.Groups["value"].Value.Trim();
            values[key] = value;
            entries[key] = new IniLineEntry(
                index,
                valueMatch.Groups["indent"].Value,
                valueMatch.Groups["separator"].Value,
                valueMatch.Groups["comment"].Success ? valueMatch.Groups["comment"].Value.TrimEnd() : null);
        }

        return new ParsedIniDocument(issues.Count == 0, issues, values, entries);
    }

    [GeneratedRegex(@"^\s*\[[^\]]+\]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"^(?<indent>\s*)(?<key>[A-Za-z0-9_.-]+)(?<separator>\s*=\s*)(?<value>.*?)(?<comment>\s+[;#].*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ValueLineRegex();

    private sealed record ParsedIniDocument(
        bool IsSupported,
        IReadOnlyList<StructuredConfigIssue> Issues,
        IReadOnlyDictionary<string, string?> Values,
        IReadOnlyDictionary<string, IniLineEntry> Entries);

    private sealed record IniLineEntry(
        int LineIndex,
        string Indent,
        string Separator,
        string? Comment);
}
