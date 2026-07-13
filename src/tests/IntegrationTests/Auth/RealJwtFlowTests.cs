using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

[Collection("real-jwt")]
public sealed class RealJwtFlowTests(RealJwtApiFactory factory)
{
    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt);

    [Fact]
    public async Task Register_login_bearer_acces_produits_200()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"jwt-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        // Register + confirmation d'email (requise au login)
        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, password, ct);

        // Login → real JWT
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(ct);
        auth.ShouldNotBeNull();
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();

        // GET /api/v1/products with valid Bearer token → 200
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var listResp = await authed.GetAsync("/api/v1/products", ct);
        listResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_products_sans_token_retourne_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/products", ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_sans_cookie_est_idempotent_204()
    {
        // /auth/logout est AllowAnonymous (adjudiqué) : la credential réelle du logout est le
        // cookie HttpOnly `rt` (possession), pas l'access token. Rationale : révocation
        // idempotente, suppression de cookie inoffensive, un access token absent/expiré ne doit
        // pas empêcher un utilisateur de se déconnecter (cf. AuthEndpoints.cs). SameSite=Strict
        // couvre déjà le logout-CSRF (nuisance seulement). Sans Authorization NI cookie `rt` :
        // rien à révoquer côté serveur → 204 quand même (idempotent), jamais 401.
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var resp = await client.PostAsync("/api/v1/auth/logout", content: null, ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
