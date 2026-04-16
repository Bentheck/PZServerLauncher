using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Data.Entities;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Services;

public sealed class WorkshopBrowserSettingsStore
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDataProtector _protector;

    public WorkshopBrowserSettingsStore(ApplicationDbContext dbContext, AppPaths appPaths)
    {
        _dbContext = dbContext;
        var provider = DataProtectionProvider.Create(new DirectoryInfo(appPaths.StateDirectory));
        _protector = provider.CreateProtector("PZServerLauncher.WorkshopBrowserSettings");
    }

    public async Task<SteamWorkshopBrowserSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(cancellationToken);
        return new SteamWorkshopBrowserSettingsDto(!string.IsNullOrWhiteSpace(entity.ProtectedSteamWebApiKey));
    }

    public async Task<string?> GetSteamWebApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(entity.ProtectedSteamWebApiKey)
            ? null
            : _protector.Unprotect(entity.ProtectedSteamWebApiKey);
    }

    public async Task<SteamWorkshopBrowserSettingsDto> SetSteamWebApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(cancellationToken);
        entity.ProtectedSteamWebApiKey = string.IsNullOrWhiteSpace(apiKey)
            ? null
            : _protector.Protect(apiKey.Trim());
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new SteamWorkshopBrowserSettingsDto(!string.IsNullOrWhiteSpace(entity.ProtectedSteamWebApiKey));
    }

    public async Task<SteamWorkshopBrowserSettingsDto> RemoveSteamWebApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(cancellationToken);
        entity.ProtectedSteamWebApiKey = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new SteamWorkshopBrowserSettingsDto(false);
    }

    private async Task<HostSettingsEntity> GetOrCreateEntityAsync(CancellationToken cancellationToken)
    {
        var entity = await _dbContext.HostSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (entity is not null)
        {
            return entity;
        }

        entity = new HostSettingsEntity
        {
            Id = 1,
        };

        _dbContext.HostSettings.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
