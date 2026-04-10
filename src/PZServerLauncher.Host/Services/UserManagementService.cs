using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data;

namespace PZServerLauncher.Host.Services;

public sealed class UserManagementService(UserManager<ApplicationUser> userManager)
{
    private static readonly IReadOnlyList<UserRole> ManagedRoles =
    [
        UserRole.Owner,
        UserRole.Admin,
        UserRole.Operator,
        UserRole.Viewer,
    ];

    public async Task<IReadOnlyList<UserAccountDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var users = await userManager.Users.OrderBy(x => x.UserName).ToListAsync(cancellationToken);
        var results = new List<UserAccountDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await GetRolesAsync(user);
            results.Add(user.ToDto(roles));
        }

        return results;
    }

    public async Task<UserAccountDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roles = SanitizeRoles(request.Roles);
        var normalizedUserName = request.UserName.Trim();
        var user = new ApplicationUser
        {
            UserName = normalizedUserName,
            Email = null,
            NormalizedEmail = null,
            DisplayName = normalizedUserName,
            LockoutEnabled = true,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        EnsureSucceeded(createResult);

        var roleResult = await userManager.AddToRolesAsync(user, roles.Select(role => role.ToString()));
        EnsureSucceeded(roleResult);
        return user.ToDto(roles);
    }

    public async Task<UserAccountDto> UpdateAsync(
        string userId,
        UpdateUserRequestDto request,
        string? actingUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        var desiredRoles = SanitizeRoles(request.Roles);
        var existingRoles = await GetRolesAsync(user);
        var normalizedUserName = request.UserName.Trim();

        await EnsureOwnerWillRemainAsync(user, existingRoles, desiredRoles, actingUserId);

        user.UserName = normalizedUserName;
        user.Email = null;
        user.NormalizedEmail = null;
        user.DisplayName = normalizedUserName;
        user.LockoutEnabled = true;

        var updateResult = await userManager.UpdateAsync(user);
        EnsureSucceeded(updateResult);

        var rolesToRemove = existingRoles.Except(desiredRoles).Select(role => role.ToString()).ToArray();
        if (rolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            EnsureSucceeded(removeResult);
        }

        var rolesToAdd = desiredRoles.Except(existingRoles).Select(role => role.ToString()).ToArray();
        if (rolesToAdd.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            EnsureSucceeded(addResult);
        }

        return user.ToDto(desiredRoles);
    }

    public async Task DeleteAsync(string userId, string? actingUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(actingUserId) &&
            string.Equals(userId, actingUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("You cannot delete the account that is currently signed in.");
        }

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");
        var existingRoles = await GetRolesAsync(user);
        await EnsureOwnerWillRemainAsync(user, existingRoles, [], actingUserId);

        var deleteResult = await userManager.DeleteAsync(user);
        EnsureSucceeded(deleteResult);
    }

    public async Task ResetPasswordAsync(
        string userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new InvalidOperationException("A new password is required.");
        }

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

        IdentityResult result;
        if (await userManager.HasPasswordAsync(user))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            result = await userManager.ResetPasswordAsync(user, resetToken, newPassword);
        }
        else
        {
            result = await userManager.AddPasswordAsync(user, newPassword);
        }

        EnsureSucceeded(result);
    }

    public async Task EnsureRemoteAccessReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owners = await userManager.GetUsersInRoleAsync(UserRole.Owner.ToString());
        if (owners.Count == 0)
        {
            throw new InvalidOperationException("Create an owner account before enabling remote access.");
        }

        if (!owners.Any(user => user.TwoFactorEnabled))
        {
            throw new InvalidOperationException("Enable two-factor authentication for an owner account before enabling remote access. Open the local host and finish setup at /Account/Manage/EnableAuthenticator.");
        }
    }

    public static bool RoleRequiresTwoFactor(UserRole role) =>
        role is UserRole.Owner or UserRole.Admin;

    public static bool RolesRequireTwoFactor(IEnumerable<UserRole> roles) =>
        roles.Any(RoleRequiresTwoFactor);

    private async Task<IReadOnlyList<UserRole>> GetRolesAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return roles
            .Select(role => Enum.TryParse<UserRole>(role, out var parsed) ? parsed : (UserRole?)null)
            .Where(role => role.HasValue && role.Value != UserRole.LocalSystem)
            .Select(role => role!.Value)
            .Distinct()
            .OrderBy(role => role)
            .ToArray();
    }

    private async Task EnsureOwnerWillRemainAsync(
        ApplicationUser targetUser,
        IReadOnlyList<UserRole> existingRoles,
        IReadOnlyList<UserRole> desiredRoles,
        string? actingUserId)
    {
        var removingOwner = existingRoles.Contains(UserRole.Owner) && !desiredRoles.Contains(UserRole.Owner);
        if (!removingOwner)
        {
            return;
        }

        var owners = await userManager.GetUsersInRoleAsync(UserRole.Owner.ToString());
        if (owners.Count <= 1)
        {
            throw new InvalidOperationException("At least one owner account must remain.");
        }

        if (!string.IsNullOrWhiteSpace(actingUserId) &&
            string.Equals(targetUser.Id, actingUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("You cannot remove the owner role from the account that is currently signed in.");
        }
    }

    private static IReadOnlyList<UserRole> SanitizeRoles(IEnumerable<UserRole> roles)
    {
        var sanitized = roles
            .Where(role => ManagedRoles.Contains(role))
            .Distinct()
            .OrderBy(role => role)
            .ToArray();
        if (sanitized.Length == 0)
        {
            throw new InvalidOperationException("At least one application role is required.");
        }

        return sanitized;
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
