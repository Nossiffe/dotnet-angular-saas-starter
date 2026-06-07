using Saas.Domain.Products;

namespace Saas.Application.Common.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(ProductId id, CancellationToken cancellationToken);
    void Add(Product product);
    void Remove(Product product);
}
