using System.Reflection;
using System.Text.Json;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Caching.Hybrid;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Common.Behaviors;

/// <summary>
/// Behavior de pipeline qui cache les queries marquées <see cref="CachedQueryAttribute"/>
/// (opt-in strict) dans <see cref="HybridCache"/>, avec une clé et des tags scopés au tenant.
/// </summary>
/// <remarks>
/// <para>Clé : <c>q:{tenantId}:{TMessage.Name}:{json(message)}</c> — le tenant vient
/// d'<see cref="ITenantProvider"/> (fail-closed).</para>
/// <para>Tags : <c>t:{tenantId}</c> (tout le tenant) + <c>t:{tenantId}:{Tag}</c> (la famille
/// d'entités) — les writes invalident par tag via <c>RemoveByTagAsync</c>. Invalidation
/// <b>logique</b> : <c>RemoveByTagAsync</c> n'efface pas physiquement, il ignore toute entrée
/// antérieure au point d'invalidation.</para>
/// <para>Seuls les succès sont cachés : <typeparamref name="TResponse"/> est un
/// <c>ErrorOr&lt;TValue&gt;</c> dont on ne conserve que <c>TValue</c> (re-wrappé au hit) — les
/// erreurs ne sont jamais mises en cache.</para>
/// </remarks>
public sealed class CachingBehavior<TMessage, TResponse>(HybridCache cache, ITenantProvider tenant)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    private delegate ValueTask<object> CacheInvoker(
        HybridCache cache,
        string key,
        IEnumerable<string> tags,
        TimeSpan expiration,
        Func<CancellationToken, ValueTask<object>> next,
        CancellationToken cancellationToken);

    private static readonly CachedQueryAttribute? _attribute =
        typeof(TMessage).GetCustomAttribute<CachedQueryAttribute>(inherit: false);

    // Compilé une seule fois par type fermé (TMessage, TResponse) : les champs statiques d'un
    // type générique sont uniques par instanciation fermée → pas de ConcurrentDictionary.
    private static readonly CacheInvoker? _invoker = BuildInvoker();

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        // Opt-in strict : pas d'attribut → behavior transparent, aucune écriture de cache.
        if (_attribute is null || _invoker is null)
        {
            return await next(message, cancellationToken);
        }

        // Fail-closed : lève si le claim tenant est absent (voulu — [CachedQuery] est réservé
        // aux queries tenant-scoped).
        var tenantId = tenant.TenantId;
        var key = $"q:{tenantId}:{typeof(TMessage).Name}:{JsonSerializer.Serialize(message)}";
        var tags = new[] { $"t:{tenantId}", $"t:{tenantId}:{_attribute.Tag}" };
        var expiration = TimeSpan.FromSeconds(_attribute.Seconds);

        var boxed = await _invoker(
            cache,
            key,
            tags,
            expiration,
            async token => (object)(await next(message, token))!,
            cancellationToken);

        return (TResponse)boxed;
    }

    private static CacheInvoker? BuildInvoker()
    {
        if (_attribute is null)
        {
            return null;
        }

        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(ErrorOr<>))
        {
            throw new InvalidOperationException(
                $"[CachedQuery] requiert une réponse ErrorOr<T> ; {typeof(TMessage).Name} renvoie {responseType.Name}.");
        }

        var valueType = responseType.GetGenericArguments()[0];
        var method = typeof(CachingAdapter)
            .GetMethod(nameof(CachingAdapter.CacheAsync), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(valueType);

        return method.CreateDelegate<CacheInvoker>();
    }
}
