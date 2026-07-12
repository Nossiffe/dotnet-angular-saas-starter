using ErrorOr;
using Mediator;

namespace Saas.Application.Auth.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<ErrorOr<AuthResult>>;
