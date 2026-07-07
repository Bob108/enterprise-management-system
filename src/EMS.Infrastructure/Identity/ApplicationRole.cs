using Microsoft.AspNetCore.Identity;

namespace EMS.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<int>
{
    public string? Description { get; set; }

    public ApplicationRole()
    {
    }

    public ApplicationRole(string name, string? description = null) : base(name)
    {
        Description = description;
    }
}
