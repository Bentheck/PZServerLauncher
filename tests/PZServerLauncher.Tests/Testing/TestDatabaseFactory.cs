using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Host.Data;

namespace PZServerLauncher.Tests.Testing;

internal static class TestDatabaseFactory
{
    public static ApplicationDbContext Create(string databasePath)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath};Cache=Shared")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        return dbContext;
    }
}
