using Microsoft.AspNetCore.Identity;

namespace Saas.Infrastructure.Identity;

// Compte ASP.NET Core Identity. Il vit dans l'Infrastructure : le Domain ne connaît que l'identifiant
// de l'utilisateur (Guid), jamais Identity.
public sealed class ApplicationUser : IdentityUser<Guid>;
