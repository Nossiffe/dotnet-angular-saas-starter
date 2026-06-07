using Mediator;

namespace Saas.Application.Auth.ConfirmEmail;

public sealed record ConfirmEmailCommand(Guid UserId, string Token) : ICommand<bool>;
