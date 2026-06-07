using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Saas.Domain.Tenancy;

namespace Saas.Infrastructure.Persistence.Converters;

public sealed class OrganizationIdConverter() : ValueConverter<OrganizationId, Guid>(
    id => id.Value,
    value => OrganizationId.From(value));
