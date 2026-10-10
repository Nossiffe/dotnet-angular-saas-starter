namespace Saas.Application.Members;

/// <summary>Un membre d'une organisation, tel que la liste l'expose : rien d'autre.</summary>
public sealed record MemberDto(
    Guid UserId,
    string Email,
    string Role,
    DateTimeOffset JoinedAt);
