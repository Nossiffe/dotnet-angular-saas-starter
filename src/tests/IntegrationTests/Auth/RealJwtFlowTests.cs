using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

[Collection("real-jwt")]
public sealed class RealJwtFlowTests(RealJwtApiFactory factory)
{
    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);

    [Fact]
    public async Task Register_login_bearer_acces_produits_200()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"jwt-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        // Register
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password }, ct);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

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
}
