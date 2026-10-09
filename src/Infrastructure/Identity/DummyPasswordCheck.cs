using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Saas.Domain.Identity;

namespace Saas.Infrastructure.Identity;

/// <summary>
/// Vérifie un mot de passe contre un hash factice de même coût qu'un vrai (PBKDF2, mêmes options).
/// Utilisé quand il n'y a pas de hash à vérifier (compte inconnu ou verrouillé), pour que la durée
/// d'une connexion ne révèle pas l'existence du compte.
/// </summary>
internal sealed class DummyPasswordCheck
{
    private readonly PasswordHasher<ApplicationUser> _hasher;
    private readonly ApplicationUser _user = new();
    private readonly string _hash;

    public DummyPasswordCheck(IOptions<PasswordHasherOptions> options)
    {
        _hasher = new PasswordHasher<ApplicationUser>(options);
        _hash = _hasher.HashPassword(_user, Guid.NewGuid().ToString());
    }

    public void Run(string password) => _hasher.VerifyHashedPassword(_user, _hash, password);
}
