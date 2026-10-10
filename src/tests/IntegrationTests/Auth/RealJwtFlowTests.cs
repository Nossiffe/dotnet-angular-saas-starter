using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

[Collection("real-jwt")]
public sealed class RealJwtFlowTests(RealJwtApiFactory factory)
{
    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt);

    [Fact]
    public async Task Register_login_then_the_bearer_token_reads_products()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"jwt-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        // Register + confirmation d'email (requise au login)
        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, password, ct);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ct);
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(ct);
        auth.ShouldNotBeNull();
        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var listResp = await authed.GetAsync("/api/v1/products", ct);
        listResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // La route existe (vérifié dans les endpoints de l'application) : le 401 vient de l'absence de
    // token, et non d'une route inconnue, que la fallback policy refuserait aussi par un 401.
    [Theory]
    [InlineData("GET", "/api/v1/products", "/products")]
    [InlineData("GET", "/api/v1/organizations/{org}/members", "/organizations/{orgId:guid}/members")]
    public async Task Organization_routes_without_a_token_return_401(string method, string route, string pattern)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var path = route
            .Replace("{org}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)
            .Replace("{user}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var resp = await client.SendAsync(request, ct);

        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Any(e => (e.RoutePattern.RawText ?? "").TrimEnd('/').EndsWith(pattern, StringComparison.Ordinal)
                && e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase) == true)
            .ShouldBeTrue();
        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_without_a_cookie_returns_204()
    {
        // /auth/logout est AllowAnonymous : la credential réelle du logout est le
        // cookie HttpOnly `rt` (possession), pas l'access token : révocation
        // idempotente, suppression de cookie inoffensive, un access token absent/expiré ne doit
        // pas empêcher un utilisateur de se déconnecter (cf. AuthEndpoints.cs). SameSite=Strict
        // couvre déjà le logout-CSRF (nuisance seulement). Sans Authorization ni cookie `rt` :
        // rien à révoquer côté serveur → 204 quand même (idempotent), jamais 401.
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var resp = await client.PostAsync("/api/v1/auth/logout", content: null, ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
