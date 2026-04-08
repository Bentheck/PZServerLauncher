using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
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
        builder.Services.AddSingleton<ProjectZomboidServerPlanner>();
        builder.Services.AddHttpClient(nameof(SteamCmdToolService));
        builder.Services.AddSingleton<SteamCmdToolService>();
        builder.Services.AddSingleton<ServerProcessSupervisor>();
        builder.Services.AddSingleton<BackgroundJobDispatcher>();
        builder.Services.AddSingleton<WorkshopPresetScannerService>();
        builder.Services.AddHostedService<ProfileAutoStartService>();
        builder.Services.AddSingleton<DatabaseInitializer>();
        builder.Services.AddSingleton<HostStartupRegistrationService>();
        builder.Services.AddScoped<ProfileStore>();
        builder.Services.AddScoped<HostSettingsStore>();
        builder.Services.AddScoped<JobStore>();
        builder.Services.AddScoped<AuditStore>();
        builder.Services.AddScoped<ConfigFileService>();
        builder.Services.AddScoped<ServerInstallService>();
        builder.Services.AddScoped<ServerBackupService>();
        builder.Services.AddScoped<LocalServerImportService>();
        builder.Services.AddScoped<UserManagementService>();
        builder.Services.AddScoped<RemoteAccessDiagnosticsService>();
        builder.Services.AddScoped<WindowsFirewallRuleService>();

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
                    var hasLoopbackToken = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
                        (context.Request.Path.StartsWithSegments("/hubs/runtime") &&
                         context.Request.Query.ContainsKey("access_token"));

                    return hasLoopbackToken
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
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (!HttpMethods.IsPost(context.Request.Method) ||
                    !context.Request.Path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetNoLimiter("default");
                }

                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"auth:{ipAddress}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(15),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
            });
        });

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
        app.UseRateLimiter();
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

        api.MapPost("/host/stop", async (
            HostShutdownRequestDto request,
            RuntimeStateStore runtimeStateStore,
            ServerProcessSupervisor supervisor,
            AuditStore auditStore,
            IHostApplicationLifetime lifetime,
            CancellationToken cancellationToken) =>
        {
            var runningProfiles = runtimeStateStore.ListStatuses()
                .Where(status => status.State is ServerRuntimeState.Starting or ServerRuntimeState.Running or ServerRuntimeState.Stopping)
                .Select(status => status.ProfileId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (runningProfiles.Count > 0 && !request.StopRunningServers)
            {
                return Results.Conflict(new OperationResultDto(
                    false,
                    $"{runningProfiles.Count} managed server(s) are still active. Use the 'Stop All + Host' action to stop them before shutting down the host."));
            }

            if (request.StopRunningServers)
            {
                foreach (var profileId in runningProfiles)
                {
                    await supervisor.StopAsync(profileId, cancellationToken);
                }
            }

            await auditStore.WriteAsync(
                "host.stopped",
                "host",
                "local",
                request.StopRunningServers && runningProfiles.Count > 0
                    ? $"Stopped {runningProfiles.Count} server(s) and shut down the local host."
                    : "Shut down the local host.",
                cancellationToken: cancellationToken);

            _ = Task.Run(async () =>
            {
                await Task.Delay(250);
                lifetime.StopApplication();
            }, CancellationToken.None);

            return Results.Ok(new OperationResultDto(
                true,
                request.StopRunningServers && runningProfiles.Count > 0
                    ? $"Stopping {runningProfiles.Count} server(s) and shutting down the local host."
                    : "Shutting down the local host."));
        }).RequireAuthorization("DesktopOnly");

        api.MapGet("/profiles", async (ProfileStore store, CancellationToken cancellationToken) =>
        {
            var profiles = await store.ListAsync(cancellationToken);
            return Results.Ok(profiles.Select(x => x.ToDto()));
        }).RequireAuthorization("DesktopOrViewer");

        api.MapGet("/import/local", async (
            LocalServerImportService importService,
            CancellationToken cancellationToken) =>
            Results.Ok(await importService.DiscoverAsync(cancellationToken)))
            .RequireAuthorization("DesktopOrAdmin");

        api.MapPost("/import/local/{candidateId}", async (
            string candidateId,
            LocalServerImportService importService,
            AuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var profile = await importService.ImportAsync(candidateId, cancellationToken);
            await auditStore.WriteAsync("profile.imported", profile.ProfileId, "local", $"Imported local server '{profile.ServerName}'.", cancellationToken: cancellationToken);
            return Results.Ok(profile.ToDto());
        }).RequireAuthorization("DesktopOrAdmin");

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

        api.MapPost("/profiles/{profileId}/workshop/scan", async (
            string profileId,
            ProfileStore store,
            WorkshopPresetScannerService workshopScannerService,
            CancellationToken cancellationToken) =>
        {
            var profile = await store.GetAsync(profileId, cancellationToken);
            return profile is null
                ? Results.NotFound()
                : Results.Ok(workshopScannerService.Scan(profile.InstallDirectory, profile.WorkshopPreset));
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

            RawConfigFileDto updated;
            try
            {
                updated = configFileService.WriteRawFile(profile, kind, payload.Sha256, payload.Content);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new OperationResultDto(false, ex.Message));
            }

            await auditStore.WriteAsync("config.file.updated", profileId, "local", $"Updated raw config {kind}.", cancellationToken: cancellationToken);
            return Results.Ok(updated);
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapGet("/profiles/{profileId}/logs/recent", (
            string profileId,
            RuntimeStateStore runtimeStateStore) =>
            Results.Ok(runtimeStateStore.GetRecentLogs(profileId)))
            .RequireAuthorization("DesktopOrViewer");

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
            HostBootstrapStateStore bootstrapStateStore,
            UserManagementService userManagementService,
            CancellationToken cancellationToken) =>
        {
            var effectivePassword = await bootstrapStateStore.ResolveCertificatePasswordAsync(
                dto.CertificatePath,
                dto.CertificatePassword,
                cancellationToken);
            var validatedDto = dto with
            {
                CertificatePassword = effectivePassword,
            };
            RemoteAccessSettingsValidator.Validate(validatedDto);
            if (validatedDto.IsEnabled)
            {
                await userManagementService.EnsureRemoteAccessReadyAsync(cancellationToken);
            }

            var current = await store.GetAsync(cancellationToken);
            var updated = current with
            {
                RemoteAccess = validatedDto.ToModel(),
            };

            var saved = await store.UpdateAsync(updated, effectivePassword, cancellationToken);
            return Results.Ok(saved.RemoteAccess.ToDto());
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapPost("/settings/remote-access/self-test", async (
            RemoteAccessSettingsDto dto,
            RemoteAccessDiagnosticsService diagnosticsService,
            CancellationToken cancellationToken) =>
            Results.Ok(await diagnosticsService.RunSelfTestAsync(dto, cancellationToken)))
            .RequireAuthorization("DesktopOrAdmin");

        api.MapPost("/settings/remote-access/firewall", async (
            RemoteAccessSettingsDto dto,
            WindowsFirewallRuleService firewallRuleService,
            CancellationToken cancellationToken) =>
            Results.Ok(await firewallRuleService.EnsureRemoteAccessRuleAsync(dto, cancellationToken)))
            .RequireAuthorization("DesktopOrAdmin");

        api.MapGet("/jobs/{jobId:guid}", async (Guid jobId, JobStore store, CancellationToken cancellationToken) =>
        {
            var job = await store.GetAsync(jobId, cancellationToken);
            return job is null ? Results.NotFound() : Results.Ok(job);
        }).RequireAuthorization("DesktopOrViewer");

        api.MapGet("/jobs", async (
            int? take,
            JobStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.ListRecentAsync(Math.Clamp(take ?? 20, 1, 50), cancellationToken)))
            .RequireAuthorization("DesktopOrViewer");

        api.MapPost("/profiles/{profileId}/install", async (
            string profileId,
            BackgroundJobDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var job = await dispatcher.QueueAsync(
                OperationJobKind.Install,
                profileId,
                $"Install {profileId}",
                async (services, runningJob, token) =>
                {
                    var installer = services.GetRequiredService<ServerInstallService>();
                    var jobStore = services.GetRequiredService<JobStore>();
                    var runtimeStateStore = services.GetRequiredService<RuntimeStateStore>();
                    await installer.ExecuteInstallAsync(profileId, runningJob, jobStore, runtimeStateStore, token);
                },
                cancellationToken);
            return Results.Accepted($"/api/jobs/{job.JobId}", new OperationResultDto(true, "Install queued.", job.JobId));
        }).RequireAuthorization("DesktopOrOperator");

        api.MapPost("/profiles/{profileId}/update", async (
            string profileId,
            BackgroundJobDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var job = await dispatcher.QueueAsync(
                OperationJobKind.Update,
                profileId,
                $"Update {profileId}",
                async (services, runningJob, token) =>
                {
                    var installer = services.GetRequiredService<ServerInstallService>();
                    var jobStore = services.GetRequiredService<JobStore>();
                    var runtimeStateStore = services.GetRequiredService<RuntimeStateStore>();
                    var backupService = services.GetRequiredService<ServerBackupService>();
                    await backupService.CreateBackupAsync(profileId, BackupTrigger.PreUpdate, token);
                    await installer.ExecuteInstallAsync(profileId, runningJob, jobStore, runtimeStateStore, token);
                },
                cancellationToken);
            return Results.Accepted($"/api/jobs/{job.JobId}", new OperationResultDto(true, "Update queued.", job.JobId));
        }).RequireAuthorization("DesktopOrOperator");

        api.MapPost("/profiles/{profileId}/start", async (
            string profileId,
            ProfileStore profileStore,
            ServerProcessSupervisor supervisor,
            AuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var profile = await profileStore.GetAsync(profileId, cancellationToken);
            if (profile is null)
            {
                return Results.NotFound();
            }

            await supervisor.StartAsync(profile, cancellationToken);
            await auditStore.WriteAsync("runtime.started", profileId, "local", "Started server process.", cancellationToken: cancellationToken);
            return Results.Ok(new OperationResultDto(true, "Server started."));
        }).RequireAuthorization("DesktopOrOperator");

        api.MapPost("/profiles/{profileId}/stop", async (
            string profileId,
            ServerProcessSupervisor supervisor,
            AuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            await supervisor.StopAsync(profileId, cancellationToken);
            await auditStore.WriteAsync("runtime.stopped", profileId, "local", "Stopped server process.", cancellationToken: cancellationToken);
            return Results.Ok(new OperationResultDto(true, "Server stopped."));
        }).RequireAuthorization("DesktopOrOperator");

        api.MapPost("/profiles/{profileId}/restart", async (
            string profileId,
            ProfileStore profileStore,
            ServerProcessSupervisor supervisor,
            AuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var profile = await profileStore.GetAsync(profileId, cancellationToken);
            if (profile is null)
            {
                return Results.NotFound();
            }

            await supervisor.StopAsync(profileId, cancellationToken);
            await supervisor.StartAsync(profile, cancellationToken);
            await auditStore.WriteAsync("runtime.restarted", profileId, "local", "Restarted server process.", cancellationToken: cancellationToken);
            return Results.Ok(new OperationResultDto(true, "Server restarted."));
        }).RequireAuthorization("DesktopOrOperator");

        api.MapGet("/profiles/{profileId}/backups", (
            string profileId,
            ServerBackupService backupService) =>
            Results.Ok(backupService.ListBackups(profileId)))
            .RequireAuthorization("DesktopOrViewer");

        api.MapPost("/profiles/{profileId}/backup", async (
            string profileId,
            BackgroundJobDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var job = await dispatcher.QueueAsync(
                OperationJobKind.Backup,
                profileId,
                $"Backup {profileId}",
                async (services, runningJob, token) =>
                {
                    var backupService = services.GetRequiredService<ServerBackupService>();
                    var jobStore = services.GetRequiredService<JobStore>();
                    var zipPath = await backupService.CreateBackupAsync(profileId, BackupTrigger.Manual, token);
                    await jobStore.UpdateAsync(runningJob with
                    {
                        Status = OperationJobStatus.Succeeded,
                        ProgressPercent = 100,
                        Detail = $"Created backup {Path.GetFileName(zipPath)}.",
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                    }, token);
                },
                cancellationToken);
            return Results.Accepted($"/api/jobs/{job.JobId}", new OperationResultDto(true, "Backup queued.", job.JobId));
        }).RequireAuthorization("DesktopOrOperator");

        api.MapPost("/profiles/{profileId}/restore", async (
            string profileId,
            RestoreBackupRequestDto request,
            BackgroundJobDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var job = await dispatcher.QueueAsync(
                OperationJobKind.Restore,
                profileId,
                $"Restore {profileId}",
                async (services, runningJob, token) =>
                {
                    var supervisor = services.GetRequiredService<ServerProcessSupervisor>();
                    var backupService = services.GetRequiredService<ServerBackupService>();
                    var profileStore = services.GetRequiredService<ProfileStore>();
                    var jobStore = services.GetRequiredService<JobStore>();

                    if (supervisor.IsRunning(profileId))
                    {
                        await supervisor.StopAsync(profileId, token);
                    }

                    await backupService.RestoreBackupAsync(profileId, request.BackupFileName, token);

                    if (request.RestartAfterRestore)
                    {
                        var profile = await profileStore.GetAsync(profileId, token)
                            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
                        await supervisor.StartAsync(profile, token);
                    }

                    await jobStore.UpdateAsync(runningJob with
                    {
                        Status = OperationJobStatus.Succeeded,
                        ProgressPercent = 100,
                        Detail = $"Restored backup {request.BackupFileName}.",
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                    }, token);
                },
                cancellationToken);
            return Results.Accepted($"/api/jobs/{job.JobId}", new OperationResultDto(true, "Restore queued.", job.JobId));
        }).RequireAuthorization("DesktopOrOperator");

        api.MapGet("/users", async (
            UserManagementService userManagementService,
            CancellationToken cancellationToken) =>
            Results.Ok(await userManagementService.ListAsync(cancellationToken)))
            .RequireAuthorization("DesktopOrAdmin");

        api.MapPost("/users", async (
            CreateUserRequestDto request,
            UserManagementService userManagementService,
            AuditStore auditStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var created = await userManagementService.CreateAsync(request, cancellationToken);
                await auditStore.WriteAsync("user.created", created.UserId, "local", $"Created user {created.UserName}.", user.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
                return Results.Ok(created);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new OperationResultDto(false, ex.Message));
            }
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapPut("/users/{userId}", async (
            string userId,
            UpdateUserRequestDto request,
            UserManagementService userManagementService,
            AuditStore auditStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var updated = await userManagementService.UpdateAsync(userId, request, user.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
                await auditStore.WriteAsync("user.updated", updated.UserId, "local", $"Updated user {updated.UserName}.", user.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new OperationResultDto(false, ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new OperationResultDto(false, ex.Message));
            }
        }).RequireAuthorization("DesktopOrAdmin");

        api.MapDelete("/users/{userId}", async (
            string userId,
            UserManagementService userManagementService,
            AuditStore auditStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await userManagementService.DeleteAsync(userId, user.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
                await auditStore.WriteAsync("user.deleted", userId, "local", $"Deleted user {userId}.", user.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new OperationResultDto(false, ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new OperationResultDto(false, ex.Message));
            }
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
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.EnsureReadyAsync(dbContext);

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
