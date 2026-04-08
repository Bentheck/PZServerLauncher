using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Host.Infrastructure;

public sealed class HostBootstrapStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly AppPaths _appPaths;
    private readonly IDataProtector _protector;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HostBootstrapState? _current;

    public HostBootstrapStateStore(AppPaths appPaths)
    {
        _appPaths = appPaths;
        var dataProtectionProvider = DataProtectionProvider.Create(_appPaths.StateDirectory);
        _protector = dataProtectionProvider.CreateProtector("PZServerLauncher.HostBootstrapState");
    }

    public async Task<HostBootstrapState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_current is not null)
        {
            return _current;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_current is not null)
            {
                return _current;
            }

            _current = await LoadOrCreateCoreAsync(cancellationToken);
            return _current;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<HostBootstrapState> UpdateRemoteAccessAsync(
        RemoteAccessSettings settings,
        string? certificatePassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = _current ?? await LoadOrCreateCoreAsync(cancellationToken);
            var updated = current with
            {
                RemoteAccessEnabled = settings.IsEnabled,
                RemoteBindAddress = settings.BindAddress,
                RemoteHttpsPort = settings.HttpsPort,
                PublicHostname = settings.PublicHostname,
                CertificatePath = settings.CertificatePath,
                ProtectedCertificatePassword = string.IsNullOrWhiteSpace(certificatePassword)
                    ? current.ProtectedCertificatePassword
                    : Protect(certificatePassword),
                CreateFirewallRule = settings.CreateFirewallRule,
            };

            await SaveCoreAsync(updated, cancellationToken);
            _current = updated;
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetLocalApiTokenAsync(CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(cancellationToken);
        return Unprotect(state.ProtectedLocalApiToken);
    }

    public string? GetCertificatePassword(HostBootstrapState state) =>
        string.IsNullOrWhiteSpace(state.ProtectedCertificatePassword)
            ? null
            : Unprotect(state.ProtectedCertificatePassword);

    public static bool IsLoopback(IPAddress? address) =>
        address is not null && IPAddress.IsLoopback(address);

    private async Task<HostBootstrapState> LoadOrCreateCoreAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_appPaths.HostStatePath))
        {
            await using var readStream = File.OpenRead(_appPaths.HostStatePath);
            var existing = await JsonSerializer.DeserializeAsync<HostBootstrapState>(readStream, SerializerOptions, cancellationToken);
            if (existing is not null && !string.IsNullOrWhiteSpace(existing.ProtectedLocalApiToken))
            {
                return existing;
            }
        }

        var created = new HostBootstrapState
        {
            LoopbackPort = FindAvailableLoopbackPort(),
            ProtectedLocalApiToken = Protect(Guid.NewGuid().ToString("N")),
        };

        await SaveCoreAsync(created, cancellationToken);
        return created;
    }

    private async Task SaveCoreAsync(HostBootstrapState state, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_appPaths.HostStatePath);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
    }

    private static int FindAvailableLoopbackPort()
    {
        for (var port = ProjectZomboidDefaults.DefaultLoopbackPort; port < ProjectZomboidDefaults.DefaultLoopbackPort + 9; port++)
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                return port;
            }
            catch (SocketException)
            {
            }
        }

        return ProjectZomboidDefaults.DefaultLoopbackPort;
    }

    private string Protect(string plaintext)
    {
        return _protector.Protect(plaintext);
    }

    private string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
