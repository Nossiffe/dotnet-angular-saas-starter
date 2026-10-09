using Microsoft.AspNetCore.Identity;
using Saas.Domain.Identity;

namespace Saas.Infrastructure.Identity;

/// <summary>
/// Vérifie un mot de passe contre un hash factice, calculé une fois par le hasher de l'application :
/// même algorithme et même coût qu'un vrai hash. Utilisé quand il n'y a pas de hash à vérifier
/// (compte inconnu ou verrouillé), pour que la durée d'une connexion ne révèle pas si le compte existe.
/// </summary>
internal sealed class DummyPasswordCheck
{
    private readonly ApplicationUser _user = new();
    private string? _hash;

    public void Run(IPasswordHasher<ApplicationUser> hasher, string password)
    {
        var hash = LazyInitializer.EnsureInitialized(
            ref _hash, () => hasher.HashPassword(_user, Guid.NewGuid().ToString()));
        hasher.VerifyHashedPassword(_user, hash, password);
    }
}
