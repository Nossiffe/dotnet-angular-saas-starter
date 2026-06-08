using Mediator;
using Saas.Domain.Tenancy;

namespace Saas.Application.Members.ChangeMemberRole;

public sealed record ChangeMemberRoleCommand(Guid OrganizationId, Guid MemberUserId, OrganizationRole NewRole) : ICommand;
