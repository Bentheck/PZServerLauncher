namespace PZServerLauncher.Core.Settings;

public interface IIniDocumentService
{
    StructuredConfigDocument Parse(string text);

    string Format(StructuredConfigDocument document);

    IReadOnlyDictionary<string, string?> ReadValues(string text, IEnumerable<string> keyPaths);

    string ApplyValues(string text, IReadOnlyDictionary<string, string?> values);
}
