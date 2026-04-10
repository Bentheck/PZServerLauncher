using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProjectZomboidFleetAccessPostureSummary(
    int ProfileCount,
    int PublicProfileCount,
    int PrivateProfileCount,
    int OpenAccessCount,
    int PasswordGatedCount,
    int PvpEnabledCount,
    int VoiceEnabledCount,
    int SafetyEnabledCount,
    int PublicOpenCount,
    int PublicOpenWithoutSafetyCount,
    string AccessHeadline,
    string TrustHeadline,
    string CommunicationHeadline,
    string OperatorSummary,
    IReadOnlyList<string> Checklist);

public static class ProjectZomboidFleetAccessPostureSummaryBuilder
{
    public static ProjectZomboidFleetAccessPostureSummary Build(
        IReadOnlyCollection<ProjectZomboidProfilePostureSummary> postures,
        bool remoteAccessEnabled)
    {
        var profileCount = postures.Count;
        var publicCount = postures.Count(posture => posture.IsPubliclyListed);
        var privateCount = profileCount - publicCount;
        var openAccessCount = postures.Count(posture => posture.IsOpenAccess);
        var passwordGatedCount = profileCount - openAccessCount;
        var pvpCount = postures.Count(posture => posture.IsPvpEnabled);
        var voiceCount = postures.Count(posture => posture.IsVoiceEnabled);
        var safetyCount = postures.Count(posture => posture.IsSafetyEnabled);
        var publicOpenCount = postures.Count(posture => posture.IsPubliclyListed && posture.IsOpenAccess);
        var publicOpenWithoutSafetyCount = postures.Count(posture => posture.IsPubliclyListed && posture.IsOpenAccess && !posture.IsSafetyEnabled);

        if (profileCount == 0)
        {
            return new ProjectZomboidFleetAccessPostureSummary(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                "No fleet access posture is available until the first profile exists.",
                "Create or import a profile before the trust posture can be summarized.",
                remoteAccessEnabled ? "Remote access is enabled, but there are no profiles to administer yet." : "Remote access is disabled and no profiles exist yet.",
                "Create or import the first server profile, then revisit Dashboard to compare access, PvP, safety, and voice posture across the fleet.",
                ["Create or import the first server profile to unlock the fleet posture view."]);
        }

        var accessHeadline = $"{publicCount} public | {privateCount} private | {openAccessCount} open access | {passwordGatedCount} password-gated.";
        var trustHeadline = $"{pvpCount} PvP-enabled | {safetyCount} safety-enabled | {publicOpenCount} public+open | {publicOpenWithoutSafetyCount} public+open without safety.";
        var communicationHeadline = $"{voiceCount} profile(s) with voice enabled | remote web {(remoteAccessEnabled ? "enabled" : "disabled")}.";

        var operatorSummary = publicOpenWithoutSafetyCount > 0
            ? $"{publicOpenWithoutSafetyCount} public/open profile(s) are missing PvP safety. Review those profiles before the next busy session."
            : publicOpenCount > 0
                ? "At least one profile is both public and open. Make sure the broader trust posture is intentional before the next session."
                : openAccessCount > 0
                    ? "Open-access profiles exist, but none are publicly listed. Double-check the intended join path and moderation posture."
                    : "Fleet access posture looks deliberate. Use Profiles or Overview to refine the next server you plan to run.";

        var checklist = new List<string>();
        if (publicOpenWithoutSafetyCount > 0)
        {
            checklist.Add("Review the public/open profiles that still have PvP safety disabled.");
        }

        if (publicOpenCount > 0 && !remoteAccessEnabled)
        {
            checklist.Add("If these public/open profiles need shared moderation, consider enabling remote access for the operator team.");
        }

        if (voiceCount > 0)
        {
            checklist.Add("Check that the welcome text or server rules still match the fleet's voice-chat posture.");
        }

        if (checklist.Count == 0)
        {
            checklist.Add("Use Profiles to compare server rules and Network & Admin posture before the next branch rollout or community event.");
        }

        return new ProjectZomboidFleetAccessPostureSummary(
            profileCount,
            publicCount,
            privateCount,
            openAccessCount,
            passwordGatedCount,
            pvpCount,
            voiceCount,
            safetyCount,
            publicOpenCount,
            publicOpenWithoutSafetyCount,
            accessHeadline,
            trustHeadline,
            communicationHeadline,
            operatorSummary,
            checklist);
    }
}
