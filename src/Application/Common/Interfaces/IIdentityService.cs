namespace Saas.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<CreateUserResult> CreateUserAsync(string email, string password, CancellationToken cancellationToken);
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<AppUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken);
}
