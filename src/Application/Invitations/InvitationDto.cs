namespace Saas.Application.Invitations;

public sealed record InvitationDto(
    Guid Id,
    string Email,
    string Role,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);
