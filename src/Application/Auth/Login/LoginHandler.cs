using Dapper;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.Login;

public sealed class LoginHandler(
    IIdentityService identity,
    IDbConnectionFactory connections,
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

        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<MembershipRow>(new CommandDefinition(
            """
            SELECT "OrganizationId", "Role"
            FROM "OrganizationMemberships"
            WHERE "UserId" = @UserId
            """,
            new { UserId = user.Id },
            cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        var membership = command.TenantId is { } requested
            && rows.Exists(m => m.OrganizationId == requested)
            ? rows.First(m => m.OrganizationId == requested)
            : rows[0];

        var tenantId = membership.OrganizationId;
        var tenantRole = membership.Role;

        var roles = await identity.GetRolesAsync(user.Id, cancellationToken);
        var (token, exp) = tokens.CreateAccessToken(user.Id, user.Email, tenantId, roles, tenantRole);
        var refreshToken = await refresh.IssueAsync(user.Id, tenantId, cancellationToken);

        return new AuthResult(token, exp, refreshToken);
    }
}
