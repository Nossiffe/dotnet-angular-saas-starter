using Dapper;
using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.RefreshToken;

public sealed class RefreshTokenHandler(
    IRefreshTokenStore refresh,
    IIdentityService identity,
    IDbConnectionFactory connections,
    ITokenService tokens) : ICommandHandler<RefreshTokenCommand, ErrorOr<AuthResult>>
{
    public async ValueTask<ErrorOr<AuthResult>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        // L'adhésion est vérifiée avant la rotation : un utilisateur retiré de l'organisation perd ses
        // sessions sur celle-ci, et seulement celles-ci. Sans session active (token inconnu, expiré ou
        // déjà révoqué), la rotation décide, y compris la détection de réutilisation.
        MembershipRow? membership = null;
        if (await refresh.FindActiveAsync(command.RefreshToken, cancellationToken) is { } session)
        {
            membership = await FindMembershipAsync(session.UserId, session.TenantId, cancellationToken);
            if (membership is null)
            {
                await refresh.RevokeTenantSessionsAsync(session.UserId, session.TenantId, cancellationToken);
                return AuthErrors.SessionExpired;
            }
        }

        var rotated = await refresh.ValidateAndRotateAsync(command.RefreshToken, cancellationToken);
        if (rotated is null || membership is null)
        {
            return AuthErrors.SessionExpired;
        }

        var user = await identity.FindByIdAsync(rotated.UserId, cancellationToken);
        if (user is null)
        {
            return AuthErrors.SessionExpired;
        }

        var roles = await identity.GetRolesAsync(rotated.UserId, cancellationToken);
        var (token, exp) = tokens.CreateAccessToken(
            rotated.UserId, user.Email, rotated.TenantId, roles, membership.Role);

        return new AuthResult(token, exp, rotated.NewRefreshToken, rotated.RefreshTokenDays);
    }

    private async Task<MembershipRow?> FindMembershipAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<MembershipRow>(new CommandDefinition(
            """
            SELECT "OrganizationId", "Role"
            FROM "OrganizationMemberships"
            WHERE "UserId" = @UserId AND "OrganizationId" = @TenantId
            """,
            new { UserId = userId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }
}
