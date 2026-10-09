namespace Saas.Api.Contracts;

// TenantId : organisation à ouvrir ; sans elle, la connexion ouvre la plus ancienne adhésion.
public sealed record LoginRequest(string Email, string Password, Guid? TenantId = null);
