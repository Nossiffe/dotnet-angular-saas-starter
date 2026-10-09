namespace Saas.Api.Contracts;

public sealed record LoginRequest(string Email, string Password, Guid? TenantId);
