namespace Saas.Application.Common.Behaviors;

/// <summary>
/// Opt-in strict : marque une query tenant-scoped comme cacheable par le
/// <see cref="CachingBehavior{TMessage,TResponse}"/>. Une query SANS cet attribut
/// traverse le behavior sans aucune écriture de cache.
/// </summary>
/// <remarks>
/// Réservé aux queries tenant-scoped : la clé et les tags sont préfixés par le
/// <c>TenantId</c> d'<c>ITenantProvider</c>, qui est fail-closed (lève si le claim
/// <c>tenant</c> est absent). Poser cet attribut sur une query anonyme fera lever
/// le behavior — c'est voulu.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CachedQueryAttribute(int seconds) : Attribute
{
    /// <summary>Durée de vie de l'entrée de cache, en secondes.</summary>
    public int Seconds { get; } = seconds;

    /// <summary>
    /// Sous-tag logique de la famille d'entités (ex. <c>"products"</c>). Combiné au
    /// tenant, il donne le tag <c>t:{tenantId}:{Tag}</c> ciblé par
    /// <c>RemoveByTagAsync</c> lors des writes.
    /// </summary>
    public string Tag { get; init; } = string.Empty;
}
