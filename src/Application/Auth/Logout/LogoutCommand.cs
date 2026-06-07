using Mediator;

namespace Saas.Application.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken) : ICommand<Unit>;
