namespace PZServerLauncher.Core.Settings;

public interface IIniDocumentService
{
    StructuredConfigDocument Parse(string text);

    string Format(StructuredConfigDocument document);
}
