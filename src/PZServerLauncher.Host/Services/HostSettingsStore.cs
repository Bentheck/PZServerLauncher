using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Data.Entities;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Services;

public sealed class HostSettingsStore(
    ApplicationDbContext dbContext,
    HostBootstrapStateStore bootstrapStateStore,
    HostStartupRegistrationService startupRegistrationService)
{
    public async Task<HostSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(cancellationToken);
        return entity.ToModel();
    }

    public async Task<HostSettings> UpdateAsync(HostSettings settings, string? certificatePassword = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(cancellationToken);
        entity.ApplyModel(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        await bootstrapStateStore.UpdateRemoteAccessAsync(settings.RemoteAccess, certificatePassword, cancellationToken);
        await startupRegistrationService.SyncAsync(settings.StartHostWithWindows, cancellationToken);
        return entity.ToModel();
    }

    private async Task<HostSettingsEntity> GetOrCreateEntityAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.HostSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (entity is not null)
        {
            return entity;
        }

        var bootstrap = await bootstrapStateStore.LoadAsync(cancellationToken);
        entity = new HostSettingsEntity
        {
            Id = 1,
            LoopbackPort = bootstrap.LoopbackPort,
            RemoteAccessEnabled = bootstrap.RemoteAccessEnabled,
            RemoteBindAddress = bootstrap.RemoteBindAddress,
            RemoteHttpsPort = bootstrap.RemoteHttpsPort,
            PublicHostname = bootstrap.PublicHostname,
            CertificatePath = bootstrap.CertificatePath,
            CreateFirewallRule = bootstrap.CreateFirewallRule,
        };

        dbContext.HostSettings.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
