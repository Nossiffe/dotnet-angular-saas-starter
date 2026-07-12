using Dapper;
using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;
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
    : ICommandHandler<InviteMemberCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(InviteMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var orgId = OrganizationId.From(command.OrganizationId);
        var organization = await organizations.GetByIdAsync(orgId, cancellationToken);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        var normalizedEmail = command.Email.Trim().ToUpperInvariant();

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
                return OrganizationErrors.AlreadyMember;
            }
        }

        if (await invitations.HasPendingInvitationAsync(orgId, command.Email.Trim().ToLowerInvariant(), cancellationToken))
        {
            return InvitationErrors.AlreadyPending;
        }

        var rawToken = tokens.GenerateRawToken();
        var tokenHash = tokens.HashToken(rawToken);

        var invitedBy = Guid.Parse(currentUser.UserId!);
        var created = Invitation.Create(
            orgId,
            command.Email,
            command.Role,
            invitedBy,
            tokenHash,
            clock.UtcNow,
            clock.UtcNow.AddDays(7));

        if (created.IsError)
        {
            return created.Errors;
        }

        var invitation = created.Value;
        invitations.Add(invitation);
        await uow.SaveChangesAsync(cancellationToken);

        enqueuer.Enqueue(invitation.Id.Value, rawToken, invitation.InviteeEmail);

        return invitation.Id.Value;
    }
}
