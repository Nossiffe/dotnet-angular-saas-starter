using Dapper;
using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Invitations.ListPendingInvitations;

public sealed class ListPendingInvitationsHandler(IDbConnectionFactory connections)
    : IQueryHandler<ListPendingInvitationsQuery, ErrorOr<IReadOnlyList<InvitationDto>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<InvitationDto>>> Handle(
        ListPendingInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        // Npgsql matérialise les colonnes `timestamptz` en DateTime (Kind=Utc) via le data reader de
        // Dapper : on lit donc un row intermédiaire en DateTime puis on projette en DateTimeOffset
        // (offset zéro = UTC) pour respecter le contrat InvitationDto sans dépendre du mapping implicite.
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT "Id", "InviteeEmail" AS "Email", "Role", "ExpiresAt", "CreatedAt"
            FROM "Invitations"
            WHERE "OrganizationId" = @OrgId AND "Status" = 'Pending'
            ORDER BY "CreatedAt" DESC
            """,
            new { OrgId = query.OrganizationId },
            cancellationToken: cancellationToken));

        return rows
            .Select(r => new InvitationDto(
                r.Id,
                r.Email,
                r.Role,
                new DateTimeOffset(r.ExpiresAt, TimeSpan.Zero),
                new DateTimeOffset(r.CreatedAt, TimeSpan.Zero)))
            .ToList();
    }

    private sealed record Row(Guid Id, string Email, string Role, DateTime ExpiresAt, DateTime CreatedAt);
}
