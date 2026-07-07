using Microsoft.AspNetCore.Identity;

namespace EMS.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<int>
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Deactivated users keep their history but cannot log in or refresh tokens.</summary>
    public bool IsActive { get; set; } = true;
}
