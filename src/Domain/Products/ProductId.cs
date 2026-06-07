using Vogen;

namespace Saas.Domain.Products;

[ValueObject<Guid>(conversions: Conversions.SystemTextJson, comparison: ComparisonGeneration.Omit)]
public partial struct ProductId;
