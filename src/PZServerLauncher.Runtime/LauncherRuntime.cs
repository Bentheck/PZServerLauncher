using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;
using PZServerLauncher.Host;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Infrastructure.Planning;
using PZServerLauncher.Infrastructure.Settings;

namespace PZServerLauncher.Runtime;

public sealed partial class LauncherRuntime : ILauncherRuntime
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly RuntimeEventBus _eventBus = new();
    private readonly string? _rootDirectory;
    private IHost? _host;
    private DateTimeOffset? _startedAtUtc;

    public LauncherRuntime(string? rootDirectory = null)
    {
        _rootDirectory = rootDirectory;
    }

    public event Func<ServerRuntimeStatus, Task>? StatusChanged
    {
        add => _eventBus.StatusChanged += value;
        remove => _eventBus.StatusChanged -= value;
    }

    public event Func<OperationJob, Task>? JobChanged
    {
        add => _eventBus.JobChanged += value;
        remove => _eventBus.JobChanged -= value;
    }

    public event Func<string, string, Task>? LogLineReceived
    {
        add => _eventBus.LogLineReceived += value;
        remove => _eventBus.LogLineReceived -= value;
    }

    public event Func<ProfileLiveOperationsSnapshot, Task>? LiveOperationsChanged
    {
        add => _eventBus.LiveOperationsChanged += value;
        remove => _eventBus.LiveOperationsChanged -= value;
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_host is not null)
            {
                await _host.StopAsync();
                await DisposeHostAsync(_host);
                _host = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_host is not null)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_host is not null)
            {
                return;
            }

            var host = BuildHost();
            await host.StartAsync(cancellationToken);
            await EnsureDatabaseAndRolesAsync(host.Services, cancellationToken);
            _host = host;
            _startedAtUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IHost BuildHost()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(new AppPaths(_rootDirectory));
        builder.Services.AddSingleton<HostBootstrapStateStore>();
        builder.Services.AddSingleton(_eventBus);
        builder.Services.AddSingleton<IRuntimeEventPublisher>(_eventBus);
        builder.Services.AddSingleton<PersistentLogService>();
        builder.Services.AddSingleton<IRuntimeLogSink>(serviceProvider => serviceProvider.GetRequiredService<PersistentLogService>());
        builder.Services.AddSingleton<ILoggerProvider, RollingFileLoggerProvider>();
        builder.Services.AddSingleton<ProjectZomboidLiveOperationsInterpreter>();
        builder.Services.AddSingleton<RuntimeStateStore>();
        builder.Services.AddSingleton<ProjectZomboidServerPlanner>();
        builder.Services.AddSingleton<ICapabilityResolver, CapabilityResolver>();
        builder.Services.AddScoped<WorkspaceBootstrapService>();
        builder.Services.AddSingleton<ISettingsCatalogResolver, ProjectZomboidSettingsCatalogResolver>();
        builder.Services.AddSingleton<IIniDocumentService, IniDocumentService>();
        builder.Services.AddSingleton<ISandboxVarsDocumentService, SandboxVarsDocumentService>();
        builder.Services.AddSingleton<ISandboxPresetDocumentService, SandboxPresetDocumentService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient(nameof(SteamCmdToolService));
        builder.Services.AddHttpClient(nameof(SteamWorkshopApiClient));
        builder.Services.AddSingleton<SteamCmdToolService>();
        builder.Services.AddSingleton<ServerProcessSupervisor>();
        builder.Services.AddSingleton<BackgroundJobDispatcher>();
        builder.Services.AddSingleton<WorkshopPresetScannerService>();
        builder.Services.AddScoped<WorkshopBrowserSettingsStore>();
        builder.Services.AddScoped<WorkshopCatalogService>();
        builder.Services.AddScoped<SteamWorkshopApiClient>();
        builder.Services.AddHostedService<ProfileAutoStartService>();
        builder.Services.AddHostedService<ScheduledBackupService>();
        builder.Services.AddSingleton<DatabaseInitializer>();
        builder.Services.AddSingleton<HostStartupRegistrationService>();
        builder.Services.AddScoped<ProfileStore>();
        builder.Services.AddScoped<HostSettingsStore>();
        builder.Services.AddScoped<JobStore>();
        builder.Services.AddScoped<AuditStore>();
        builder.Services.AddScoped<ConfigFileService>();
        builder.Services.AddScoped<StructuredSettingsService>();
        builder.Services.AddScoped<SettingsDraftStore>();
        builder.Services.AddScoped<ModsMapsDraftStore>();
        builder.Services.AddScoped<NamedWorkshopPresetStore>();
        builder.Services.AddSingleton<SandboxPresetLibraryService>();
        builder.Services.AddScoped<ProfileRetirementService>();
        builder.Services.AddScoped<ServerInstallService>();
        builder.Services.AddScoped<ServerBackupService>();
        builder.Services.AddScoped<ServerWorldResetService>();
        builder.Services.AddScoped<LocalServerImportService>();
        builder.Services.AddScoped<UserManagementService>();
        builder.Services.AddScoped<RemoteAccessDiagnosticsService>();
        builder.Services.AddScoped<WindowsFirewallRuleService>();

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            var appPaths = new AppPaths(_rootDirectory);
            options.UseSqlite($"Data Source={appPaths.DatabasePath};Cache=Shared");
            options.ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return builder.Build();
    }

    private async Task<T> ExecuteScopedAsync<T>(Func<IServiceProvider, Task<T>> action, CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken);
        var host = _host ?? throw new InvalidOperationException("Integrated runtime is not available.");
        await using var scope = host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }
}
