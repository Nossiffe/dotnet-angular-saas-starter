using Dapper;
using FluentValidation;
using FluentValidation.Results;
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

        // Mot de passe valide mais email non confirmé → refus explicite (400) avec un message clair.
        // Le mot de passe ayant déjà été validé, l'existence du compte n'est pas un secret supplémentaire
        // (le login révèle déjà la validité via le mauvais mot de passe) — message borné à « email non confirmé ».
        if (!user.EmailConfirmed)
        {
            throw new ValidationException(
                [new ValidationFailure("email", "Veuillez confirmer votre adresse email avant de vous connecter.")]);
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
