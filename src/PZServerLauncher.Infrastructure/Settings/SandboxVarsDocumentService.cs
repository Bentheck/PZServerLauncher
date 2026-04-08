using System.Text.RegularExpressions;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Infrastructure.Settings;

public sealed partial class SandboxVarsDocumentService : ISandboxVarsDocumentService
{
    private const string MissingRootMessage = "SandboxVars.lua should define a SandboxVars table.";
    private static readonly string[] EmptyStringArray = [];

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
                ? MissingRootMessage
                : string.Join(" ", parsed.Issues.Select(issue => issue.Message));
            throw new InvalidOperationException(message);
        }

        var newline = DetectLineEnding(text);
        var lines = text.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.None).ToList();
        var missingTopLevelEntries = new List<KeyValuePair<string, string?>>();

        foreach (var entry in values)
        {
            if (parsed.Entries.TryGetValue(entry.Key, out var lineEntry))
            {
                lines[lineEntry.LineIndex] = RewriteLine(lineEntry, entry.Value);
                continue;
            }

            if (entry.Key.Contains('.', StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Sandbox field '{entry.Key}' is missing from the file and cannot be inserted safely yet.");
            }

            missingTopLevelEntries.Add(entry);
        }

        if (missingTopLevelEntries.Count > 0)
        {
            var insertionIndex = parsed.RootCloseLineIndex >= 0 ? parsed.RootCloseLineIndex : lines.Count;
            var generatedLines = missingTopLevelEntries
                .Select(entry => $"    {entry.Key} = {FormatValue(entry.Value)},")
                .ToList();
            lines.InsertRange(insertionIndex, generatedLines);
        }

        return string.Join(newline, lines);
    }

    private static string BuildDefaultDocument(IReadOnlyDictionary<string, string?> values)
    {
        var lines = new List<string>
        {
            "SandboxVars = {",
            "    VERSION = 4,",
        };

        foreach (var entry in values)
        {
            if (entry.Key.Contains('.', StringComparison.Ordinal))
            {
                continue;
            }

            lines.Add($"    {entry.Key} = {FormatValue(entry.Value)},");
        }

        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string RewriteLine(SandboxLineEntry entry, string? updatedValue)
    {
        var comment = string.IsNullOrWhiteSpace(entry.Comment)
            ? string.Empty
            : $" {entry.Comment}";

        return $"{entry.Indent}{entry.Key} = {FormatValue(updatedValue)}{entry.TrailingComma}{comment}";
    }

    private static string FormatValue(string? value)
    {
        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue ? "true" : "false";
        }

        return string.IsNullOrWhiteSpace(value) ? "0" : value.Trim();
    }

    private static string DetectLineEnding(string text) =>
        text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static ParsedSandboxDocument ParseInternal(string text)
    {
        var issues = new List<StructuredConfigIssue>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ParsedSandboxDocument(true, issues, new Dictionary<string, string?>(StringComparer.Ordinal), new Dictionary<string, SandboxLineEntry>(StringComparer.Ordinal), -1);
        }

        var lines = text.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.None);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var entries = new Dictionary<string, SandboxLineEntry>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var rootFound = false;
        var rootClosed = false;
        var rootCloseLineIndex = -1;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (!rootFound)
            {
                if (RootStartRegex().IsMatch(line))
                {
                    rootFound = true;
                    continue;
                }

                continue;
            }

            if (CloseLineRegex().IsMatch(line))
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                    continue;
                }

                rootClosed = true;
                rootCloseLineIndex = index;
                continue;
            }

            var tableMatch = TableStartRegex().Match(line);
            if (tableMatch.Success)
            {
                stack.Push(tableMatch.Groups["key"].Value);
                continue;
            }

            var valueMatch = ValueLineRegex().Match(line);
            if (!valueMatch.Success)
            {
                issues.Add(new StructuredConfigIssue("Unsupported SandboxVars.lua syntax.", index + 1));
                continue;
            }

            var key = valueMatch.Groups["key"].Value;
            var pathSegments = stack.Reverse().Append(key);
            var keyPath = string.Join('.', pathSegments);
            var rawValue = valueMatch.Groups["value"].Value.Trim();
            values[keyPath] = rawValue;
            entries[keyPath] = new SandboxLineEntry(
                index,
                valueMatch.Groups["indent"].Value,
                key,
                valueMatch.Groups["comma"].Success ? valueMatch.Groups["comma"].Value : ",",
                valueMatch.Groups["comment"].Success ? valueMatch.Groups["comment"].Value.TrimEnd() : null);
        }

        if (!rootFound)
        {
            issues.Add(new StructuredConfigIssue(MissingRootMessage));
        }

        if (!rootClosed)
        {
            issues.Add(new StructuredConfigIssue("SandboxVars.lua has unbalanced braces."));
        }

        return new ParsedSandboxDocument(issues.Count == 0, issues, values, entries, rootCloseLineIndex);
    }

    [GeneratedRegex(@"^\s*SandboxVars\s*=\s*\{\s*(--.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex RootStartRegex();

    [GeneratedRegex(@"^\s*(?<key>[A-Za-z0-9_]+)\s*=\s*\{\s*(--.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TableStartRegex();

    [GeneratedRegex(@"^(?<indent>\s*)(?<key>[A-Za-z0-9_]+)\s*=\s*(?<value>.*?)(?<comma>,?)\s*(?<comment>--.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ValueLineRegex();

    [GeneratedRegex(@"^\s*\},?\s*(--.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex CloseLineRegex();

    private sealed record ParsedSandboxDocument(
        bool IsSupported,
        IReadOnlyList<StructuredConfigIssue> Issues,
        IReadOnlyDictionary<string, string?> Values,
        IReadOnlyDictionary<string, SandboxLineEntry> Entries,
        int RootCloseLineIndex);

    private sealed record SandboxLineEntry(
        int LineIndex,
        string Indent,
        string Key,
        string TrailingComma,
        string? Comment);
}
