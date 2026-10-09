using System.Net;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Platform;

// Les timeouts ne sont pas testables via une requête HTTP réelle sans endpoint lent dédié
// (et ils ne se déclenchent pas sous débogueur) — on vérifie donc le câblage
// par DI (policy résolue = config par défaut) plutôt que le comportement de bout en bout.
[Collection("api")]
public sealed class MiddlewareWiringTests(PostgresApiFactory factory)
{
    [Fact]
    public void Request_timeouts_use_the_configured_default_policy()
    {
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<RequestTimeoutOptions>>().Value;

        options.DefaultPolicy.ShouldNotBeNull();
        options.DefaultPolicy!.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task A_normal_request_succeeds_with_the_timeout_middleware()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
