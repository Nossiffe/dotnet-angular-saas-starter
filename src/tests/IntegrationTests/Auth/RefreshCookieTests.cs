using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

/// <summary>
/// Contrat du transport du refresh token en cookie HttpOnly (cf. <see cref="Saas.Api.Common.RefreshCookie"/>) :
/// attributs du cookie posé au login, absence du refresh token dans le body JSON, refus sans cookie,
/// suppression du cookie au logout. Le mécanisme métier (rotation, détection de réutilisation) est couvert
/// par <see cref="AuthSecurityTests"/> — ce fichier ne teste que le contrat de transport.
/// </summary>
[Collection("api")]
public sealed class RefreshCookieTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Login_sets_an_HttpOnly_Secure_SameSite_Strict_refresh_cookie_on_the_auth_path()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthTestFlow.HttpsClient(factory);
        var email = $"cookie-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, password, ct);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var setCookie = response.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith("rt=", StringComparison.Ordinal));
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("secure", Case.Insensitive);
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
        setCookie.ShouldContain("path=/api/v1/auth", Case.Insensitive);

        var body = await response.Content.ReadAsStringAsync(ct);
        body.ShouldNotContain("refreshToken"); // le refresh token n'apparaît jamais dans le JSON
    }

    [Fact]
    public async Task Refresh_without_a_cookie_returns_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient(); // pas de login préalable → aucun cookie `rt`

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_deletes_the_cookie()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthTestFlow.HttpsClient(factory);
        var email = $"cookie-logout-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, password, ct);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { }, ct);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var deletedCookie = logout.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith("rt=", StringComparison.Ordinal));
        // Suppression via ExpireTime passé : ASP.NET Core encode un Set-Cookie vide + expires révolu.
        deletedCookie.ShouldContain("expires=", Case.Insensitive);
        var expiresValue = ParseExpires(deletedCookie);
        expiresValue.ShouldBeLessThan(DateTimeOffset.UtcNow);
        // Path/HttpOnly du Set-Cookie de suppression doivent correspondre exactement à ceux posés au
        // login (RefreshCookie.Delete) : un navigateur n'efface un cookie que si Path (et Domain)
        // correspondent au cookie original — un mismatch laisserait `rt` orphelin dans le jar.
        deletedCookie.ShouldContain("path=/api/v1/auth", Case.Insensitive);
        deletedCookie.ShouldContain("httponly", Case.Insensitive);
    }

    private static DateTimeOffset ParseExpires(string setCookieHeader)
    {
        var expiresPart = setCookieHeader
            .Split(';')
            .Select(p => p.Trim())
            .First(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        var raw = expiresPart["expires=".Length..];
        return DateTimeOffset.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
    }
}
