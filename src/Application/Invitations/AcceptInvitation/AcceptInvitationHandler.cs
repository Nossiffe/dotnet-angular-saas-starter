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

        // Message neutre (anti-énumération) : ne révèle pas si l'invitation existe, est expirée,
        // révoquée ou si le token est faux (l'endpoint est anonyme — pas d'oracle d'énumération).
        var invitation = await invitations.GetByIdAsync(InvitationId.From(command.InvitationId), cancellationToken);
        if (invitation is null || !tokens.VerifyToken(command.Token, invitation.TokenHash))
        {
            return InvitationErrors.Invalid;
        }

        var existingUser = await identity.FindByEmailAsync(invitation.InviteeEmail, cancellationToken);
        var isNewUser = existingUser is null;

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
        var userId = existingUserId ?? await CreateUserAsync(invitation.InviteeEmail, password, ct);

        var organization = await organizations.GetByIdAsync(invitation.OrganizationId, ct);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        if (organization.Memberships.Any(m => m.UserId == userId))
        {
            return OrganizationErrors.AlreadyJoined;
        }

        var accepted = invitation.Accept(clock.UtcNow);
        if (accepted.IsError)
        {
            return accepted.Errors;
        }

        organization.AddMember(userId, invitation.Role);
        await uow.SaveChangesAsync(ct);

        return Result.Success;
    }

    private async Task<Guid> CreateUserAsync(string email, string? password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ValidationException(
                [new ValidationFailure("Password", "Un mot de passe est requis pour finaliser la création du compte.")]);
        }

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
