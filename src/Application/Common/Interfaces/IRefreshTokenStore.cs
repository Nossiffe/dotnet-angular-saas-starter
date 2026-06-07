namespace Saas.Application.Common.Interfaces;

public interface IRefreshTokenStore
{
    Task<string> IssueAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
    Task<RotatedToken?> ValidateAndRotateAsync(string rawToken, CancellationToken cancellationToken);
}
