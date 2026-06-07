using Microsoft.EntityFrameworkCore;
using Saas.Domain.Identity;
using Saas.Domain.Products;
using Saas.Domain.Tenancy;

namespace Saas.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationMembership> Memberships { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
