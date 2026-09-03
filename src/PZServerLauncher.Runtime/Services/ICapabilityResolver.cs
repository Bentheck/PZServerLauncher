using System.Security.Claims;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.Runtime.Services;

public interface ICapabilityResolver
{
    bool HasCapability(ClaimsPrincipal user, Capability capability);

    ResolvedCapabilitiesDto ResolveCapabilities(ClaimsPrincipal user);

    WorkspaceActorDto DescribeActor(ClaimsPrincipal user);
}
