using Microsoft.AspNetCore.Authorization;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Host.Security;

public sealed class CapabilityAuthorizationHandler(ICapabilityResolver capabilityResolver)
    : AuthorizationHandler<CapabilityRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CapabilityRequirement requirement)
    {
        if (capabilityResolver.HasCapability(context.User, requirement.Capability))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
