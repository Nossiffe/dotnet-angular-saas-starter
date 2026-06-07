using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Saas.IntegrationTests.Auth;

/// <summary>
/// Helpers de flux d'auth pour les tests d'intégration : register + confirmation d'email.
/// Depuis que le login exige un email confirmé, tout flux register→login doit passer par confirm-email
/// (le token de confirmation est capturé par le fake <see cref="CapturingEmailSender"/>).
/// </summary>
internal static class AuthTestFlow
{
    /// <summary>Inscrit un utilisateur PUIS confirme son email via le token capturé. L'user est alors connectable.</summary>
    public static async Task RegisterAndConfirmAsync(
        HttpClient client, PostgresApiFactory factory, string email, string password, CancellationToken cancellationToken)
    {
        var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new { email, password }, cancellationToken);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        await ConfirmEmailAsync(client, factory, email, cancellationToken);
    }

    /// <summary>Confirme l'email d'un utilisateur déjà inscrit via le lien capturé par le fake email sender.</summary>
    public static async Task ConfirmEmailAsync(
        HttpClient client, PostgresApiFactory factory, string email, CancellationToken cancellationToken)
    {
        var captured = factory.Emails.For(email);
        captured.ShouldNotBeNull();
        var (userId, token) = captured!.ExtractLink();

        var confirm = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email", new { userId, token }, cancellationToken);
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
