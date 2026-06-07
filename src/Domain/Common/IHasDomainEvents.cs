namespace Saas.Domain.Common;

/// <summary>Abstraction non générique pour collecter/vider les domain events d'un agrégat.</summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
