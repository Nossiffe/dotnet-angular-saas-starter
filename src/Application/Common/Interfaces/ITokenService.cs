namespace Saas.Application.Common.Interfaces;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(
        Guid userId, string email, Guid tenantId, IEnumerable<string> roles);
}
