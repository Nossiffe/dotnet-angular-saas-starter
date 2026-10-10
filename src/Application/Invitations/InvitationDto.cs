namespace Saas.Application.Invitations;

// InvitedByUserId : le membre qui a envoyé l'invitation. Au retrait d'un membre, l'écran
// Organisation propose de révoquer aussi celles qu'il a envoyées.
public sealed record InvitationDto(
    Guid Id,
    string Email,
    string Role,
    Guid InvitedByUserId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);
