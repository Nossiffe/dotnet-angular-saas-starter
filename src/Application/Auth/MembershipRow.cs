namespace Saas.Application.Auth;

// Projection read (Dapper) d'un membership, pour résoudre tenant + rôle au login/refresh.
public sealed record MembershipRow(Guid OrganizationId, string Role);
