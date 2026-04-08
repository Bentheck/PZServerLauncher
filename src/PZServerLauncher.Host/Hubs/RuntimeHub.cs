using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PZServerLauncher.Host.Hubs;

[Authorize(Policy = "DesktopOrViewer")]
public sealed class RuntimeHub : Hub
{
}
