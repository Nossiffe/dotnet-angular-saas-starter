using Vogen;

namespace Saas.Domain.Tenancy;

[ValueObject<Guid>(conversions: Conversions.SystemTextJson, comparison: ComparisonGeneration.Omit)]
public partial struct InvitationId;
