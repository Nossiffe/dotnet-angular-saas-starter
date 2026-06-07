namespace Saas.Application.Auth;

public sealed record AuthResult(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);
