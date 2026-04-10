using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.ViewModels;

public static class RawConfigEditorState
{
    public static void Apply(ProfileCardViewModel profile, RawConfigFileDto file)
    {
        profile.RawConfigContent = file.Content;
        profile.LoadedRawConfigSha256 = file.Sha256;
        profile.LoadedRawConfigKind = file.Kind;
        profile.IsRawConfigLoaded = true;
        profile.RawConfigDiagnostics = file.Diagnostics.Count == 0
            ? "No validation issues found."
            : string.Join(Environment.NewLine, file.Diagnostics);
        profile.RawConfigStatus = $"Loaded {Describe(file.Kind)}.";
    }

    public static string Describe(ConfigFileKind kind) =>
        ConfigFileOptionViewModel.All.First(option => option.Kind == kind).Label;
}
