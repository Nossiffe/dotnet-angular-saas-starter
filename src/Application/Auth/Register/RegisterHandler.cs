using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Auth.Register;

public sealed class RegisterHandler(
    IOrganizationRepository organizations,
    IUnitOfWork uow,
    IIdentityService identity)
    : ICommandHandler<RegisterCommand, Guid>
{
    public async ValueTask<Guid> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Guid userId = Guid.Empty;

        await uow.ExecuteInTransactionAsync(async ct2 =>
        {
            var result = await identity.CreateUserAsync(command.Email, command.Password, ct2);
            if (!result.Succeeded)
            {
                var failures = result.Errors
                    .SelectMany(kv => kv.Value.Select(v => new ValidationFailure(kv.Key, v)));
                throw new ValidationException(failures);
            }

            userId = result.UserId;

            var organization = Organization.Create(
                command.Email,
                Slug.From($"org-{userId:N}"),
                userId);
            organizations.Add(organization);

            await uow.SaveChangesAsync(ct2);
        }, cancellationToken);

        return userId;
    }
}
