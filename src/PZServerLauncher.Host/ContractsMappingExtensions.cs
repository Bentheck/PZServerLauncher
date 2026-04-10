using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data;

namespace PZServerLauncher.Host;

public static class ContractsMappingExtensions
{
    public static ProfileDto ToDto(this ServerProfile profile) =>
        new(
            profile.ProfileId,
            profile.DisplayName,
            profile.ServerName,
            profile.InstallDirectory,
            profile.CacheDirectory,
            profile.Branch,
            profile.DefaultPort,
            profile.UdpPort,
            profile.RconPort,
            profile.BindIp,
            profile.AdminUsername,
            profile.PreferredMemoryInGigabytes,
            profile.StartWithHost,
            profile.AutoRestartOnCrash,
            profile.WorkshopPreset,
            profile.BackupPolicy);

    public static ServerProfile ToModel(this ProfileUpsertRequestDto request) =>
        new()
        {
            ProfileId = request.ProfileId,
            DisplayName = request.DisplayName,
            ServerName = request.ServerName,
            InstallDirectory = request.InstallDirectory,
            CacheDirectory = request.CacheDirectory,
            Branch = request.Branch,
            DefaultPort = request.DefaultPort,
            UdpPort = request.UdpPort,
            RconPort = request.RconPort,
            UseSteam = request.UseSteam,
            AdminUsername = request.AdminUsername,
            AdminPassword = request.AdminPassword,
            BindIp = request.BindIp,
            PreferredMemoryInGigabytes = request.PreferredMemoryInGigabytes,
            StartWithHost = request.StartWithHost,
            AutoRestartOnCrash = request.AutoRestartOnCrash,
            WorkshopPreset = request.WorkshopPreset,
            BackupPolicy = request.BackupPolicy,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

    public static RemoteAccessSettings ToModel(this RemoteAccessSettingsDto dto) =>
        new()
        {
            IsEnabled = dto.IsEnabled,
            BindAddress = dto.BindAddress,
            HttpsPort = dto.HttpsPort,
            PublicHostname = dto.PublicHostname,
            CertificatePath = dto.CertificatePath,
            CreateFirewallRule = dto.CreateFirewallRule,
            RequiresHostRestart = true,
        };

    public static RemoteAccessSettingsDto ToDto(this RemoteAccessSettings settings) =>
        new(
            settings.IsEnabled,
            settings.BindAddress,
            settings.HttpsPort,
            settings.PublicHostname,
            settings.CertificatePath,
            null,
            settings.CreateFirewallRule);

    public static UserAccountDto ToDto(this ApplicationUser user, IReadOnlyList<UserRole> roles) =>
        new(
            user.Id,
            user.UserName ?? user.Id,
            roles,
            user.TwoFactorEnabled);
}
