using System.Diagnostics.CodeAnalysis;

namespace Saas.Domain.Common;

[SuppressMessage("Design", "CA1032:Implement standard exception constructors",
    Justification = "Domain exception is only ever thrown with a domain message.")]
[SuppressMessage("Roslynator", "RCS1194:Implement exception constructors",
    Justification = "Domain exception is only ever thrown with a domain message.")]
public sealed class DomainException(string message) : Exception(message);
