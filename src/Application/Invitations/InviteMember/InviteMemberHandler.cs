using Dapper;
using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.InviteMember;

public sealed class InviteMemberHandler(
    IOrganizationRepository organizations,
    IInvitationRepository invitations,
    IUnitOfWork uow,
    IDbConnectionFactory connections,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<InviteMemberCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(InviteMemberCommand command, CancellationToken cancellationToken)
    {
        var orgId = OrganizationId.From(command.OrganizationId);
        var organization = await organizations.GetByIdAsync(orgId, cancellationToken);
        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        // Identity stocke l'e-mail normalisé en majuscules (NormalizedEmail, indexé) ; les invitations
        // le stockent en minuscules (Invitation.Create). Chaque requête utilise la forme de sa table.
        var email = command.Email.Trim().ToLowerInvariant();

        if (await IsAlreadyMemberAsync(command.OrganizationId, email, cancellationToken))
        {
            return OrganizationErrors.AlreadyMember;
        }

        if (await invitations.HasPendingInvitationAsync(orgId, email, cancellationToken))
        {
            return InvitationErrors.AlreadyPending;
        }

        var invitedBy = Guid.Parse(currentUser.UserId!);
        var created = Invitation.Create(
            orgId,
            command.Email,
            command.Role,
            invitedBy,
            clock.UtcNow,
            clock.UtcNow.Add(Invitation.Lifetime));

        if (created.IsError)
        {
            return created.Errors;
        }

        // L'e-mail part après le commit, par l'outbox (MemberInvitedDomainEvent) : c'est là que le
        // jeton du lien est créé (SendInvitationEmailHandler).
        var invitation = created.Value;
        invitations.Add(invitation);
        await uow.SaveChangesAsync(cancellationToken);

        return invitation.Id.Value;
    }

    private async Task<bool> IsAlreadyMemberAsync(Guid organizationId, string email, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var member = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT 1
            FROM "OrganizationMemberships" m
            JOIN "AspNetUsers" u ON u."Id" = m."UserId"
            WHERE m."OrganizationId" = @OrgId AND u."NormalizedEmail" = @Email
            """,
            new { OrgId = organizationId, Email = email.ToUpperInvariant() },
            cancellationToken: cancellationToken));
        return member is not null;
    }
}
