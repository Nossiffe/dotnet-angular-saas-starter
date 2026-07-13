using ErrorOr;
using Microsoft.Extensions.Caching.Hybrid;

namespace Saas.Application.Common.Behaviors;

/// <summary>
/// Adaptateur non générique : héberge la méthode générique <see cref="CacheAsync{TValue}"/>
/// fermée une fois par type de valeur via <c>MakeGenericMethod</c> (le behavior ne connaît
/// <c>TValue</c> qu'à l'exécution). Ne cache que le succès d'un <c>ErrorOr&lt;TValue&gt;</c>.
/// </summary>
internal static class CachingAdapter
{
    // Enveloppe sérialisable : ne porte QUE la valeur de succès (jamais l'ErrorOr).
    private readonly record struct Box<TValue>(TValue Value, bool Ok);

    public static async ValueTask<object> CacheAsync<TValue>(
        HybridCache cache,
        string key,
        IEnumerable<string> tags,
        TimeSpan expiration,
        Func<CancellationToken, ValueTask<object>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(next);

        // L'erreur éventuelle est capturée hors-bande (closure) pour ne JAMAIS être sérialisée.
        object? errorResponse = null;
        var options = new HybridCacheEntryOptions { Expiration = expiration };

#pragma warning disable EXTEXP0018 // overloads tags/RemoveByTag de HybridCache : stables en .NET 10, attribut Experimental résiduel
        var box = await cache.GetOrCreateAsync(
            key,
            async token =>
            {
                var response = (ErrorOr<TValue>)await next(token);
                if (!response.IsError)
                {
                    return new Box<TValue>(response.Value, Ok: true);
                }

                errorResponse = response; // capturé, jamais mis en cache
                return new Box<TValue>(default!, Ok: false);
            },
            options,
            tags,
            cancellationToken);

        if (box.Ok)
        {
            ErrorOr<TValue> success = box.Value;
            return success;
        }

        // Miss d'erreur : ne pas entretenir l'entrée non-ok (fenêtre de course courte,
        // best-effort — identique à l'ancien cache-aside manuel de GetProductById).
        await cache.RemoveAsync(key, cancellationToken);
#pragma warning restore EXTEXP0018

        // errorResponse null ⇒ hit concurrent sur un box non-ok posé par un autre appelant :
        // ré-exécuter next pour obtenir l'ErrorOr d'erreur frais.
        return errorResponse ?? await next(cancellationToken);
    }
}
