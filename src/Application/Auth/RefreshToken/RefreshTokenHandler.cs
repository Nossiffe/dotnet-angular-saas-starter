using Dapper;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.RefreshToken;

public sealed class RefreshTokenHandler(
    IRefreshTokenStore refresh,
    IIdentityService identity,
    IDbConnectionFactory connections,
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

        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var membership = await connection.QuerySingleOrDefaultAsync<MembershipRow>(new CommandDefinition(
            """
            SELECT "OrganizationId", "Role"
            FROM "OrganizationMemberships"
            WHERE "UserId" = @UserId AND "OrganizationId" = @TenantId
            """,
            new { rotated.UserId, rotated.TenantId },
            cancellationToken: cancellationToken));

        var tenantRole = membership?.Role ?? "Member";
        var roles = await identity.GetRolesAsync(rotated.UserId, cancellationToken);
        var (token, exp) = tokens.CreateAccessToken(
            rotated.UserId, user.Email, rotated.TenantId, roles, tenantRole);

        return new AuthResult(token, exp, rotated.NewRefreshToken);
    }
}
