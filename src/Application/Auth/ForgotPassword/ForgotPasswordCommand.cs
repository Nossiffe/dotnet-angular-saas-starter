using ErrorOr;
using Mediator;

namespace Saas.Application.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : ICommand<ErrorOr<Success>>;
