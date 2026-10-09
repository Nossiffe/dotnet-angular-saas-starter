using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.AcceptInvitation;

public sealed class AcceptInvitationHandler(
    IInvitationRepository invitations,
    IOrganizationRepository organizations,
    IIdentityService identity,
    IUnitOfWork uow,
    IInvitationTokenService tokens,
    IDateTimeProvider clock)
    : ICommandHandler<AcceptInvitationCommand, ErrorOr<AcceptInvitationResult>>
{
    public async ValueTask<ErrorOr<AcceptInvitationResult>> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Message neutre (anti-énumération) : ne révèle pas si l'invitation existe, si le token est
        // faux ou si aucun lien n'est valable (avant le premier envoi, ou après un renvoi en attente
        // de l'outbox). L'endpoint est anonyme : pas d'oracle d'énumération.
        var invitation = await invitations.GetByIdAsync(InvitationId.From(command.InvitationId), cancellationToken);
        if (invitation is null || !tokens.VerifyToken(command.Token, invitation.TokenHash))
        {
            return InvitationErrors.Invalid;
        }

        var existingUser = await identity.FindByEmailAsync(invitation.InviteeEmail, cancellationToken);
        var isNewUser = existingUser is null;
        if (isNewUser && string.IsNullOrEmpty(command.Password))
        {
            return InvitationErrors.PasswordRequired;
        }

        ErrorOr<Success>? transactionResult = null;
        await uow.ExecuteInTransactionAsync(
            async ct => transactionResult = await AcceptWithinTransactionAsync(invitation, existingUser?.Id, command.Password, ct),
            cancellationToken);

        if (transactionResult is { IsError: true } errorResult)
        {
            return errorResult.Errors;
        }

        return new AcceptInvitationResult(isNewUser);
    }

    private async Task<ErrorOr<Success>> AcceptWithinTransactionAsync(
        Invitation invitation,
        Guid? existingUserId,
        string? password,
        CancellationToken ct)
    {
        // Toutes les vérifications ErrorOr d'abord (aucun effet de bord avant) : la transaction
        // ne rollback que sur throw, un early-return ErrorOr commiterait ce qui précède.
        var organization = await organizations.GetByIdAsync(invitation.OrganizationId, ct);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        var accepted = invitation.Accept(clock.UtcNow);
        if (accepted.IsError)
        {
            return accepted.Errors;
        }

        // Effets de bord seulement après validation : création du user (throw → rollback OK),
        // puis ajout idempotent (déjà membre → no-op, cf. Organization.AddMember).
        var userId = existingUserId ?? await CreateUserAsync(invitation.InviteeEmail, password!, ct);
        organization.AddMember(userId, invitation.Role);
        await uow.SaveChangesAsync(ct);

        return Result.Success;
    }

    // Les refus d'Identity (règles de mot de passe, e-mail déjà pris) sont des erreurs de saisie liées
    // à un champ : ValidationException, comme le pipeline FluentValidation (docs/adr/0003).
    private async Task<Guid> CreateUserAsync(string email, string password, CancellationToken ct)
    {
        var result = await identity.CreateUserAsync(email, password, ct);
        if (!result.Succeeded)
        {
            var failures = result.Errors
                .SelectMany(kv => kv.Value.Select(v => new ValidationFailure(kv.Key, v)));
            throw new ValidationException(failures);
        }

        var emailConfirmed = await identity.ConfirmEmailDirectlyAsync(result.UserId, ct);
        if (!emailConfirmed)
        {
            throw new InvalidOperationException("Échec de la confirmation de l'email du nouvel utilisateur.");
        }

        return result.UserId;
    }
}
