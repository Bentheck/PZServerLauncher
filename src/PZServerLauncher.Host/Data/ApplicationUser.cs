using Microsoft.AspNetCore.Identity;

namespace PZServerLauncher.Host.Data;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}

