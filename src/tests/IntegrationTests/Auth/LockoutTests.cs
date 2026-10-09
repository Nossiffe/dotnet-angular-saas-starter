using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

[Collection("api")]
public sealed class LockoutTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Five_failures_lock_the_account_even_for_the_right_password()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var email = $"lockout-{Guid.NewGuid():N}@saas.local";
        const string password = "Sup3rSecret!!42";

        await AuthTestFlow.RegisterAndConfirmAsync(client, factory, email, password, ct);

        for (var i = 0; i < 5; i++)
        {
            var bad = await client.PostAsJsonAsync(
                "/api/v1/auth/login", new { email, password = "Wrong-Password-123!" }, ct);
            bad.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // 6e tentative avec le bon mot de passe : compte verrouillé → toujours 401 (anti-énumération).
        var locked = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password }, ct);
        locked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void The_password_hasher_uses_210000_iterations()
    {
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<PasswordHasherOptions>>();
        options.Value.IterationCount.ShouldBeGreaterThanOrEqualTo(210_000);
    }
}
