using System.Diagnostics.CodeAnalysis;

namespace Saas.Domain.Common;

[SuppressMessage("Design", "CA1032:Implement standard exception constructors",
    Justification = "Domain exception is only ever thrown with (entity, key).")]
[SuppressMessage("Roslynator", "RCS1194:Implement exception constructors",
    Justification = "Domain exception is only ever thrown with (entity, key).")]
public sealed class NotFoundException(string entity, object key)
    : Exception($"'{entity}' ({key}) introuvable.")
{
    public string Entity { get; } = entity;
    public object Key { get; } = key;
}
