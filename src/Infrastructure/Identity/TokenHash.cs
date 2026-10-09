using System.Security.Cryptography;
using System.Text;

namespace Saas.Infrastructure.Identity;

// Empreinte stockée d'un token aléatoire (refresh token, invitation) : SHA-256 en hexadécimal
// minuscule. Le token brut n'est jamais enregistré.
internal static class TokenHash
{
    public static string Sha256(string rawToken)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
