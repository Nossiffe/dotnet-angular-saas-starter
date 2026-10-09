using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Identity;

namespace Saas.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    DummyPasswordCheck dummyPasswordCheck) : IIdentityService
{
    private static readonly IReadOnlyDictionary<string, string[]> _emptyErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    public async Task<CreateUserResult> CreateUserAsync(
        string email, string password, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code, StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.Description).ToArray(),
                    StringComparer.Ordinal);
            return new CreateUserResult(false, Guid.Empty, errors);
        }

        return new CreateUserResult(true, user.Id, _emptyErrors);
    }

    public async Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        return user is null ? null : new AppUser(user.Id, user.Email!, user.EmailConfirmed);
    }

    public async Task<AppUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? null : new AppUser(user.Id, user.Email!, user.EmailConfirmed);
    }

    public async Task<AppUser?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);

        // Sans compte, ou compte verrouillé, Identity répondrait sans calculer de hash PBKDF2
        // (~400 ms à 210 000 itérations) : la durée de la réponse révélerait quels e-mails ont un
        // compte. On calcule donc un hash de même coût dans tous les cas.
        if (user is null || await users.IsLockedOutAsync(user))
        {
            dummyPasswordCheck.Run(users.PasswordHasher, password);
            return null;
        }

        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        return result.Succeeded ? new AppUser(user.Id, user.Email!, user.EmailConfirmed) : null;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return [];
        }

        return (IReadOnlyList<string>)await users.GetRolesAsync(user);
    }

    public async Task<string?> GenerateEmailConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        return Encode(token);
    }

    public async Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        if (TryDecode(token) is not { } decoded)
        {
            return false;
        }

        var result = await users.ConfirmEmailAsync(user, decoded);
        return result.Succeeded;
    }

    public async Task<bool> ConfirmEmailDirectlyAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        // L'invité prouve la possession de l'email via le token d'invitation (lien tokenisé) → on confirme
        // directement, en générant puis consommant un token de confirmation en interne (jamais exposé).
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var result = await users.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    public async Task<(Guid UserId, string Token)?> GeneratePasswordResetTokenAsync(
        string email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return null; // email inconnu — le handler ne révèle rien (anti-énumération)
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        return (user.Id, Encode(token));
    }

    public async Task<bool> ResetPasswordAsync(
        Guid userId, string token, string newPassword, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        if (TryDecode(token) is not { } decoded)
        {
            return false;
        }

        var result = await users.ResetPasswordAsync(user, decoded, newPassword);
        return result.Succeeded;
    }

    // Les tokens Identity contiennent des caractères non-URL-safe → Base64Url pour le transport en lien email.
    private static string Encode(string token)
        => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    // Un token reçu d'un lien peut être tronqué ou forgé : un Base64Url invalide est un token
    // invalide (400), pas une erreur serveur.
    private static string? TryDecode(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
