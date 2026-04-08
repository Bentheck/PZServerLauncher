using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var appPaths = new AppPaths();
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        builder.UseSqlite($"Data Source={appPaths.DatabasePath};Cache=Shared");
        return new ApplicationDbContext(builder.Options);
    }
}
