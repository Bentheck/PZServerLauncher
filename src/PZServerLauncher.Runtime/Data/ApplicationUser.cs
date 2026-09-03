using Microsoft.AspNetCore.Identity;

namespace PZServerLauncher.Runtime.Data;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}

