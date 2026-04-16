using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Runtime;

public sealed partial class LauncherRuntime
{
    private async Task<HostInfoDto> BuildHostInfoAsync(CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken);

        return await ExecuteScopedAsync(
            async services =>
            {
                var settings = await services.GetRequiredService<HostSettingsStore>().GetAsync(cancellationToken);
                var runtimeStateStore = services.GetRequiredService<RuntimeStateStore>();
                var health = new HostHealth(
                    true,
                    typeof(LauncherRuntime).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                    settings.LoopbackPort,
                    settings.RemoteAccess.IsEnabled,
                    settings.RemoteAccess.IsEnabled ? BuildRemoteBaseUrl(settings.RemoteAccess) : null,
                    _startedAtUtc ?? DateTimeOffset.UtcNow,
                    runtimeStateStore.ListStatuses().Count(status => status.State == ServerRuntimeState.Running));
                return new HostInfoDto(health, settings);
            },
            cancellationToken);
    }

    private async Task<ServerProfile> RequireProfileAsync(string profileId, CancellationToken cancellationToken)
    {
        var profile = await ExecuteScopedAsync(
            async services => await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken),
            cancellationToken);

        return profile ?? throw new KeyNotFoundException($"Profile '{profileId}' was not found.");
    }

    private Task<OperationResultDto?> QueueLifecycleJobAsync(
        OperationJobKind kind,
        string profileId,
        string summary,
        Func<IServiceProvider, OperationJob, CancellationToken, Task> work,
        string message,
        CancellationToken cancellationToken) =>
        ExecuteScopedAsync(
            async services =>
            {
                var job = await services.GetRequiredService<BackgroundJobDispatcher>()
                    .QueueAsync(kind, profileId, summary, work, cancellationToken);
                return (OperationResultDto?)new OperationResultDto(true, message, job.JobId);
            },
            cancellationToken);

    private static async Task EnsureDatabaseAndRolesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.EnsureReadyAsync(dbContext, cancellationToken);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { UserRole.Owner, UserRole.Admin, UserRole.Operator, UserRole.Viewer })
        {
            if (!await roleManager.RoleExistsAsync(role.ToString()))
            {
                await roleManager.CreateAsync(new IdentityRole(role.ToString()));
            }
        }
    }

    private static ClaimsPrincipal BuildDesktopPrincipal()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "local-system"),
            new Claim(ClaimTypes.Name, "Local desktop"),
            new Claim(ClaimTypes.Role, UserRole.LocalSystem.ToString()),
            new Claim("auth_source", "loopback"),
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "desktop"));
    }

    private static string BuildRemoteBaseUrl(RemoteAccessSettings settings)
    {
        var host = string.IsNullOrWhiteSpace(settings.PublicHostname) ? settings.BindAddress : settings.PublicHostname;
        return $"https://{host}:{settings.HttpsPort}";
    }

    private static bool TryParseWorkshopImagePath(
        string imageUrl,
        out string profileId,
        out string workshopId,
        out WorkshopCatalogItemSource source)
    {
        profileId = string.Empty;
        workshopId = string.Empty;
        source = WorkshopCatalogItemSource.LocalAndSteam;

        if (!Uri.TryCreate(imageUrl, UriKind.RelativeOrAbsolute, out var uri))
        {
            return false;
        }

        var path = uri.IsAbsoluteUri ? uri.AbsolutePath : imageUrl;
        var match = System.Text.RegularExpressions.Regex.Match(
            path,
            @"^/api/profiles/(?<profileId>[^/]+)/workshop-browser/items/(?<workshopId>[^/]+)/image$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        profileId = Uri.UnescapeDataString(match.Groups["profileId"].Value);
        workshopId = Uri.UnescapeDataString(match.Groups["workshopId"].Value);

        var query = uri.IsAbsoluteUri ? uri.Query : (imageUrl.Contains('?') ? imageUrl[imageUrl.IndexOf('?')..] : string.Empty);
        var sourceValue = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(parts => parts.Length == 2 && string.Equals(parts[0], "source", StringComparison.OrdinalIgnoreCase));

        if (sourceValue is not null &&
            Enum.TryParse(Uri.UnescapeDataString(sourceValue[1]), ignoreCase: true, out WorkshopCatalogItemSource parsedSource))
        {
            source = parsedSource;
        }

        return true;
    }

    private static async Task DisposeHostAsync(IHost host)
    {
        if (host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            host.Dispose();
        }
    }
}
