using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Saas.Domain.Tenancy;

namespace Saas.Infrastructure.Persistence.Converters;

public sealed class InvitationIdConverter() : ValueConverter<InvitationId, Guid>(
    id => id.Value,
    value => InvitationId.From(value));
