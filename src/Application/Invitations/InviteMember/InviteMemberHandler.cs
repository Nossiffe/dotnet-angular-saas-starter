using Dapper;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.InviteMember;

public sealed class InviteMemberHandler(
    IOrganizationRepository organizations,
    IInvitationRepository invitations,
    IInvitationTokenService tokens,
    IInvitationEmailEnqueuer enqueuer,
    IUnitOfWork uow,
    IDbConnectionFactory connections,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<InviteMemberCommand, Guid>
{
    public async ValueTask<Guid> Handle(InviteMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var orgId = OrganizationId.From(command.OrganizationId);
        _ = await organizations.GetByIdAsync(orgId, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), command.OrganizationId);

        var email = command.Email.Trim().ToLowerInvariant();
        var normalizedEmail = email.ToUpperInvariant();

        await using (var connection = await connections.OpenConnectionAsync(cancellationToken))
        {
            var alreadyMember = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
                """
                SELECT 1
                FROM "OrganizationMemberships" m
                JOIN "AspNetUsers" u ON u."Id" = m."UserId"
                WHERE m."OrganizationId" = @OrgId AND u."NormalizedEmail" = @Email
                """,
                new { OrgId = command.OrganizationId, Email = normalizedEmail },
                cancellationToken: cancellationToken));

            if (alreadyMember is not null)
            {
                throw new ValidationException([new ValidationFailure("Email", "Cet utilisateur est déjà membre de l'organisation.")]);
            }
        }

        if (await invitations.HasPendingInvitationAsync(orgId, email, cancellationToken))
        {
            throw new ValidationException([new ValidationFailure("Email", "Une invitation en attente existe déjà pour cet email.")]);
        }

        var rawToken = tokens.GenerateRawToken();
        var tokenHash = tokens.HashToken(rawToken);

        var invitedBy = Guid.Parse(currentUser.UserId!);
        var invitation = Invitation.Create(
            orgId,
            email,
            command.Role,
            invitedBy,
            tokenHash,
            clock.UtcNow.AddDays(7));

        invitations.Add(invitation);
        await uow.SaveChangesAsync(cancellationToken);

        enqueuer.Enqueue(invitation.Id.Value, rawToken, email);

        return invitation.Id.Value;
    }
}
