using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Auth.Register;

public sealed class RegisterHandler(IAppDbContext db, IIdentityService identity)
    : ICommandHandler<RegisterCommand, Guid>
{
    public async ValueTask<Guid> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Guid userId = Guid.Empty;

        await db.ExecuteInTransactionAsync(async ct2 =>
        {
            var result = await identity.CreateUserAsync(command.Email, command.Password, ct2);
            if (!result.Succeeded)
            {
                var failures = result.Errors
                    .SelectMany(kv => kv.Value.Select(v => new ValidationFailure(kv.Key, v)));
                throw new ValidationException(failures);
            }

            userId = result.UserId;

            var org = new Organization { Name = command.Email, Slug = $"org-{userId:N}" };
            db.Organizations.Add(org);
            db.Memberships.Add(new OrganizationMembership
            {
                OrganizationId = org.Id,
                UserId = userId,
                Role = "Owner",
            });

            await db.SaveChangesAsync(ct2);
        }, cancellationToken);

        return userId;
    }
}
