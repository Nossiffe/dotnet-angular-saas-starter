namespace Saas.Application.Products;

/// <summary>
/// Clés et tag HybridCache des produits. Le tenant fait partie de la clé et du tag : un tenant ne
/// lit jamais l'entrée d'un autre, et une écriture n'invalide que les entrées de son tenant.
/// </summary>
internal static class ProductCache
{
    public static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    public static string Key(Guid tenantId, Guid productId) => $"products:{tenantId}:{productId}";

    public static string Tag(Guid tenantId) => $"products:{tenantId}";
}
