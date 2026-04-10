using System.Diagnostics;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.Host.Services;

public sealed class WindowsFirewallRuleService
{
    private const string RuleName = "PZServerLauncher Remote HTTPS";

    public async Task<OperationResultDto> EnsureRemoteAccessRuleAsync(
        RemoteAccessSettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!OperatingSystem.IsWindows())
        {
            return new OperationResultDto(true, "Firewall rule management is only required on Windows.");
        }

        var hostExecutable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(hostExecutable) || !File.Exists(hostExecutable))
        {
            return new OperationResultDto(false, "Could not resolve the local host executable path for the firewall rule.");
        }

        var escapedRuleName = EscapeSingleQuotedPowerShellString(RuleName);
        var escapedProgram = EscapeSingleQuotedPowerShellString(hostExecutable);
        var script = $"""
            $ErrorActionPreference = 'Stop'
            $ruleName = '{escapedRuleName}'
            Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue | Out-Null
            New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort {settings.HttpsPort} -Program '{escapedProgram}' -Profile Domain,Private | Out-Null
            """;

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "`\"", StringComparison.Ordinal)}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process
        {
            StartInfo = startInfo,
        };
        process.Start();
        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode == 0)
        {
            return new OperationResultDto(true, $"Created or updated the Windows Firewall rule for TCP {settings.HttpsPort}.");
        }

        var detail = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
        if (detail.Contains("access is denied", StringComparison.OrdinalIgnoreCase))
        {
            return new OperationResultDto(false, "Windows Firewall rejected the rule update. Re-run the desktop app as administrator or add the inbound rule manually.");
        }

        return new OperationResultDto(
            false,
            $"Windows Firewall rule update failed: {detail.Trim()}");
    }

    private static string EscapeSingleQuotedPowerShellString(string input) =>
        input.Replace("'", "''", StringComparison.Ordinal);
}
