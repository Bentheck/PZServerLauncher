namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProjectZomboidOperatorChecklistItem(
    string StatusLabel,
    string Message,
    bool IsBlocking,
    bool IsFollowUp);
