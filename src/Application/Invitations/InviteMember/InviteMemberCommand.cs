using Mediator;
using Saas.Domain.Tenancy;

namespace Saas.Application.Invitations.InviteMember;

public sealed record InviteMemberCommand(Guid OrganizationId, string Email, OrganizationRole Role) : ICommand<Guid>;
