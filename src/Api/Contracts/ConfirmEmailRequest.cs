namespace Saas.Api.Contracts;

public sealed record ConfirmEmailRequest(Guid UserId, string Token);
