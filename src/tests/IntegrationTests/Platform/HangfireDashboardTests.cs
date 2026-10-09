using System.Net;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Platform;

// NonDevelopmentApiFactory force ASPNETCORE_ENVIRONMENT=Production (voir ce fichier pour le
// pourquoi) afin d'exercer la branche prod de HangfireDashboardAuthFilter : authentifié +
// claim platform_role=PlatformAdmin requis (jamais tenant_role=Owner — cf. ARCHITECTURE.md § Hangfire).
[Collection("hangfire")]
public sealed class HangfireDashboardTests(NonDevelopmentApiFactory factory)
{
    [Fact]
    public async Task Dashboard_refuse_un_utilisateur_authentifie_sans_platform_role()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/hangfire", TestContext.Current.CancellationToken);
        // Hors Development (host de test forcé en Production) : uniquement 401/403/404 attendu —
        // ensemble fermé plutôt que ">= 401", qui laisserait passer silencieusement un 500 (bug).
        new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound }
            .ShouldContain(resp.StatusCode);
    }

    [Fact]
    public async Task Dashboard_accepte_platform_role_PlatformAdmin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PlatformRoleHeader, "PlatformAdmin");
        var resp = await client.GetAsync("/hangfire", TestContext.Current.CancellationToken);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
