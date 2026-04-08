using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class UserManagementServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;
    private readonly ServiceProvider _serviceProvider;

    public UserManagementServiceTests()
    {
        _databasePath = Path.Combine(_tempRoot, "users.db");
        Directory.CreateDirectory(_tempRoot);

        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite($"Data Source={_databasePath};Cache=Shared"));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<UserManagementService>();

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { UserRole.Owner, UserRole.Admin, UserRole.Operator, UserRole.Viewer })
        {
            var roleName = role.ToString();
            if (!roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
            {
                roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
            }
        }
    }

    [Fact]
    public async Task CreateAsync_AssignsRolesAndEnablesLockout()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<UserManagementService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var created = await service.CreateAsync(new CreateUserRequestDto(
            "operator1",
            "operator1@example.com",
            "StrongPassword!123",
            [UserRole.Operator]));

        var persisted = await userManager.FindByIdAsync(created.UserId);

        Assert.NotNull(persisted);
        Assert.True(persisted!.LockoutEnabled);
        Assert.Contains(UserRole.Operator, created.Roles);
    }

    [Fact]
    public async Task EnsureRemoteAccessReadyAsync_RequiresOwnerWithTwoFactor()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<UserManagementService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var owner = await service.CreateAsync(new CreateUserRequestDto(
            "owner1",
            "owner1@example.com",
            "StrongPassword!123",
            [UserRole.Owner]));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureRemoteAccessReadyAsync());
        Assert.Contains("two-factor", ex.Message, StringComparison.OrdinalIgnoreCase);

        var persisted = await userManager.FindByIdAsync(owner.UserId);
        await userManager.SetTwoFactorEnabledAsync(persisted!, true);

        await service.EnsureRemoteAccessReadyAsync();
    }

    [Fact]
    public async Task UpdateAsync_RejectsRemovingLastOwner()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<UserManagementService>();

        var owner = await service.CreateAsync(new CreateUserRequestDto(
            "owner2",
            "owner2@example.com",
            "StrongPassword!123",
            [UserRole.Owner]));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(
            owner.UserId,
            new UpdateUserRequestDto(owner.UserName, owner.Email ?? "owner2@example.com", [UserRole.Viewer]),
            actingUserId: "another-user"));

        Assert.Contains("owner", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_RejectsDeletingCurrentlySignedInUser()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<UserManagementService>();

        var viewer = await service.CreateAsync(new CreateUserRequestDto(
            "viewer1",
            "viewer1@example.com",
            "StrongPassword!123",
            [UserRole.Viewer]));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(viewer.UserId, viewer.UserId));
        Assert.Contains("currently signed in", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        SqliteConnection.ClearAllPools();

        if (!Directory.Exists(_tempRoot))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
        }
    }
}
