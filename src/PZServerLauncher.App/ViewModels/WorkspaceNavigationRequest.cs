namespace PZServerLauncher.App.ViewModels;

public sealed record WorkspaceNavigationRequest(
    string GlobalPageId,
    string? ProfileId = null,
    string? ProfilePageId = null);
