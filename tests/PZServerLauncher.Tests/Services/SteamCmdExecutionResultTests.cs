using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class SteamCmdExecutionResultTests
{
    [Fact]
    public void MissingConfigurationFailure_IsDetected_ForExitCodeSeven()
    {
        var result = new SteamCmdExecutionResult(7, [
            "[2026-04-09 23:05:16] app_update 380870 -beta unstable validate",
            "[2026-04-09 23:05:16] ERROR! Failed to install app '380870' (Missing configuration)",
        ]);

        Assert.True(result.HasMissingConfigurationFailure);
    }

    [Fact]
    public void LastRelevantLine_PrefersLastMeaningfulSteamCmdLine()
    {
        var result = new SteamCmdExecutionResult(8, [
            "Loading Steam API...",
            "OK",
            "[2026-04-09 23:05:16] ERROR! Failed to install app '380870' (Missing configuration)",
            "OK",
        ]);

        Assert.Equal("[2026-04-09 23:05:16] ERROR! Failed to install app '380870' (Missing configuration)", result.LastRelevantLine);
    }
}
