using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var appPaths = new AppPaths();
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        builder.UseSqlite($"Data Source={appPaths.DatabasePath};Cache=Shared");
        builder.ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning));
        return new ApplicationDbContext(builder.Options);
    }
}
