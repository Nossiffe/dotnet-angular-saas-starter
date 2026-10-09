using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Identity;

/// <summary>
/// Génère/valide les tokens d'invitation : raw 256-bit base64url envoyé dans le lien,
/// hash SHA-256 (hex minuscule) stocké en base. Stateless → enregistré en Singleton.
/// </summary>
public sealed class InvitationTokenService : IInvitationTokenService
{
    public string GenerateRawToken()
        => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string HashToken(string rawToken) => TokenHash.Sha256(rawToken);

    public bool VerifyToken(string rawToken, string storedHash)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(HashToken(rawToken)),
            Encoding.UTF8.GetBytes(storedHash));
}
