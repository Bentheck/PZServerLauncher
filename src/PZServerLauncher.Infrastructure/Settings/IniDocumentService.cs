using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Infrastructure.Settings;

public sealed class IniDocumentService : IIniDocumentService
{
    public StructuredConfigDocument Parse(string text)
    {
        var issues = new List<StructuredConfigIssue>();
        var lines = text.ReplaceLineEndings("\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';') || line.StartsWith('#') || line.StartsWith('['))
            {
                continue;
            }

            if (!line.Contains('='))
            {
                issues.Add(new StructuredConfigIssue("Expected a key=value entry.", index + 1));
            }
        }

        return new StructuredConfigDocument(text, issues.Count == 0, issues);
    }

    public string Format(StructuredConfigDocument document) => document.SourceText;
}
