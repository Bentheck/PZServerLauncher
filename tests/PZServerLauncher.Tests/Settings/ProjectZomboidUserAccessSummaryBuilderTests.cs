using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidUserAccessSummaryBuilderTests
{
    [Fact]
    public void Build_SummarizesPrivilegedCoverageAndCreateRoleGuardrails()
    {
        UserAccountDto[] users =
        [
            new UserAccountDto("owner-1", "owner", [UserRole.Owner], true),
            new UserAccountDto("admin-1", "admin", [UserRole.Admin], false),
            new UserAccountDto("viewer-1", "viewer", [UserRole.Viewer], false),
        ];

        var summary = ProjectZomboidUserAccessSummaryBuilder.Build(
            ownerBootstrapConfigured: true,
            users,
            nameof(UserRole.Admin));

        Assert.Contains("3 managed account", summary.RosterHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("privileged", summary.SecurityHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Admin can manage configuration", summary.CreateRoleHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(summary.Checklist, item => item.IsFollowUp && item.Message.Contains("privileged", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_UsesBootstrapGuardrailsBeforeUsersExist()
    {
        var summary = ProjectZomboidUserAccessSummaryBuilder.Build(
            ownerBootstrapConfigured: false,
            Array.Empty<UserAccountDto>(),
            nameof(UserRole.Viewer));

        Assert.Contains("bootstrap", summary.RosterHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Single(summary.Checklist);
        Assert.True(summary.Checklist[0].IsBlocking);
    }
}
