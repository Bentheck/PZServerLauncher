using System.Globalization;
using System.Text.RegularExpressions;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Infrastructure.Settings;

public sealed partial class SandboxPresetDocumentService : ISandboxPresetDocumentService
{
    private const string MissingRootMessage = "Preset .lua files should start with 'return {'.";

    public IReadOnlyDictionary<string, string?> ReadValues(string text)
    {
        var parsed = ParseInternal(text);
        if (!parsed.IsSupported)
        {
            throw new InvalidOperationException(BuildErrorMessage(parsed.Issues));
        }

        return parsed.Values;
    }

    public string WriteValues(IReadOnlyDictionary<string, string?> values)
    {
        var root = new SandboxPresetNode();
        foreach (var entry in values)
        {
            AddValue(root, entry.Key, entry.Value);
        }

        var lines = new List<string> { "return {" };
        WriteNode(lines, root, "    ");
        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddValue(SandboxPresetNode root, string keyPath, string? value)
    {
        var segments = keyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return;
        }

        var current = root;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            var segment = segments[index];
            if (!current.Children.TryGetValue(segment, out var child))
            {
                child = new SandboxPresetNode();
                current.Children[segment] = child;
            }

            current = child;
        }

        current.Values[segments[^1]] = value;
    }

    private static void WriteNode(List<string> lines, SandboxPresetNode node, string indent)
    {
        var entries = new List<SandboxPresetEntry>();
        entries.AddRange(node.Values.Select(pair => SandboxPresetEntry.CreateScalar(pair.Key, pair.Value)));
        entries.AddRange(node.Children.Select(pair => SandboxPresetEntry.CreateTable(pair.Key, pair.Value)));

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var hasTrailingComma = index < entries.Count - 1;
            if (entry.TableNode is null)
            {
                lines.Add($"{indent}{entry.Key} = {FormatValue(entry.Value)}{(hasTrailingComma ? "," : string.Empty)}");
                continue;
            }

            lines.Add($"{indent}{entry.Key} = {{");
            WriteNode(lines, entry.TableNode, indent + "    ");
            lines.Add($"{indent}}}{(hasTrailingComma ? "," : string.Empty)}");
        }
    }

    private static string FormatValue(string? value)
    {
        var trimmed = value?.Trim();
        if (bool.TryParse(trimmed, out var boolValue))
        {
            return boolValue ? "true" : "false";
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "0";
        }

        if (LooksLikeNumericLiteral(trimmed) || LooksLikeQuotedString(trimmed))
        {
            return trimmed;
        }

        return $"\"{EscapeLuaString(trimmed)}\"";
    }

    private static bool LooksLikeQuotedString(string value) =>
        value.Length >= 2 &&
        value[0] == '"' &&
        value[^1] == '"';

    private static bool LooksLikeNumericLiteral(string value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static string EscapeLuaString(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string? NormalizeReadValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        if (!LooksLikeQuotedString(trimmed))
        {
            return trimmed;
        }

        return UnescapeLuaString(trimmed[1..^1]);
    }

    private static string UnescapeLuaString(string value) =>
        value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);

    private static string BuildErrorMessage(IReadOnlyList<string> issues) =>
        issues.Count == 0 ? MissingRootMessage : string.Join(" ", issues);

    private static ParsedSandboxPresetDocument ParseInternal(string text)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            issues.Add("Preset .lua files cannot be empty.");
            return new ParsedSandboxPresetDocument(false, issues, new Dictionary<string, string?>(StringComparer.Ordinal));
        }

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        var lines = text.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.None);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var rootFound = false;
        var rootClosed = false;

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
                continue;
            }

            var tableMatch = TableStartRegex().Match(line);
            if (tableMatch.Success)
            {
                var key = tableMatch.Groups["key"].Value;
                stack.Push(key);
                continue;
            }

            var valueMatch = ValueLineRegex().Match(line);
            if (!valueMatch.Success)
            {
                issues.Add($"Unsupported preset syntax on line {index + 1}.");
                continue;
            }

            var valueKey = valueMatch.Groups["key"].Value;
            var pathSegments = stack.Reverse().Append(valueKey);
            var keyPath = string.Join('.', pathSegments);
            values[keyPath] = NormalizeReadValue(valueMatch.Groups["value"].Value.Trim());
        }

        if (!rootFound)
        {
            issues.Add(MissingRootMessage);
        }

        if (!rootClosed)
        {
            issues.Add("Preset .lua has unbalanced braces.");
        }

        return new ParsedSandboxPresetDocument(issues.Count == 0, issues, values);
    }

    [GeneratedRegex(@"^\s*return\s*\{\s*(--.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex RootStartRegex();

    [GeneratedRegex(@"^(?<indent>\s*)(?<key>[A-Za-z0-9_]+)\s*=\s*\{\s*(--.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TableStartRegex();

    [GeneratedRegex(@"^(?<indent>\s*)(?<key>[A-Za-z0-9_]+)\s*=\s*(?<value>.*?)(?<comma>,?)\s*(?<comment>--.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ValueLineRegex();

    [GeneratedRegex(@"^\s*\},?\s*(--.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex CloseLineRegex();

    private sealed record ParsedSandboxPresetDocument(
        bool IsSupported,
        IReadOnlyList<string> Issues,
        IReadOnlyDictionary<string, string?> Values);

    private sealed class SandboxPresetNode
    {
        public Dictionary<string, string?> Values { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, SandboxPresetNode> Children { get; } = new(StringComparer.Ordinal);
    }

    private sealed record SandboxPresetEntry(
        string Key,
        string? Value,
        SandboxPresetNode? TableNode)
    {
        public static SandboxPresetEntry CreateScalar(string key, string? value) => new(key, value, null);

        public static SandboxPresetEntry CreateTable(string key, SandboxPresetNode node) => new(key, null, node);
    }
}
