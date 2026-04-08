namespace PZServerLauncher.App.ViewModels;

public sealed record ImportCandidateViewModel(
    string CandidateId,
    string DisplayName,
    string ServerName,
    string CacheDirectory,
    string InstallDirectory,
    string Branch,
    string Notes,
    bool IsAlreadyImported)
{
    public string ImportActionLabel => IsAlreadyImported ? "Imported" : "Import";

    public bool CanImport => !IsAlreadyImported;
}
