using System.Text.Json;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data.Entities;

namespace PZServerLauncher.Host.Data;

public static class EntityMappingExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static ServerProfile ToModel(this ServerProfileEntity entity) =>
        new()
        {
            ProfileId = entity.ProfileId,
            DisplayName = entity.DisplayName,
            ServerName = entity.ServerName,
            InstallDirectory = entity.InstallDirectory,
            CacheDirectory = entity.CacheDirectory,
            Branch = (ProjectZomboidBranch)entity.Branch,
            DefaultPort = entity.DefaultPort,
            UdpPort = entity.UdpPort,
            RconPort = entity.RconPort,
            UseSteam = entity.UseSteam,
            AdminUsername = entity.AdminUsername,
            AdminPassword = entity.AdminPassword,
            BindIp = entity.BindIp,
            PreferredMemoryInGigabytes = entity.PreferredMemoryInGigabytes,
            StartWithHost = entity.StartWithHost,
            AutoRestartOnCrash = entity.AutoRestartOnCrash,
            WorkshopPreset = new WorkshopPreset
            {
                WorkshopItemIds = DeserializeList(entity.WorkshopItemIdsJson),
                EnabledModIds = DeserializeList(entity.EnabledModIdsJson),
                MapFolders = DeserializeList(entity.MapFoldersJson),
            },
            BackupPolicy = new BackupPolicy
            {
                ScheduledBackupsEnabled = entity.ScheduledBackupsEnabled,
                ScheduledBackupRetentionCount = entity.ScheduledBackupRetentionCount,
                PreUpdateBackupRetentionCount = entity.PreUpdateBackupRetentionCount,
                KeepManualBackupsForever = entity.KeepManualBackupsForever,
                PreUpdateBackupEnabled = entity.PreUpdateBackupEnabled,
            },
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };

    public static ServerProfileEntity ToEntity(this ServerProfile model) =>
        new()
        {
            ProfileId = model.ProfileId,
            DisplayName = model.DisplayName,
            ServerName = model.ServerName,
            InstallDirectory = model.InstallDirectory,
            CacheDirectory = model.CacheDirectory,
            Branch = (int)model.Branch,
            DefaultPort = model.DefaultPort,
            UdpPort = model.UdpPort,
            RconPort = model.RconPort,
            UseSteam = model.UseSteam,
            AdminUsername = model.AdminUsername,
            AdminPassword = model.AdminPassword,
            BindIp = model.BindIp,
            PreferredMemoryInGigabytes = model.PreferredMemoryInGigabytes,
            StartWithHost = model.StartWithHost,
            AutoRestartOnCrash = model.AutoRestartOnCrash,
            WorkshopItemIdsJson = SerializeList(model.WorkshopPreset.WorkshopItemIds),
            EnabledModIdsJson = SerializeList(model.WorkshopPreset.EnabledModIds),
            MapFoldersJson = SerializeList(model.WorkshopPreset.MapFolders),
            ScheduledBackupsEnabled = model.BackupPolicy.ScheduledBackupsEnabled,
            ScheduledBackupRetentionCount = model.BackupPolicy.ScheduledBackupRetentionCount,
            PreUpdateBackupRetentionCount = model.BackupPolicy.PreUpdateBackupRetentionCount,
            KeepManualBackupsForever = model.BackupPolicy.KeepManualBackupsForever,
            PreUpdateBackupEnabled = model.BackupPolicy.PreUpdateBackupEnabled,
            CreatedAtUtc = model.CreatedAtUtc,
            UpdatedAtUtc = model.UpdatedAtUtc,
        };

    public static void ApplyModel(this ServerProfileEntity entity, ServerProfile model)
    {
        entity.DisplayName = model.DisplayName;
        entity.ServerName = model.ServerName;
        entity.InstallDirectory = model.InstallDirectory;
        entity.CacheDirectory = model.CacheDirectory;
        entity.Branch = (int)model.Branch;
        entity.DefaultPort = model.DefaultPort;
        entity.UdpPort = model.UdpPort;
        entity.RconPort = model.RconPort;
        entity.UseSteam = model.UseSteam;
        entity.AdminUsername = model.AdminUsername;
        entity.AdminPassword = model.AdminPassword;
        entity.BindIp = model.BindIp;
        entity.PreferredMemoryInGigabytes = model.PreferredMemoryInGigabytes;
        entity.StartWithHost = model.StartWithHost;
        entity.AutoRestartOnCrash = model.AutoRestartOnCrash;
        entity.WorkshopItemIdsJson = SerializeList(model.WorkshopPreset.WorkshopItemIds);
        entity.EnabledModIdsJson = SerializeList(model.WorkshopPreset.EnabledModIds);
        entity.MapFoldersJson = SerializeList(model.WorkshopPreset.MapFolders);
        entity.ScheduledBackupsEnabled = model.BackupPolicy.ScheduledBackupsEnabled;
        entity.ScheduledBackupRetentionCount = model.BackupPolicy.ScheduledBackupRetentionCount;
        entity.PreUpdateBackupRetentionCount = model.BackupPolicy.PreUpdateBackupRetentionCount;
        entity.KeepManualBackupsForever = model.BackupPolicy.KeepManualBackupsForever;
        entity.PreUpdateBackupEnabled = model.BackupPolicy.PreUpdateBackupEnabled;
        entity.UpdatedAtUtc = model.UpdatedAtUtc;
    }

    public static HostSettings ToModel(this HostSettingsEntity entity) =>
        new()
        {
            LoopbackPort = entity.LoopbackPort,
            StartHostWithWindows = entity.StartHostWithWindows,
            RemoteAccess = new RemoteAccessSettings
            {
                IsEnabled = entity.RemoteAccessEnabled,
                BindAddress = entity.RemoteBindAddress,
                HttpsPort = entity.RemoteHttpsPort,
                PublicHostname = entity.PublicHostname,
                CertificatePath = entity.CertificatePath,
                CreateFirewallRule = entity.CreateFirewallRule,
            },
            OwnerBootstrap = new OwnerBootstrapState(
                entity.OwnerIsConfigured,
                entity.OwnerUserId,
                entity.OwnerUserName,
                entity.OwnerConfiguredAtUtc),
        };

    public static void ApplyModel(this HostSettingsEntity entity, HostSettings model)
    {
        entity.LoopbackPort = model.LoopbackPort;
        entity.StartHostWithWindows = model.StartHostWithWindows;
        entity.RemoteAccessEnabled = model.RemoteAccess.IsEnabled;
        entity.RemoteBindAddress = model.RemoteAccess.BindAddress;
        entity.RemoteHttpsPort = model.RemoteAccess.HttpsPort;
        entity.PublicHostname = model.RemoteAccess.PublicHostname;
        entity.CertificatePath = model.RemoteAccess.CertificatePath;
        entity.CreateFirewallRule = model.RemoteAccess.CreateFirewallRule;
        entity.OwnerIsConfigured = model.OwnerBootstrap.IsConfigured;
        entity.OwnerUserId = model.OwnerBootstrap.OwnerUserId;
        entity.OwnerUserName = model.OwnerBootstrap.OwnerUserName;
        entity.OwnerConfiguredAtUtc = model.OwnerBootstrap.ConfiguredAtUtc;
    }

    public static OperationJob ToModel(this OperationJobEntity entity) =>
        new(
            entity.JobId,
            (OperationJobKind)entity.Kind,
            (OperationJobStatus)entity.Status,
            entity.ProfileId,
            entity.Summary,
            entity.Detail,
            entity.ProgressPercent,
            entity.CreatedAtUtc,
            entity.StartedAtUtc,
            entity.CompletedAtUtc);

    public static OperationJobEntity ToEntity(this OperationJob model) =>
        new()
        {
            JobId = model.JobId,
            Kind = (int)model.Kind,
            Status = (int)model.Status,
            ProfileId = model.ProfileId,
            Summary = model.Summary,
            Detail = model.Detail,
            ProgressPercent = model.ProgressPercent,
            CreatedAtUtc = model.CreatedAtUtc,
            StartedAtUtc = model.StartedAtUtc,
            CompletedAtUtc = model.CompletedAtUtc,
        };

    public static AuditEntry ToModel(this AuditEntryEntity entity) =>
        new(
            entity.EntryId,
            entity.OccurredAtUtc,
            entity.Action,
            entity.Subject,
            entity.ActorType,
            entity.ActorId,
            entity.Detail);

    private static IReadOnlyList<string> DeserializeList(string json) =>
        JsonSerializer.Deserialize<List<string>>(json, SerializerOptions) ?? [];

    private static string SerializeList(IReadOnlyList<string> values) =>
        JsonSerializer.Serialize(values, SerializerOptions);
}
