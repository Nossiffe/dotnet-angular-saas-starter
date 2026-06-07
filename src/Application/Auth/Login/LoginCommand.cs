using Mediator;

namespace Saas.Application.Auth.Login;

public sealed record LoginCommand(string Email, string Password, Guid? TenantId) : ICommand<AuthResult?>;
