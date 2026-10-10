using Dapper;
using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Members.ListMembers;

public sealed class ListMembersHandler(IDbConnectionFactory connections)
    : IQueryHandler<ListMembersQuery, ErrorOr<IReadOnlyList<MemberDto>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<MemberDto>>> Handle(
        ListMembersQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        // L'e-mail vient du compte Identity : une adhésion a toujours son compte, créé à l'inscription
        // ou à l'acceptation d'une invitation. Comme pour les invitations, la colonne `timestamptz`
        // arrive en DateTime (Kind=Utc) et devient un DateTimeOffset en UTC.
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT m."UserId", u."Email", m."Role", m."JoinedAt"
            FROM "OrganizationMemberships" m
            JOIN "AspNetUsers" u ON u."Id" = m."UserId"
            WHERE m."OrganizationId" = @OrgId
            ORDER BY m."JoinedAt", m."Id"
            """,
            new { OrgId = query.OrganizationId },
            cancellationToken: cancellationToken));

        return rows
            .Select(r => new MemberDto(r.UserId, r.Email, r.Role, new DateTimeOffset(r.JoinedAt, TimeSpan.Zero)))
            .ToList();
    }

    private sealed record Row(Guid UserId, string Email, string Role, DateTime JoinedAt);
}
