using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProjectZomboidUserAccessSummary(
    string RosterHeadline,
    string SecurityHeadline,
    string OwnerHeadline,
    string RoleCoverageHeadline,
    string ReviewHeadline,
    string CreateRoleHeadline,
    string CreateRoleGuardrailHeadline,
    string OperatorSummary,
    string NextStepSummary,
    IReadOnlyList<ProjectZomboidOperatorChecklistItem> Checklist);

public static class ProjectZomboidUserAccessSummaryBuilder
{
    public static ProjectZomboidUserAccessSummary Build(
        bool ownerBootstrapConfigured,
        IReadOnlyCollection<UserAccountDto> users,
        string createRoleName)
    {
        var owners = users.Count(account => account.Roles.Contains(UserRole.Owner));
        var admins = users.Count(account => account.Roles.Contains(UserRole.Admin));
        var operators = users.Count(account => account.Roles.Contains(UserRole.Operator));
        var viewers = users.Count(account => account.Roles.Contains(UserRole.Viewer));
        var privileged = users.Count(account => account.Roles.Any(RoleIsPrivileged));

        return new ProjectZomboidUserAccessSummary(
            !ownerBootstrapConfigured
                ? "Owner bootstrap is still required."
                : users.Count == 0
                    ? "No managed accounts exist yet."
                    : $"{users.Count} managed account(s) | {owners} owner(s) | {admins} admin(s) | {operators} operator(s) | {viewers} viewer(s).",
            !ownerBootstrapConfigured
                ? "Create the owner account first, then return here for access management."
                : privileged == 0
                    ? "No privileged accounts exist yet."
                    : $"{privileged} privileged account(s) can change host or remote configuration.",
            !ownerBootstrapConfigured
                ? "Owner protection becomes visible after bootstrap."
                : owners == 0
                    ? "No owner accounts are visible. That should only happen briefly during bootstrap recovery."
                    : owners == 1
                        ? "One owner account protects the host."
                        : $"{owners} owner accounts exist. Keep at least one reserved for recovery.",
            !ownerBootstrapConfigured
                ? "Role coverage appears after the owner account is created."
                : $"{owners} owner | {admins} admin | {operators} operator | {viewers} viewer.",
            !ownerBootstrapConfigured
                ? "Security review is unavailable until bootstrap completes."
                : $"{privileged} privileged account(s) | {operators} operator(s) | {viewers} viewer(s).",
            BuildCreateRoleHeadline(createRoleName),
            BuildCreateRoleGuardrailHeadline(createRoleName),
            BuildOperatorSummary(ownerBootstrapConfigured, users.Count, owners, privileged),
            BuildNextStepSummary(ownerBootstrapConfigured, users.Count),
            BuildChecklist(ownerBootstrapConfigured, users.Count, owners, privileged, createRoleName));
    }

    public static ProjectZomboidUserAccessSummary Empty() =>
        Build(ownerBootstrapConfigured: false, Array.Empty<UserAccountDto>(), nameof(UserRole.Viewer));

    private static string BuildOperatorSummary(
        bool ownerBootstrapConfigured,
        int totalUsers,
        int owners,
        int privileged)
    {
        if (!ownerBootstrapConfigured)
        {
            return "Finish owner bootstrap first so the account manager can become a real local security console.";
        }

        if (totalUsers == 0)
        {
            return "No shared accounts exist yet. Keep the surface owner-only until you truly need more operators.";
        }

        if (owners > 1)
        {
            return "Multiple owner accounts exist. Keep at least one reserved for emergency recovery and challenge whether you really need more than one.";
        }

        if (privileged == totalUsers && totalUsers > 1)
        {
            return "Every account shown is privileged. Consider shrinking elevated access by moving lower-risk users into Operator or Viewer roles.";
        }

        return "The role mix is coherent. Keep elevated access narrow and review admin and owner assignments whenever you add or promote users.";
    }

    private static string BuildNextStepSummary(bool ownerBootstrapConfigured, int totalUsers)
    {
        if (!ownerBootstrapConfigured)
        {
            return "Create the owner account first, then come back here to add operators or admins.";
        }

        if (totalUsers == 0)
        {
            return "Create the first operator or viewer only if you really need shared administration.";
        }

        return "Review whether each admin and operator still needs the role they have today, and keep elevated access as small as possible.";
    }

    private static IReadOnlyList<ProjectZomboidOperatorChecklistItem> BuildChecklist(
        bool ownerBootstrapConfigured,
        int totalUsers,
        int owners,
        int privileged,
        string createRoleName)
    {
        var checklist = new List<ProjectZomboidOperatorChecklistItem>();

        if (!ownerBootstrapConfigured)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Blocking", "Finish owner bootstrap before you try to manage shared web-admin accounts.", true, false));
            return checklist;
        }

        if (owners == 1)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Healthy", "Exactly one owner account currently protects the host.", false, false));
        }
        else if (owners > 1)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Follow-up", "Multiple owner accounts exist. Confirm that each one is truly necessary for recovery.", false, true));
        }

        if (totalUsers == 0)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Follow-up", "Create the first operator or viewer only if shared administration is actually needed.", false, true));
        }

        if (privileged == totalUsers && totalUsers > 1)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Follow-up", "Every current account is privileged. Consider shifting lower-risk users into Operator or Viewer roles.", false, true));
        }

        checklist.Add(RoleIsPrivileged(createRoleName)
            ? new ProjectZomboidOperatorChecklistItem("Follow-up", "The selected create-role is privileged. Keep that role rare and assign it intentionally.", false, true)
            : new ProjectZomboidOperatorChecklistItem("Healthy", "The selected create-role is lower risk and keeps configuration power narrow.", false, false));

        if (checklist.Count == 0)
        {
            checklist.Add(new ProjectZomboidOperatorChecklistItem("Healthy", "Account posture is steady. Review roles again whenever you expose the optional remote surface to more people.", false, false));
        }

        return checklist;
    }

    private static string BuildCreateRoleHeadline(string createRoleName) =>
        createRoleName switch
        {
            nameof(UserRole.Owner) => "Owner is full control and should stay rare.",
            nameof(UserRole.Admin) => "Admin can manage configuration and remote access.",
            nameof(UserRole.Operator) => "Operator can handle lifecycle, backups, and day-to-day maintenance.",
            nameof(UserRole.Viewer) => "Viewer is read-only and safest for visibility-only access.",
            _ => "Custom role selection.",
        };

    private static string BuildCreateRoleGuardrailHeadline(string createRoleName) =>
        RoleIsPrivileged(createRoleName)
            ? "This is a privileged role. Assign it only when the user truly needs configuration or recovery control."
            : "This role is intentionally lower risk and better for visibility-first access.";

    private static bool RoleIsPrivileged(string roleName) =>
        string.Equals(roleName, nameof(UserRole.Owner), StringComparison.Ordinal) ||
        string.Equals(roleName, nameof(UserRole.Admin), StringComparison.Ordinal);

    private static bool RoleIsPrivileged(UserRole role) =>
        role is UserRole.Owner or UserRole.Admin;
}
