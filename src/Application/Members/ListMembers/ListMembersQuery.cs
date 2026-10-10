using ErrorOr;
using Mediator;

namespace Saas.Application.Members.ListMembers;

public sealed record ListMembersQuery(Guid OrganizationId)
    : IQuery<ErrorOr<IReadOnlyList<MemberDto>>>;
