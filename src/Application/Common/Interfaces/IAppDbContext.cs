using Microsoft.EntityFrameworkCore;
using Saas.Domain.Products;

namespace Saas.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Product> Products { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
