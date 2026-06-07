using Mediator;

namespace Saas.Application.Auth.Register;

public sealed record RegisterCommand(string Email, string Password) : ICommand<Guid>;
