using ErrorOr;

namespace Saas.Domain.Products;

public static class ProductErrors
{
    public static Error NotFound => Error.NotFound(
        code: "Product.NotFound", description: "Produit introuvable.");
}
