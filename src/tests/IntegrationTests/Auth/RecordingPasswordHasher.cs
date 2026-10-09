using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Saas.Domain.Identity;

namespace Saas.IntegrationTests.Auth;

/// <summary>Hasher réel de l'application, qui note chaque mot de passe vérifié.</summary>
public sealed class RecordingPasswordHasher(ConcurrentQueue<string> verifiedPasswords, IOptions<PasswordHasherOptions> options)
    : PasswordHasher<ApplicationUser>(options)
{
    public override PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user, string hashedPassword, string providedPassword)
    {
        verifiedPasswords.Enqueue(providedPassword);
        return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
