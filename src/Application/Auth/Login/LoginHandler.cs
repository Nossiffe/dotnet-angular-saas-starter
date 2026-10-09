using Dapper;
using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
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
        ArgumentNullException.ThrowIfNull(command);

        var user = await identity.CheckCredentialsAsync(command.Email, command.Password, cancellationToken);
        if (user is null)
        {
            return AuthErrors.InvalidCredentials;
        }

        // Mot de passe valide mais email non confirmé → refus explicite (400) avec un message clair.
        // Le mot de passe ayant déjà été validé, l'existence du compte n'est pas un secret supplémentaire
        // (le login révèle déjà la validité via le mauvais mot de passe) — message borné à « email non confirmé ».
        //
        // Tradeoff 400 vs 401 — décision délibérée, acceptable :
        // Renvoyer 400 ici (au lieu de 401) constitue un micro-oracle d'existence du compte, mais
        // uniquement pour un attaquant qui connaît déjà le mot de passe valide. Ce gating par le
        // mot de passe rend l'information marginalement plus sensible qu'un 401 générique, mais le
        // risque est jugé acceptable : la confirmation d'email est une friction volontaire (UX),
        // et le message aide l'utilisateur légitime à diagnostiquer son problème.
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
