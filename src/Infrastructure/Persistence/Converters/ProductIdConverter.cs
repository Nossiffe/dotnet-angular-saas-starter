using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Saas.Domain.Products;

namespace Saas.Infrastructure.Persistence.Converters;

// Converter EF Core pour le value object Vogen ProductId (généré en STJ only côté Domain
// pour ne pas faire dépendre Domain d'EF Core). La conversion stockage vit ici, en Infrastructure.
public sealed class ProductIdConverter() : ValueConverter<ProductId, Guid>(
    id => id.Value,
    value => ProductId.From(value));
