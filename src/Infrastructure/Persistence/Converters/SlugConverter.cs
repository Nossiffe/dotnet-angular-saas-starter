using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Saas.Domain.Tenancy;

namespace Saas.Infrastructure.Persistence.Converters;

public sealed class SlugConverter() : ValueConverter<Slug, string>(
    slug => slug.Value,
    value => Slug.From(value));
