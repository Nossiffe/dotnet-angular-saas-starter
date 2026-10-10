using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace Saas.IntegrationTests.Auth;

/// <summary>
/// Helpers de flux d'auth pour les tests d'intégration : register + confirmation d'email + transport
/// du refresh token par cookie HttpOnly/Secure (cf. <see cref="Saas.Api.Common.RefreshCookie"/>).
/// La connexion exige un e-mail confirmé : un flux inscription → connexion passe par confirm-email,
/// avec le token capturé par le fake <see cref="CapturingEmailSender"/>.
/// </summary>
internal static class AuthTestFlow
{
    private const string RefreshCookieName = "rt";

    /// <summary>Inscrit un utilisateur puis confirme son email via le token capturé. L'user est alors connectable.</summary>
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

    /// <summary>
    /// Client HTTP en scheme HTTPS : requis pour que le <see cref="System.Net.CookieContainer"/> du
    /// <see cref="HttpClient"/> rejoue automatiquement le cookie refresh (Secure=true) sur les requêtes
    /// suivantes. Le TestServer ignore le scheme réel (aucune vraie négociation TLS) — seul le
    /// CookieContainer côté client s'appuie dessus pour décider d'envoyer un cookie Secure.
    /// </summary>
    public static HttpClient HttpsClient(PostgresApiFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    /// <summary>
    /// Client sans cookie-jar (`HandleCookies = false`) qui rejoue manuellement une valeur de cookie `rt`
    /// donnée via un header `Cookie` explicite — utilisé pour présenter une valeur ancienne ou révoquée que
    /// le cookie-jar automatique aurait de toute façon écrasée par la rotation.
    /// </summary>
    public static HttpClient RawCookieClient(PostgresApiFactory factory, string rawRefreshToken)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieName}={rawRefreshToken}");
        return client;
    }

    /// <summary>Extrait la valeur brute du cookie `rt=...` posé par un `Set-Cookie` de réponse.</summary>
    public static string ExtractRefreshCookieValue(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal));
        var withoutName = setCookie[(RefreshCookieName.Length + 1)..];
        var separatorIndex = withoutName.IndexOf(';');
        return separatorIndex < 0 ? withoutName : withoutName[..separatorIndex];
    }
}
