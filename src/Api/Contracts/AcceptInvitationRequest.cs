namespace Saas.Api.Contracts;

public sealed record AcceptInvitationRequest(Guid InvitationId, string Token, string? Password);
