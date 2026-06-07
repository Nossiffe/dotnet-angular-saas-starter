using Mediator;
using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.RefreshToken;

public sealed class RefreshTokenHandler(
    IRefreshTokenStore refresh,
    IIdentityService identity,
    IAppDbContext db,
    ITokenService tokens) : ICommandHandler<RefreshTokenCommand, AuthResult?>
{
    public async ValueTask<AuthResult?> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rotated = await refresh.ValidateAndRotateAsync(command.RefreshToken, cancellationToken);
        if (rotated is null)
        {
            return null;
        }

        var user = await identity.FindByIdAsync(rotated.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var membership = await db.Memberships
            .FirstOrDefaultAsync(
                m => m.UserId == rotated.UserId && m.OrganizationId == rotated.TenantId,
                cancellationToken);

        var tenantRole = membership?.Role ?? "Member";
        var roles = await identity.GetRolesAsync(rotated.UserId, cancellationToken);
        var (token, exp) = tokens.CreateAccessToken(
            rotated.UserId, user.Email, rotated.TenantId, roles, tenantRole);

        return new AuthResult(token, exp, rotated.NewRefreshToken);
    }
}
