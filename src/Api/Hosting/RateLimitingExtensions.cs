using System.Net;
using System.Threading.RateLimiting;

namespace Saas.Api.Hosting;

/// <summary>Limitation de débit : globale par utilisateur ou par IP, plus stricte sur l'authentification.</summary>
public static class RateLimitingExtensions
{
    /// <summary>Politique des endpoints d'authentification : 10 requêtes par 30 secondes et par IP.</summary>
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(GlobalPartition);
            options.AddPolicy(AuthPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromSeconds(30), QueueLimit = 0 }));
        });

    // 100 requêtes par minute, par utilisateur connecté ou sinon par IP ; aucune limite en loopback.
    private static RateLimitPartition<string> GlobalPartition(HttpContext http)
    {
        if (http.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip))
        {
            return RateLimitPartition.GetNoLimiter("loopback");
        }

        var key = http.User.Identity?.IsAuthenticated == true
            ? $"user:{http.User.Identity.Name}"
            : $"ip:{http.Connection.RemoteIpAddress}";
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = 0,
        });
    }
}
