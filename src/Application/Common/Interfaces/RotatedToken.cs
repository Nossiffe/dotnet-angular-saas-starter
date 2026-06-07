namespace Saas.Application.Common.Interfaces;

public sealed record RotatedToken(Guid UserId, Guid TenantId, string NewRefreshToken);
