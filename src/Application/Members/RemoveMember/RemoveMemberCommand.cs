using ErrorOr;
using Mediator;

namespace Saas.Application.Members.RemoveMember;

public sealed record RemoveMemberCommand(Guid OrganizationId, Guid MemberUserId)
    : ICommand<ErrorOr<Deleted>>;
