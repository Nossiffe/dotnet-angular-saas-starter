using Microsoft.AspNetCore.Identity;

namespace Saas.Domain.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
}
