using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProjectZomboidRemoteAccessRolloutSummary(
    string ModeHeadline,
    string EndpointHeadline,
    string CertificateHeadline,
    string FirewallHeadline,
    string ReadinessHeadline,
    string OperatorSummary,
    string NextStepSummary,
    IReadOnlyList<ProjectZomboidOperatorChecklistItem> Checklist);

public static class ProjectZomboidRemoteAccessRolloutSummaryBuilder
{
    public static ProjectZomboidRemoteAccessRolloutSummary Build(
        RemoteAccessSettings settings,
        int ownerAccountsWithTwoFactor,
        IReadOnlyList<string> selfTestMessages)
    {
        var endpointHost = string.IsNullOrWhiteSpace(settings.PublicHostname)
            ? settings.BindAddress
            : settings.PublicHostname;
        var checklist = BuildChecklist(settings, ownerAccountsWithTwoFactor, selfTestMessages);

        return new ProjectZomboidRemoteAccessRolloutSummary(
            settings.IsEnabled ? "Remote HTTPS is staged for exposure." : "The host is still loopback-only.",
            settings.IsEnabled ? $"Planned endpoint: https://{endpointHost}:{settings.HttpsPort}" : "No remote endpoint is currently exposed.",
            string.IsNullOrWhiteSpace(settings.CertificatePath) ? "No PFX certificate path is selected yet." : $"Certificate path staged: {settings.CertificatePath}",
            settings.CreateFirewallRule ? "Firewall rule intent is enabled." : "Firewall changes are currently deferred.",
            ownerAccountsWithTwoFactor == 0
                ? "Owner 2FA is still missing, so remote exposure should stay off."
                : settings.IsEnabled
                    ? "Owner 2FA is ready; the remaining rollout concerns are certificate, bind address, and host restart."
                    : "Owner 2FA is ready if you later choose to expose the web admin.",
            BuildOperatorSummary(settings, ownerAccountsWithTwoFactor, checklist),
            BuildNextStepSummary(settings, ownerAccountsWithTwoFactor, checklist),
            checklist);
    }

    public static ProjectZomboidRemoteAccessRolloutSummary Empty() =>
        Build(new RemoteAccessSettings(), 0, Array.Empty<string>());

    private static string BuildOperatorSummary(
        RemoteAccessSettings settings,
        int ownerAccountsWithTwoFactor,
        IReadOnlyList<ProjectZomboidOperatorChecklistItem> checklist)
    {
        if (ownerAccountsWithTwoFactor == 0)
        {
            return "Finish owner TOTP enrollment locally before you trust any remote web sign-in.";
        }

        if (checklist.Any(item => item.IsBlocking))
        {
            return "Treat this as a rollout checklist, not a toggle. Clear every blocking item before you expose HTTPS beyond the local machine.";
        }

        if (settings.IsEnabled)
        {
            return settings.RequiresHostRestart
                ? "The listener is staged, but the host still needs a restart before the HTTPS binding becomes real."
                : "The listener is enabled. Keep firewall and router posture deliberately scoped to the audience you actually want.";
        }

        return "Remote access is intentionally off. Use this page to stage certificate and endpoint details before you decide whether exposure is actually needed.";
    }

    private static string BuildNextStepSummary(
        RemoteAccessSettings settings,
        int ownerAccountsWithTwoFactor,
        IReadOnlyList<ProjectZomboidOperatorChecklistItem> checklist)
    {
        if (ownerAccountsWithTwoFactor == 0)
        {
            return "Open the local authenticator setup first, then come back here once an owner account has TOTP enabled.";
        }

        if (checklist.Any(item => item.IsBlocking))
        {
            return "Fix the blocking rollout item first, rerun the local checks, and only then consider firewall or router changes.";
        }

        if (!settings.IsEnabled)
        {
            return "Keep the listener disabled if this machine is meant to stay desktop-only, or enable it only after the certificate and bind posture are ready.";
        }

        return settings.CreateFirewallRule
            ? "Restart the host, verify the local endpoint, and only then apply or review the inbound firewall rule."
            : "Restart the host, verify the local endpoint, and decide whether the listener should stay loopback-only or become reachable from the network.";
    }

    private static IReadOnlyList<ProjectZomboidOperatorChecklistItem> BuildChecklist(
        RemoteAccessSettings settings,
        int ownerAccountsWithTwoFactor,
        IReadOnlyList<string> selfTestMessages)
    {
        var checklist = new List<ProjectZomboidOperatorChecklistItem>
        {
            ownerAccountsWithTwoFactor == 0
                ? new ProjectZomboidOperatorChecklistItem("Blocking", "Finish owner TOTP enrollment before you trust remote sign-in.", true, false)
                : new ProjectZomboidOperatorChecklistItem("Healthy", "Owner TOTP readiness is in place for remote administration.", false, false),
            string.IsNullOrWhiteSpace(settings.CertificatePath)
                ? new ProjectZomboidOperatorChecklistItem("Blocking", "Choose a PFX certificate path so the host can validate HTTPS binding.", true, false)
                : new ProjectZomboidOperatorChecklistItem("Healthy", "A certificate path is staged for host validation.", false, false),
            settings.IsEnabled
                ? settings.RequiresHostRestart
                    ? new ProjectZomboidOperatorChecklistItem("Follow-up", $"Remote HTTPS is enabled for {settings.BindAddress}:{settings.HttpsPort}. Restart the host after saving.", false, true)
                    : new ProjectZomboidOperatorChecklistItem("Healthy", $"Remote HTTPS is enabled for {settings.BindAddress}:{settings.HttpsPort}.", false, false)
                : new ProjectZomboidOperatorChecklistItem("Healthy", "Remote HTTPS is still disabled, so the host remains local-only while you stage the rollout.", false, false),
            settings.CreateFirewallRule
                ? new ProjectZomboidOperatorChecklistItem("Follow-up", "Firewall intent is enabled. Apply or verify the inbound rule only after the local endpoint looks healthy.", false, true)
                : new ProjectZomboidOperatorChecklistItem("Follow-up", "Firewall intent is off. Manual inbound access will still be required if you expose the listener later.", false, true),
        };

        checklist.AddRange(selfTestMessages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => ParseSelfTestMessage(message.Trim())));

        return checklist
            .DistinctBy(item => $"{item.StatusLabel}|{item.Message}")
            .ToArray();
    }

    private static ProjectZomboidOperatorChecklistItem ParseSelfTestMessage(string message)
    {
        var normalized = message.ToLowerInvariant();
        if (normalized.Contains("failed", StringComparison.Ordinal) ||
            normalized.Contains("could not", StringComparison.Ordinal) ||
            normalized.Contains("is not", StringComparison.Ordinal) ||
            normalized.Contains("missing", StringComparison.Ordinal) ||
            normalized.Contains("invalid", StringComparison.Ordinal))
        {
            return new ProjectZomboidOperatorChecklistItem("Blocking", message, true, false);
        }

        if (normalized.Contains("manual", StringComparison.Ordinal) ||
            normalized.Contains("restart the host", StringComparison.Ordinal) ||
            normalized.Contains("requires administrative rights", StringComparison.Ordinal) ||
            normalized.Contains("firewall", StringComparison.Ordinal) ||
            normalized.Contains("port forward", StringComparison.Ordinal))
        {
            return new ProjectZomboidOperatorChecklistItem("Follow-up", message, false, true);
        }

        return new ProjectZomboidOperatorChecklistItem("Healthy", message, false, false);
    }
}
