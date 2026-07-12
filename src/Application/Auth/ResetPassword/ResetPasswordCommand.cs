using ErrorOr;
using Mediator;

namespace Saas.Application.Auth.ResetPassword;

public sealed record ResetPasswordCommand(Guid UserId, string Token, string NewPassword) : ICommand<ErrorOr<Success>>;
