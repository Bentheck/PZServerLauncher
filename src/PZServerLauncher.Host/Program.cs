using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Components;
using PZServerLauncher.Host.Components.Account;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Hubs;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Host.Security;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Host;

public class Program
{
    public static async Task Main(string[] args)
    {
        var appPaths = new AppPaths();
        var bootstrapStateStore = new HostBootstrapStateStore(appPaths);
        var bootstrapState = await bootstrapStateStore.LoadAsync();

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, bootstrapState.LoopbackPort);

            if (bootstrapState.RemoteAccessEnabled &&
                !string.IsNullOrWhiteSpace(bootstrapState.CertificatePath) &&
                File.Exists(bootstrapState.CertificatePath))
            {
                options.Listen(IPAddress.Parse(bootstrapState.RemoteBindAddress), bootstrapState.RemoteHttpsPort, listen =>
                {
                    listen.UseHttps(bootstrapState.CertificatePath, bootstrapStateStore.GetCertificatePassword(bootstrapState));
                });
            }
        });

        builder.Services.AddSingleton(appPaths);
        builder.Services.AddSingleton(bootstrapStateStore);
        builder.Services.AddSingleton(new StartupMetadata(DateTimeOffset.UtcNow, typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"));
        builder.Services.AddSingleton<RuntimeStateStore>();
        builder.Services.AddScoped<ProjectZomboidServerPlanner>();
        builder.Services.AddScoped<ProfileStore>();
        builder.Services.AddScoped<HostSettingsStore>();
        builder.Services.AddScoped<JobStore>();
        builder.Services.AddScoped<AuditStore>();
        builder.Services.AddScoped<ConfigFileService>();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
        builder.Services.AddSignalR();

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite($"Data Source={appPaths.DatabasePath};Cache=Shared"));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = "SmartAuth";
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddPolicyScheme("SmartAuth", "Local bearer or identity cookie", policy =>
            {
                policy.ForwardDefaultSelector = context =>
                {
                    var header = context.Request.Headers.Authorization.ToString();
                    return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? LoopbackTokenAuthenticationHandler.SchemeName
                        : IdentityConstants.ApplicationScheme;
                };
            })
            .AddScheme<AuthenticationSchemeOptions, LoopbackTokenAuthenticationHandler>(
                LoopbackTokenAuthenticationHandler.SchemeName,
                _ => { })
            .AddIdentityCookies();

        builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("DesktopOnly", policy => policy.RequireRole(UserRole.LocalSystem.ToString()))
            .AddPolicy("DesktopOrViewer", policy => policy.RequireAssertion(context =>
                IsLocalSystem(context.User) || HasAnyRole(context.User, UserRole.Viewer, UserRole.Operator, UserRole.Admin, UserRole.Owner)))
            .AddPolicy("DesktopOrOperator", policy => policy.RequireAssertion(context =>
                IsLocalSystem(context.User) || HasAnyRole(context.User, UserRole.Operator, UserRole.Admin, UserRole.Owner)))
            .AddPolicy("DesktopOrAdmin", policy => policy.RequireAssertion(context =>
                IsLocalSystem(context.User) || HasAnyRole(context.User, UserRole.Admin, UserRole.Owner)));

        var app = builder.Build();

        await EnsureDatabaseAndRolesAsync(app.Services);

        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        app.MapAdditionalIdentityEndpoints();
        app.MapHub<RuntimeHub>("/hubs/runtime");

        var api = app.MapGroup("/api");

        api.MapGet("/host/info", async (
            HostSettingsStore hostSettingsStore,
            RuntimeStateStore runtimeStateStore,
            StartupMetadata metadata,
            CancellationToken cancellationToken) =>
        {
            var settings = await hostSettingsStore.GetAsync(cancellationToken);
            var health = new HostHealth(
                true,
                metadata.Version,
                settings.LoopbackPort,
                settings.RemoteAccess.IsEnabled,
                settings.RemoteAccess.IsEnabled ? BuildRemoteBaseUrl(settings.RemoteAccess) : null,
                metadata.StartedAtUtc,
                runtimeStateStore.ListStatuses().Count(status => status.State == ServerRuntimeState.Running));

            return Results.Ok(new HostInfoDto(health, settings));
        }).RequireAuthorization("DesktopOrViewer");

        api.MapGet("/profiles", async (ProfileStore store, CancellationToken cancellationToken) =>
        {
            var profiles = await store.ListAsync(cancellationToken);
            return Results.Ok(profiles.Select(x => x.ToDto()));
        }).RequireAuthorization("DesktopOrViewer");

        api.MapGet("/profiles/{profileId}", async (string profileId, ProfileStore store, CancellationToken cancellationToken) =>
        {
            var profile = await store.GetAsync(profileId, cancellationToken);
            return profile is null ? Results.NotFound() : Results.Ok(profile.ToDto());
        }).RequireAuthorization("DesktopOrViewer");

        api.MapPost("/profiles", async (
            ProfileUpsertRequestDto request,
            ProfileStore store,
            AuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var profile = await store.UpsertAsync(request.ToModel(), cancellationToken);
            await auditStore.WriteAsync("profile.created", profile.ProfileId, "local", $"Created profile {profile.DisplayName}.", cancellationToken: cancellationToken);
            return Results.Ok(profile.ToDto());
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapPut("/profiles/{profileId}", async (
            string profileId,
            ProfileUpsertRequestDto request,
            ProfileStore store,
            AuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            if (!string.Equals(profileId, request.ProfileId, StringComparison.Ordinal))
            {
                return Results.BadRequest("Route profile id and payload profile id must match.");
            }

            var profile = await store.UpsertAsync(request.ToModel(), cancellationToken);
            await auditStore.WriteAsync("profile.updated", profile.ProfileId, "local", $"Updated profile {profile.DisplayName}.", cancellationToken: cancellationToken);
            return Results.Ok(profile.ToDto());
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapDelete("/profiles/{profileId}", async (
            string profileId,
            ProfileStore store,
            AuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var deleted = await store.DeleteAsync(profileId, cancellationToken);
            if (!deleted)
            {
                return Results.NotFound();
            }

            await auditStore.WriteAsync("profile.deleted", profileId, "local", $"Deleted profile {profileId}.", cancellationToken: cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapGet("/profiles/{profileId}/status", (string profileId, RuntimeStateStore runtimeStateStore) =>
            Results.Ok(runtimeStateStore.GetOrDefault(profileId)))
            .RequireAuthorization("DesktopOrViewer");

        api.MapGet("/profiles/{profileId}/config/common", async (
            string profileId,
            ProfileStore store,
            ConfigFileService configFileService,
            CancellationToken cancellationToken) =>
        {
            var profile = await store.GetAsync(profileId, cancellationToken);
            return profile is null ? Results.NotFound() : Results.Ok(configFileService.GetCommonConfig(profile));
        }).RequireAuthorization("DesktopOrViewer");

        api.MapPut("/profiles/{profileId}/config/common", async (
            string profileId,
            CommonConfigDto common,
            ProfileStore store,
            ConfigFileService configFileService,
            AuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var profile = await store.GetAsync(profileId, cancellationToken);
            if (profile is null)
            {
                return Results.NotFound();
            }

            var updated = await store.UpsertAsync(configFileService.ApplyCommonConfig(profile, common), cancellationToken);
            await auditStore.WriteAsync("config.common.updated", profileId, "local", "Updated common profile config.", cancellationToken: cancellationToken);
            return Results.Ok(configFileService.GetCommonConfig(updated));
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapGet("/profiles/{profileId}/config/files/{kind}", async (
            string profileId,
            ConfigFileKind kind,
            ProfileStore store,
            ConfigFileService configFileService,
            CancellationToken cancellationToken) =>
        {
            var profile = await store.GetAsync(profileId, cancellationToken);
            return profile is null ? Results.NotFound() : Results.Ok(configFileService.ReadRawFile(profile, kind));
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapPut("/profiles/{profileId}/config/files/{kind}", async (
            string profileId,
            ConfigFileKind kind,
            RawConfigFileDto payload,
            ProfileStore store,
            ConfigFileService configFileService,
            AuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var profile = await store.GetAsync(profileId, cancellationToken);
            if (profile is null)
            {
                return Results.NotFound();
            }

            var updated = configFileService.WriteRawFile(profile, kind, payload.Sha256, payload.Content);
            await auditStore.WriteAsync("config.file.updated", profileId, "local", $"Updated raw config {kind}.", cancellationToken: cancellationToken);
            return Results.Ok(updated);
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapGet("/settings/host", async (HostSettingsStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.GetAsync(cancellationToken)))
            .RequireAuthorization("DesktopOrViewer");

        api.MapPut("/settings/host", async (
            HostSettings settings,
            HostSettingsStore store,
            CancellationToken cancellationToken) =>
        {
            var updated = await store.UpdateAsync(settings, null, cancellationToken);
            return Results.Ok(updated);
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapGet("/settings/remote-access", async (HostSettingsStore store, CancellationToken cancellationToken) =>
        {
            var settings = await store.GetAsync(cancellationToken);
            return Results.Ok(settings.RemoteAccess.ToDto());
        }).RequireAuthorization("DesktopOrViewer");

        api.MapPut("/settings/remote-access", async (
            RemoteAccessSettingsDto dto,
            HostSettingsStore store,
            CancellationToken cancellationToken) =>
        {
            var current = await store.GetAsync(cancellationToken);
            var updated = current with
            {
                RemoteAccess = dto.ToModel(),
            };

            var saved = await store.UpdateAsync(updated, dto.CertificatePassword, cancellationToken);
            return Results.Ok(saved.RemoteAccess.ToDto());
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapGet("/jobs/{jobId:guid}", async (Guid jobId, JobStore store, CancellationToken cancellationToken) =>
        {
            var job = await store.GetAsync(jobId, cancellationToken);
            return job is null ? Results.NotFound() : Results.Ok(job);
        }).RequireAuthorization("DesktopOrViewer");

        api.MapGet("/users", async (
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
        {
            var users = await userManager.Users.OrderBy(x => x.UserName).ToListAsync(cancellationToken);
            var results = new List<UserAccountDto>(users.Count);
            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                results.Add(user.ToDto(roles.Select(role => Enum.Parse<UserRole>(role)).ToArray()));
            }

            return Results.Ok(results);
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapPost("/onboarding/bootstrap", async (
            BootstrapOwnerRequestDto request,
            UserManager<ApplicationUser> userManager,
            HostSettingsStore hostSettingsStore,
            AuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            if (await userManager.Users.AnyAsync(cancellationToken))
            {
                return Results.BadRequest(new OperationResultDto(false, "Owner bootstrap has already been completed."));
            }

            var user = new ApplicationUser
            {
                UserName = request.UserName,
                Email = request.Email,
                DisplayName = request.UserName,
                EmailConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                return Results.BadRequest(new OperationResultDto(false, string.Join("; ", createResult.Errors.Select(x => x.Description))));
            }

            await userManager.AddToRoleAsync(user, UserRole.Owner.ToString());

            var settings = await hostSettingsStore.GetAsync(cancellationToken);
            await hostSettingsStore.UpdateAsync(settings with
            {
                OwnerBootstrap = new OwnerBootstrapState(true, user.Id, user.UserName, DateTimeOffset.UtcNow),
            }, null, cancellationToken);

            await auditStore.WriteAsync("owner.bootstrapped", "host", "local", $"Bootstrapped owner {user.UserName}.", user.Id, cancellationToken);
            return Results.Ok(new OperationResultDto(true, "Owner account created."));
        }).RequireAuthorization("DesktopOnly");

        await app.RunAsync();
    }

    private static bool IsLocalSystem(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(UserRole.LocalSystem.ToString());

    private static bool HasAnyRole(System.Security.Claims.ClaimsPrincipal user, params UserRole[] roles) =>
        roles.Any(role => user.IsInRole(role.ToString()));

    private static async Task EnsureDatabaseAndRolesAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { UserRole.Owner, UserRole.Admin, UserRole.Operator, UserRole.Viewer })
        {
            if (!await roleManager.RoleExistsAsync(role.ToString()))
            {
                await roleManager.CreateAsync(new IdentityRole(role.ToString()));
            }
        }
    }

    private static string BuildRemoteBaseUrl(RemoteAccessSettings settings)
    {
        var host = string.IsNullOrWhiteSpace(settings.PublicHostname) ? settings.BindAddress : settings.PublicHostname;
        return $"https://{host}:{settings.HttpsPort}";
    }
}

public sealed record StartupMetadata(DateTimeOffset StartedAtUtc, string Version);
