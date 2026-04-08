using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Infrastructure.Settings;

public sealed class SandboxVarsDocumentService : ISandboxVarsDocumentService
{
    public StructuredConfigDocument Parse(string text)
    {
        var issues = new List<StructuredConfigIssue>();

        if (!text.Contains("SandboxVars", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new StructuredConfigIssue("SandboxVars.lua should define a SandboxVars table."));
        }

        var braceBalance = 0;
        var lineNumber = 1;

        foreach (var character in text)
        {
            switch (character)
            {
                case '{':
                    braceBalance++;
                    break;
                case '}':
                    braceBalance--;
                    if (braceBalance < 0)
                    {
                        issues.Add(new StructuredConfigIssue("Unexpected closing brace.", lineNumber));
                        braceBalance = 0;
                    }

                    break;
                case '\n':
                    lineNumber++;
                    break;
            }
        }

        if (braceBalance != 0)
        {
            issues.Add(new StructuredConfigIssue("SandboxVars.lua has unbalanced braces."));
        }

        return new StructuredConfigDocument(text, issues.Count == 0, issues);
    }

    public string Format(StructuredConfigDocument document) => document.SourceText;
}
