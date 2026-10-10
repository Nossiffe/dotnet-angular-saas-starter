using Dapper;
using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Auth.Login;

public sealed class LoginHandler(
    IIdentityService identity,
    IDbConnectionFactory connections,
    ITokenService tokens,
    IRefreshTokenStore refresh) : ICommandHandler<LoginCommand, ErrorOr<AuthResult>>
{
    public async ValueTask<ErrorOr<AuthResult>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await identity.CheckCredentialsAsync(command.Email, command.Password, cancellationToken);
        if (user is null)
        {
            return AuthErrors.InvalidCredentials;
        }

        // Mot de passe valide, e-mail non confirmé : refus explicite (400). Le message ne révèle
        // l'existence du compte qu'à qui connaît déjà le bon mot de passe, et il aide l'utilisateur
        // légitime à comprendre pourquoi il ne peut pas se connecter.
        if (!user.EmailConfirmed)
        {
            return AuthErrors.EmailNotConfirmed;
        }

        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<MembershipRow>(new CommandDefinition(
            """
            SELECT "OrganizationId", "Role"
            FROM "OrganizationMemberships"
            WHERE "UserId" = @UserId
            ORDER BY "Id"
            """,
            new { UserId = user.Id },
            cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
        {
            return AuthErrors.InvalidCredentials;
        }

        // Sans organisation demandée (ou si l'utilisateur n'en est pas membre), la plus ancienne
        // adhésion : les Id sont des UUID v7, donc triés par date de création.
        var membership = rows.Find(m => m.OrganizationId == command.TenantId) ?? rows[0];

        var tenantId = membership.OrganizationId;
        var tenantRole = membership.Role;

        var roles = await identity.GetRolesAsync(user.Id, cancellationToken);
        var (token, exp) = tokens.CreateAccessToken(user.Id, user.Email, tenantId, roles, tenantRole);
        var (refreshToken, refreshDays) = await refresh.IssueAsync(user.Id, tenantId, cancellationToken);

        return new AuthResult(token, exp, refreshToken, refreshDays);
    }
}
