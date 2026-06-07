namespace Saas.Api.Contracts;

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);
