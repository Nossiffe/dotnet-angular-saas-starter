namespace Saas.Api.Common;

/// <summary>
/// Transport du refresh token : cookie HttpOnly, jamais exposé au JS (anti-XSS).
/// SameSite=Strict + CORS origins explicites + fronts même-origine → pas d'antiforgery
/// (ADR 0004). Path limité aux endpoints auth : le cookie ne circule pas ailleurs.
/// </summary>
internal static class RefreshCookie
{
    internal const string Name = "rt";
    private const string CookiePath = "/api/v1/auth";

    internal static void Append(HttpResponse response, string rawToken, int days)
        => response.Cookies.Append(Name, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            MaxAge = TimeSpan.FromDays(days),
        });

    internal static void Delete(HttpResponse response)
        => response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
        });

    internal static string? Read(HttpRequest request)
        => request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : null;
}
