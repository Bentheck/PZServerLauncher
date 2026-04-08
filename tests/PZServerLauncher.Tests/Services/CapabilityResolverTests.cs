using System.Security.Claims;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class CapabilityResolverTests
{
    private readonly CapabilityResolver _resolver = new();

    [Fact]
    public void ResolveCapabilities_LocalSystemGetsFullCapabilitySet()
    {
        var principal = BuildPrincipal(UserRole.LocalSystem, authSource: "loopback");

        var resolved = _resolver.ResolveCapabilities(principal);

        Assert.Equal(Enum.GetValues<Capability>().Length, resolved.AllowedCapabilities.Count);
        Assert.Contains(Capability.ManageLocalHost, resolved.AllowedCapabilities);
        Assert.Contains(Capability.ManageUsers, resolved.AllowedCapabilities);
    }

    [Fact]
    public void ResolveCapabilities_ViewerRemainsReadOnly()
    {
        var principal = BuildPrincipal(UserRole.Viewer);

        var resolved = _resolver.ResolveCapabilities(principal);

        Assert.Contains(Capability.ViewDashboard, resolved.AllowedCapabilities);
        Assert.Contains(Capability.ViewSettings, resolved.AllowedCapabilities);
        Assert.DoesNotContain(Capability.EditSettings, resolved.AllowedCapabilities);
        Assert.DoesNotContain(Capability.ManageUsers, resolved.AllowedCapabilities);
    }

    [Fact]
    public void DescribeActor_UsesLoopbackClaimToMarkDesktopSurface()
    {
        var principal = BuildPrincipal(UserRole.Operator, authSource: "loopback", name: "Local desktop");

        var actor = _resolver.DescribeActor(principal);

        Assert.Equal(WorkspaceSurfaceKind.Desktop, actor.Surface);
        Assert.Contains(UserRole.Operator, actor.Roles);
        Assert.Equal("Local desktop", actor.DisplayName);
    }

    private static ClaimsPrincipal BuildPrincipal(UserRole role, string? authSource = null, string? name = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Name, name ?? role.ToString()),
            new(ClaimTypes.Role, role.ToString()),
        };

        if (!string.IsNullOrWhiteSpace(authSource))
        {
            claims.Add(new Claim("auth_source", authSource));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
