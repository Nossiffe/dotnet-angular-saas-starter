using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Saas.Api.Hosting;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Api;

// Les tests d'intégration remplacent les limites (PostgresApiFactory) : la limite globale est vérifiée ici.
public sealed class RateLimitingTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddApiRateLimiting().BuildServiceProvider();
    private readonly RateLimiterOptions _options;

    public RateLimitingTests() => _options = _services.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

    public void Dispose()
    {
        _options.GlobalLimiter?.Dispose();
        _services.Dispose();
    }

    [Fact]
    public void A_refused_request_gets_a_429() =>
        _options.RejectionStatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);

    [Fact]
    public void An_anonymous_client_gets_100_requests_a_minute_per_IP_address()
    {
        var limiter = _options.GlobalLimiter.ShouldNotBeNull();
        var client = Request(ip: "203.0.113.7");

        for (var i = 0; i < 100; i++)
        {
            limiter.AttemptAcquire(client).IsAcquired.ShouldBeTrue();
        }

        limiter.AttemptAcquire(client).IsAcquired.ShouldBeFalse();
        limiter.AttemptAcquire(Request(ip: "203.0.113.8")).IsAcquired.ShouldBeTrue();
    }

    // Derrière une même IP (un bureau, un proxy), chaque utilisateur connecté a sa propre limite.
    [Fact]
    public void A_signed_in_user_is_limited_on_their_own_whatever_the_IP_address()
    {
        var limiter = _options.GlobalLimiter.ShouldNotBeNull();

        for (var i = 0; i < 100; i++)
        {
            limiter.AttemptAcquire(Request(ip: "203.0.113.9", user: "alice")).IsAcquired.ShouldBeTrue();
        }

        limiter.AttemptAcquire(Request(ip: "198.51.100.1", user: "alice")).IsAcquired.ShouldBeFalse();
        limiter.AttemptAcquire(Request(ip: "203.0.113.9", user: "bob")).IsAcquired.ShouldBeTrue();
        limiter.AttemptAcquire(Request(ip: "203.0.113.9")).IsAcquired.ShouldBeTrue();
    }

    [Fact]
    public void Loopback_calls_are_not_limited()
    {
        var limiter = _options.GlobalLimiter.ShouldNotBeNull();
        var local = Request(ip: IPAddress.Loopback.ToString());

        for (var i = 0; i < 150; i++)
        {
            limiter.AttemptAcquire(local).IsAcquired.ShouldBeTrue();
        }
    }

    private static DefaultHttpContext Request(string ip, string? user = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (user is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, user)], "Test"));
        }

        return context;
    }
}
