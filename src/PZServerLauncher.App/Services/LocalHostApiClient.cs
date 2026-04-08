using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.Services;

public sealed class LocalHostApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _rootDirectory;
    private readonly string _stateDirectory;
    private readonly string _stateFilePath;

    public LocalHostApiClient()
    {
        _rootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PZServerLauncher");
        _stateDirectory = Path.Combine(_rootDirectory, "state");
        _stateFilePath = Path.Combine(_stateDirectory, "host-state.json");
    }

    public async Task<HostSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await EnsureHostRunningAsync(cancellationToken);
        var hostInfo = await GetAsync<HostInfoDto>("/api/host/info", cancellationToken)
            ?? throw new InvalidOperationException("Failed to load host information.");
        var profiles = await GetAsync<List<ProfileDto>>("/api/profiles", cancellationToken) ?? [];
        var jobs = await GetAsync<List<OperationJob>>("/api/jobs?take=20", cancellationToken) ?? [];

        var statuses = new Dictionary<string, ServerRuntimeStatus>(StringComparer.OrdinalIgnoreCase);
        var backups = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            var status = await GetAsync<ServerRuntimeStatus>($"/api/profiles/{profile.ProfileId}/status", cancellationToken);
            if (status is not null)
            {
                statuses[profile.ProfileId] = status;
            }

            var backupList = await GetAsync<List<string>>($"/api/profiles/{profile.ProfileId}/backups", cancellationToken) ?? [];
            backups[profile.ProfileId] = backupList;
        }

        return new HostSnapshot(hostInfo, profiles, statuses, backups, jobs);
    }

    public async Task CreateStarterProfileAsync(CancellationToken cancellationToken = default)
    {
        var starter = ServerProfileFactory.CreateStarterProfile();
        var request = new ProfileUpsertRequestDto(
            starter.ProfileId,
            starter.DisplayName,
            starter.ServerName,
            starter.InstallDirectory,
            starter.CacheDirectory,
            starter.Branch,
            starter.DefaultPort,
            starter.UdpPort,
            starter.RconPort,
            starter.UseSteam,
            starter.AdminUsername,
            starter.AdminPassword,
            starter.BindIp,
            starter.PreferredMemoryInGigabytes,
            starter.StartWithHost,
            starter.AutoRestartOnCrash,
            starter.WorkshopPreset,
            starter.BackupPolicy);

        await PostAsync("/api/profiles", request, cancellationToken);
    }

    public Task<OperationResultDto?> InstallAsync(string profileId, CancellationToken cancellationToken = default) =>
        PostAsync<OperationResultDto>($"/api/profiles/{profileId}/install", null, cancellationToken);

    public Task<OperationResultDto?> UpdateAsync(string profileId, CancellationToken cancellationToken = default) =>
        PostAsync<OperationResultDto>($"/api/profiles/{profileId}/update", null, cancellationToken);

    public Task<OperationResultDto?> StartAsync(string profileId, CancellationToken cancellationToken = default) =>
        PostAsync<OperationResultDto>($"/api/profiles/{profileId}/start", null, cancellationToken);

    public Task<OperationResultDto?> StopAsync(string profileId, CancellationToken cancellationToken = default) =>
        PostAsync<OperationResultDto>($"/api/profiles/{profileId}/stop", null, cancellationToken);

    public Task<OperationResultDto?> RestartAsync(string profileId, CancellationToken cancellationToken = default) =>
        PostAsync<OperationResultDto>($"/api/profiles/{profileId}/restart", null, cancellationToken);

    public Task<OperationResultDto?> BackupAsync(string profileId, CancellationToken cancellationToken = default) =>
        PostAsync<OperationResultDto>($"/api/profiles/{profileId}/backup", null, cancellationToken);

    public Task<List<ProfileImportCandidateDto>?> DiscoverLocalImportsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<ProfileImportCandidateDto>>("/api/import/local", cancellationToken);

    public Task<ProfileDto?> ImportLocalCandidateAsync(string candidateId, CancellationToken cancellationToken = default) =>
        PostAsync<ProfileDto>($"/api/import/local/{candidateId}", null, cancellationToken);

    public Task<CommonConfigDto?> UpdateCommonConfigAsync(
        string profileId,
        CommonConfigDto config,
        CancellationToken cancellationToken = default) =>
        PutAsync<CommonConfigDto>($"/api/profiles/{profileId}/config/common", config, cancellationToken);

    public Task<WorkshopScanResultDto?> ScanWorkshopAsync(string profileId, CancellationToken cancellationToken = default) =>
        PostAsync<WorkshopScanResultDto>($"/api/profiles/{profileId}/workshop/scan", null, cancellationToken);

    public Task<RawConfigFileDto?> GetRawConfigAsync(
        string profileId,
        PZServerLauncher.Core.Runtime.ConfigFileKind kind,
        CancellationToken cancellationToken = default) =>
        GetAsync<RawConfigFileDto>($"/api/profiles/{profileId}/config/files/{kind}", cancellationToken);

    public Task<RawConfigFileDto?> SaveRawConfigAsync(
        string profileId,
        PZServerLauncher.Core.Runtime.ConfigFileKind kind,
        RawConfigFileDto payload,
        CancellationToken cancellationToken = default) =>
        PutAsync<RawConfigFileDto>($"/api/profiles/{profileId}/config/files/{kind}", payload, cancellationToken);

    public Task<OperationResultDto?> RestoreAsync(
        string profileId,
        string backupFileName,
        bool restartAfterRestore,
        CancellationToken cancellationToken = default) =>
        PostAsync<OperationResultDto>(
            $"/api/profiles/{profileId}/restore",
            new RestoreBackupRequestDto(backupFileName, restartAfterRestore),
            cancellationToken);

    public Task<OperationResultDto?> BootstrapOwnerAsync(
        string userName,
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        PostAsync<OperationResultDto>(
            "/api/onboarding/bootstrap",
            new BootstrapOwnerRequestDto(userName, email, password),
            cancellationToken);

    public Task<HostSettings?> GetHostSettingsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<HostSettings>("/api/settings/host", cancellationToken);

    public Task<HostSettings?> UpdateHostSettingsAsync(HostSettings settings, CancellationToken cancellationToken = default) =>
        PutAsync<HostSettings>("/api/settings/host", settings, cancellationToken);

    public Task<RemoteAccessSettingsDto?> GetRemoteAccessSettingsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<RemoteAccessSettingsDto>("/api/settings/remote-access", cancellationToken);

    public Task<RemoteAccessSettingsDto?> UpdateRemoteAccessSettingsAsync(
        RemoteAccessSettingsDto settings,
        CancellationToken cancellationToken = default) =>
        PutAsync<RemoteAccessSettingsDto>("/api/settings/remote-access", settings, cancellationToken);

    public Task<RemoteAccessSelfTestResultDto?> RunRemoteAccessSelfTestAsync(
        RemoteAccessSettingsDto settings,
        CancellationToken cancellationToken = default) =>
        PostAsync<RemoteAccessSelfTestResultDto>("/api/settings/remote-access/self-test", settings, cancellationToken);

    public Task<OperationResultDto?> ApplyRemoteFirewallRuleAsync(
        RemoteAccessSettingsDto settings,
        CancellationToken cancellationToken = default) =>
        PostAsync<OperationResultDto>("/api/settings/remote-access/firewall", settings, cancellationToken);

    public Task<OperationResultDto?> StopHostAsync(
        bool stopRunningServers,
        CancellationToken cancellationToken = default) =>
        PostAsync<OperationResultDto>("/api/host/stop", new HostShutdownRequestDto(stopRunningServers), cancellationToken);

    public async Task EnsureHostRunningAsync(CancellationToken cancellationToken = default)
    {
        if (await TryPingAsync(cancellationToken))
        {
            return;
        }

        var hostExecutable = FindHostExecutable();
        if (hostExecutable is null)
        {
            throw new FileNotFoundException("Could not locate PZServerLauncher.Host.exe. Build the host project before launching the desktop app.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = hostExecutable,
            WorkingDirectory = Path.GetDirectoryName(hostExecutable)!,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });

        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(12);
        while (DateTimeOffset.UtcNow < timeoutAt && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(500, cancellationToken);
            if (await TryPingAsync(cancellationToken))
            {
                return;
            }
        }

        throw new TimeoutException("The local host did not start within the expected time.");
    }

    public async Task<LoopbackConnectionInfo> GetLoopbackConnectionInfoAsync(CancellationToken cancellationToken = default)
    {
        await EnsureHostRunningAsync(cancellationToken);
        var state = await LoadStateAsync(cancellationToken);
        var protector = DataProtectionProvider.Create(_stateDirectory)
            .CreateProtector("PZServerLauncher.HostBootstrapState");
        var token = protector.Unprotect(state.ProtectedLocalApiToken);
        return new LoopbackConnectionInfo(new Uri($"http://127.0.0.1:{state.LoopbackPort}"), token);
    }

    private async Task<bool> TryPingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            using var client = CreateHttpClient(state);
            using var response = await client.GetAsync("/api/host/info", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        using var client = CreateHttpClient(state);
        return await client.GetFromJsonAsync<T>(path, JsonOptions, cancellationToken);
    }

    private async Task PostAsync(string path, object payload, CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        using var client = CreateHttpClient(state);
        using var response = await client.PostAsJsonAsync(path, payload, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<T?> PostAsync<T>(string path, object? payload, CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        using var client = CreateHttpClient(state);
        using var response = payload is null
            ? await client.PostAsync(path, content: null, cancellationToken)
            : await client.PostAsJsonAsync(path, payload, JsonOptions, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<T?> PutAsync<T>(string path, object payload, CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        using var client = CreateHttpClient(state);
        using var response = await client.PutAsJsonAsync(path, payload, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        try
        {
            var operation = await response.Content.ReadFromJsonAsync<OperationResultDto>(JsonOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(operation?.Message))
            {
                throw new HttpRequestException(operation.Message, null, response.StatusCode);
            }
        }
        catch (JsonException)
        {
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(body))
        {
            throw new HttpRequestException(body, null, response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
    }

    private async Task<LocalHostBootstrapState> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_stateFilePath))
        {
            return new LocalHostBootstrapState();
        }

        await using var stream = File.OpenRead(_stateFilePath);
        return await JsonSerializer.DeserializeAsync<LocalHostBootstrapState>(stream, JsonOptions, cancellationToken)
            ?? new LocalHostBootstrapState();
    }

    private HttpClient CreateHttpClient(LocalHostBootstrapState state)
    {
        var protector = DataProtectionProvider.Create(_stateDirectory)
            .CreateProtector("PZServerLauncher.HostBootstrapState");
        var token = protector.Unprotect(state.ProtectedLocalApiToken);

        var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{state.LoopbackPort}"),
            Timeout = TimeSpan.FromSeconds(25),
        };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string? FindHostExecutable()
    {
        var solutionRoot = FindSolutionRoot();
        if (solutionRoot is null)
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(solutionRoot, "src", "PZServerLauncher.Host", "bin", "Debug", "net10.0", "PZServerLauncher.Host.exe"),
            Path.Combine(solutionRoot, "src", "PZServerLauncher.Host", "bin", "Release", "net10.0", "PZServerLauncher.Host.exe"),
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? Directory.GetFiles(solutionRoot, "PZServerLauncher.Host.exe", SearchOption.AllDirectories)
                .FirstOrDefault(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PZServerLauncher.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
