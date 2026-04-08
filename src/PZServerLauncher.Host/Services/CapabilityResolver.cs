using System.Security.Claims;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Host.Services;

public sealed class CapabilityResolver : ICapabilityResolver
{
    private static readonly IReadOnlyDictionary<UserRole, Capability[]> RoleCapabilities =
        new Dictionary<UserRole, Capability[]>
        {
            [UserRole.Viewer] =
            [
                Capability.ViewDashboard,
                Capability.ViewProfiles,
                Capability.ViewSettings,
                Capability.ViewLogs,
                Capability.ViewBackups,
            ],
            [UserRole.Operator] =
            [
                Capability.ViewDashboard,
                Capability.ViewProfiles,
                Capability.ViewSettings,
                Capability.ViewLogs,
                Capability.ViewBackups,
                Capability.ViewHost,
                Capability.ManageInstallations,
                Capability.ManageRuntime,
                Capability.ManageBackups,
            ],
            [UserRole.Admin] =
            [
                Capability.ViewDashboard,
                Capability.ViewProfiles,
                Capability.ManageProfiles,
                Capability.ViewSettings,
                Capability.EditSettings,
                Capability.ViewLogs,
                Capability.ViewBackups,
                Capability.ManageBackups,
                Capability.ViewHost,
                Capability.ManageHost,
                Capability.ViewRemoteAccess,
                Capability.ManageRemoteAccess,
                Capability.ViewAdvancedFiles,
                Capability.EditAdvancedFiles,
                Capability.ManageInstallations,
                Capability.ManageRuntime,
            ],
            [UserRole.Owner] =
            [
                Capability.ViewDashboard,
                Capability.ViewProfiles,
                Capability.ManageProfiles,
                Capability.ViewSettings,
                Capability.EditSettings,
                Capability.ViewLogs,
                Capability.ViewBackups,
                Capability.ManageBackups,
                Capability.ViewHost,
                Capability.ManageHost,
                Capability.ViewRemoteAccess,
                Capability.ManageRemoteAccess,
                Capability.ViewAdvancedFiles,
                Capability.EditAdvancedFiles,
                Capability.ManageInstallations,
                Capability.ManageRuntime,
                Capability.ViewUsers,
                Capability.ManageUsers,
            ],
            [UserRole.LocalSystem] = Enum.GetValues<Capability>(),
        };

    public bool HasCapability(ClaimsPrincipal user, Capability capability) =>
        ResolveCapabilitySet(user).Contains(capability);

    public ResolvedCapabilitiesDto ResolveCapabilities(ClaimsPrincipal user) =>
        new(ResolveCapabilitySet(user).OrderBy(value => value.ToString(), StringComparer.Ordinal).ToArray());

    public WorkspaceActorDto DescribeActor(ClaimsPrincipal user)
    {
        var roles = ResolveRoles(user);
        var displayName = user.FindFirstValue(ClaimTypes.Name) ??
            user.FindFirstValue(ClaimTypes.Email) ??
            "Unknown user";
        var email = user.FindFirstValue(ClaimTypes.Email);
        var surface = string.Equals(
            user.FindFirstValue("auth_source"),
            "loopback",
            StringComparison.OrdinalIgnoreCase)
            ? WorkspaceSurfaceKind.Desktop
            : WorkspaceSurfaceKind.Web;

        return new WorkspaceActorDto(displayName, email, surface, roles);
    }

    private static IReadOnlySet<Capability> ResolveCapabilitySet(ClaimsPrincipal user)
    {
        var values = new HashSet<Capability>();
        foreach (var role in ResolveRoles(user))
        {
            if (!RoleCapabilities.TryGetValue(role, out var capabilities))
            {
                continue;
            }

            values.UnionWith(capabilities);
        }

        return values;
    }

    private static IReadOnlyList<UserRole> ResolveRoles(ClaimsPrincipal user)
    {
        var roles = new List<UserRole>();
        foreach (var claim in user.FindAll(ClaimTypes.Role))
        {
            if (Enum.TryParse<UserRole>(claim.Value, ignoreCase: false, out var role) &&
                !roles.Contains(role))
            {
                roles.Add(role);
            }
        }

        return roles;
    }
}
