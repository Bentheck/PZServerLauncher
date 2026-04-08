using System.Security.Claims;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.Host.Services;

public sealed class WorkspaceBootstrapService(ICapabilityResolver capabilityResolver)
{
    public WorkspaceBootstrapDto Build(ClaimsPrincipal user)
    {
        var capabilities = capabilityResolver.ResolveCapabilities(user);
        var globalPages = BuildGlobalPages(capabilities);
        var profilePages = BuildProfilePages(capabilities);
        return new WorkspaceBootstrapDto(
            capabilityResolver.DescribeActor(user),
            capabilities,
            globalPages,
            profilePages);
    }

    private static IReadOnlyList<WorkspacePageDto> BuildGlobalPages(ResolvedCapabilitiesDto capabilities) =>
        [
            BuildPage(WorkspacePageIds.Dashboard, "Dashboard", "/dashboard", WorkspacePageScope.Global, false, capabilities, Capability.ViewDashboard),
            BuildPage(WorkspacePageIds.Profiles, "Profiles", "/profiles", WorkspacePageScope.Global, false, capabilities, Capability.ViewProfiles),
            BuildPage(WorkspacePageIds.Host, "Host", "/host", WorkspacePageScope.Global, false, capabilities, Capability.ViewHost),
            BuildPage(WorkspacePageIds.RemoteAccess, "Remote Access", "/remote-access", WorkspacePageScope.Global, false, capabilities, Capability.ViewRemoteAccess),
            BuildPage(WorkspacePageIds.Users, "Users", "/users", WorkspacePageScope.Global, false, capabilities, Capability.ViewUsers),
        ];

    private static IReadOnlyList<WorkspacePageDto> BuildProfilePages(ResolvedCapabilitiesDto capabilities) =>
        [
            BuildPage(ProfileWorkspacePageIds.Overview, "Overview", "/profiles/{profileId}/overview", WorkspacePageScope.Profile, true, capabilities, Capability.ViewProfiles),
            BuildPage(ProfileWorkspacePageIds.InstallAndUpdate, "Install & Update", "/profiles/{profileId}/install-update", WorkspacePageScope.Profile, true, capabilities, Capability.ManageInstallations),
            BuildPage(ProfileWorkspacePageIds.General, "General", "/profiles/{profileId}/general", WorkspacePageScope.Profile, true, capabilities, Capability.ViewSettings),
            BuildPage(ProfileWorkspacePageIds.Sandbox, "Sandbox", "/profiles/{profileId}/sandbox", WorkspacePageScope.Profile, true, capabilities, Capability.ViewSettings),
            BuildPage(ProfileWorkspacePageIds.ModsAndMaps, "Mods & Maps", "/profiles/{profileId}/mods-maps", WorkspacePageScope.Profile, true, capabilities, Capability.ViewSettings),
            BuildPage(ProfileWorkspacePageIds.NetworkAndAdmin, "Network & Admin", "/profiles/{profileId}/network-admin", WorkspacePageScope.Profile, true, capabilities, Capability.ViewSettings),
            BuildPage(ProfileWorkspacePageIds.Backups, "Backups", "/profiles/{profileId}/backups", WorkspacePageScope.Profile, true, capabilities, Capability.ViewBackups),
            BuildPage(ProfileWorkspacePageIds.Logs, "Logs", "/profiles/{profileId}/logs", WorkspacePageScope.Profile, true, capabilities, Capability.ViewLogs),
            BuildPage(ProfileWorkspacePageIds.AdvancedFiles, "Advanced Files", "/profiles/{profileId}/advanced-files", WorkspacePageScope.Profile, true, capabilities, Capability.ViewAdvancedFiles),
            BuildPage(ProfileWorkspacePageIds.Classic, "Classic", "/profiles/{profileId}/classic", WorkspacePageScope.Profile, true, capabilities, Capability.ViewProfiles),
        ];

    private static WorkspacePageDto BuildPage(
        string id,
        string title,
        string route,
        WorkspacePageScope scope,
        bool requiresProfileSelection,
        ResolvedCapabilitiesDto capabilities,
        params Capability[] requiredCapabilities)
        => new(
            id,
            title,
            route,
            scope,
            requiresProfileSelection,
            requiredCapabilities,
            requiredCapabilities.All(capabilities.AllowedCapabilities.Contains));
}
