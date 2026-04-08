using Microsoft.AspNetCore.Authorization;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.Host.Security;

public sealed class CapabilityRequirement(Capability capability) : IAuthorizationRequirement
{
    public Capability Capability { get; } = capability;
}
