using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Tests.Profiles;

public sealed class ProjectZomboidBranchSupportTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    public void FromPersistedValue_NormalizesAnyLegacyValueToB42(int persistedValue)
    {
        var branch = ProjectZomboidBranchSupport.FromPersistedValue(persistedValue);

        Assert.Equal(ProjectZomboidBranch.Unstable42, branch);
    }
}
