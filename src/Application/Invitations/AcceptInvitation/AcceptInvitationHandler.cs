using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.AcceptInvitation;

public sealed class AcceptInvitationHandler(
    IInvitationRepository invitations,
    IOrganizationRepository organizations,
    IIdentityService identity,
    IUnitOfWork uow,
    IInvitationTokenService tokens,
    IDateTimeProvider clock)
    : ICommandHandler<AcceptInvitationCommand, AcceptInvitationResult>
{
    // Message neutre : ne révèle pas si l'invitation existe, est expirée, révoquée ou si le token est faux
    // (l'endpoint est anonyme — pas d'oracle d'énumération).
    private static ValidationException InvalidInvitation() =>
        new([new ValidationFailure("Token", "Invitation invalide ou expirée.")]);

    public async ValueTask<AcceptInvitationResult> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invitation = await invitations.GetByIdAsync(InvitationId.From(command.InvitationId), cancellationToken)
            ?? throw InvalidInvitation();

        if (!tokens.VerifyToken(command.Token, invitation.TokenHash))
        {
            throw InvalidInvitation();
        }

        var existingUser = await identity.FindByEmailAsync(invitation.InviteeEmail, cancellationToken);
        var isNewUser = existingUser is null;

        await uow.ExecuteInTransactionAsync(async ct =>
        {
            Guid userId;
            if (existingUser is null)
            {
                if (string.IsNullOrEmpty(command.Password))
                {
                    throw new ValidationException(
                        [new ValidationFailure("Password", "Un mot de passe est requis pour finaliser la création du compte.")]);
                }

                var result = await identity.CreateUserAsync(invitation.InviteeEmail, command.Password, ct);
                if (!result.Succeeded)
                {
                    var failures = result.Errors
                        .SelectMany(kv => kv.Value.Select(v => new ValidationFailure(kv.Key, v)));
                    throw new ValidationException(failures);
                }

                userId = result.UserId;
                var emailConfirmed = await identity.ConfirmEmailDirectlyAsync(userId, ct);
                if (!emailConfirmed)
                {
                    throw new InvalidOperationException("Échec de la confirmation de l'email du nouvel utilisateur.");
                }
            }
            else
            {
                userId = existingUser.Id;
            }

            var organization = await organizations.GetByIdAsync(invitation.OrganizationId, ct)
                ?? throw new NotFoundException(nameof(Organization), invitation.OrganizationId.Value);

            invitation.Accept(clock.UtcNow);
            organization.AddMember(userId, invitation.Role);

            await uow.SaveChangesAsync(ct);
        }, cancellationToken);

        return new AcceptInvitationResult(isNewUser);
    }
}
