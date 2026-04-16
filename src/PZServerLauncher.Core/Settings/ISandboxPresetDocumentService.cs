namespace PZServerLauncher.Core.Settings;

public interface ISandboxPresetDocumentService
{
    IReadOnlyDictionary<string, string?> ReadValues(string text);

    string WriteValues(IReadOnlyDictionary<string, string?> values);
}
