using Dapper;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Invitations.ListPendingInvitations;

public sealed class ListPendingInvitationsHandler(IDbConnectionFactory connections)
    : IQueryHandler<ListPendingInvitationsQuery, IReadOnlyList<InvitationDto>>
{
    public async ValueTask<IReadOnlyList<InvitationDto>> Handle(
        ListPendingInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<InvitationDto>(new CommandDefinition(
            """
            SELECT "Id", "InviteeEmail" AS "Email", "Role", "ExpiresAt", "CreatedAt"
            FROM "Invitations"
            WHERE "OrganizationId" = @OrgId AND "Status" = 'Pending'
            ORDER BY "CreatedAt" DESC
            """,
            new { OrgId = query.OrganizationId },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
