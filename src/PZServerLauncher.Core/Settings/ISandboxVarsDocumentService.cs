namespace PZServerLauncher.Core.Settings;

public interface ISandboxVarsDocumentService
{
    StructuredConfigDocument Parse(string text);

    string Format(StructuredConfigDocument document);
}
