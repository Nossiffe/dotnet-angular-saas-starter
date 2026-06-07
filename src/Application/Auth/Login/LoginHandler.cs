using Mediator;
using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.Login;

public sealed class LoginHandler(
    IIdentityService identity,
    IAppDbContext db,
    ITokenService tokens,
    IRefreshTokenStore refresh) : ICommandHandler<LoginCommand, AuthResult?>
{
    public async ValueTask<AuthResult?> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await identity.FindByEmailAsync(command.Email, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var passwordOk = await identity.CheckPasswordAsync(user.Id, command.Password, cancellationToken);
        if (!passwordOk)
        {
            return null;
        }

        var memberships = await db.Memberships
            .Where(m => m.UserId == user.Id)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return null;
        }

        var membership = command.TenantId is { } requested
            && memberships.Exists(m => m.OrganizationId == requested)
            ? memberships.First(m => m.OrganizationId == requested)
            : memberships[0];

        var tenantId = membership.OrganizationId;
        var tenantRole = membership.Role;

        var roles = await identity.GetRolesAsync(user.Id, cancellationToken);
        var (token, exp) = tokens.CreateAccessToken(user.Id, user.Email, tenantId, roles, tenantRole);
        var refreshToken = await refresh.IssueAsync(user.Id, tenantId, cancellationToken);

        return new AuthResult(token, exp, refreshToken);
    }
}
