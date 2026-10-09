using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

[Collection("api")]
public sealed class AuthSecurityTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Registering_an_email_already_taken_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        var first = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password }, ct);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password }, ct);
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"badpwd-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, password, ct);

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = "WrongPass!!00" }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // Rotation et détection de réutilisation : RefreshTokenStore. Le refresh token voyage dans le
    // cookie `rt` HttpOnly. Le client par défaut de WebApplicationFactory rejoue le cookie automatiquement pour le
    // login + le premier refresh légitime ; la présentation de l'ancien cookie révoqué doit en revanche
    // être manuelle (le cookie-jar automatique l'aurait déjà écrasé par la rotation), via un client sans
    // cookie-jar (`HandleCookies = false`) et un header `Cookie` explicite portant la valeur capturée.
    [Fact]
    public async Task Reusing_a_refresh_token_revokes_every_session()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthTestFlow.HttpsClient(factory);
        var email = $"reuse-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, password, ct);

        // Login → capture la valeur brute du cookie refresh initial (pour le rejouer plus bas).
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var originalRefreshToken = AuthTestFlow.ExtractRefreshCookieValue(login);

        // Premier refresh légitime (cookie rejoué automatiquement) → rotation (nouveau cookie, l'ancien révoqué)
        var refresh1 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { }, ct);
        refresh1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var currentRefreshToken = AuthTestFlow.ExtractRefreshCookieValue(refresh1);

        currentRefreshToken.ShouldNotBe(originalRefreshToken);

        // Re-présenter l'ancien cookie révoqué (client sans cookie-jar, header Cookie manuel)
        // → détection de réutilisation → révocation de tous les tokens actifs de l'utilisateur → 401
        var reuseAttempt = await AuthTestFlow.RawCookieClient(factory, originalRefreshToken)
            .PostAsJsonAsync("/api/v1/auth/refresh", new { }, ct);
        reuseAttempt.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Le cookie courant (issu de la rotation légale) doit aussi être révoqué
        // par la cascade de révocation famille déclenchée par la réutilisation
        var currentAfterRevocation = await AuthTestFlow.RawCookieClient(factory, currentRefreshToken)
            .PostAsJsonAsync("/api/v1/auth/refresh", new { }, ct);
        currentAfterRevocation.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
