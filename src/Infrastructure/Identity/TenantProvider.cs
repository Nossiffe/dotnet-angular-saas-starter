using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Identity;

public sealed class TenantProvider(IHttpContextAccessor accessor) : ITenantProvider
{
    // Mémoïse la 1re résolution réussie, pour la durée du scope (une requête). Nécessaire au
    // CachingBehavior : HybridCache exécute la factory de cache sur un contexte d'exécution
    // DÉTACHÉ (le flux ExecutionContext est supprimé pour partager le résultat entre appelants
    // concurrents), où HttpContext est indisponible. Le behavior résout le tenant sur le thread
    // de la requête AVANT d'entrer dans la factory ; le handler, qui partage cette même instance
    // scopée, relit ici la valeur mémoïsée au lieu de retoucher HttpContext (absent → lèverait).
    // Ne cache QUE le succès : le fail-closed reste intact (un contexte sans claim valide lève
    // à chaque accès, rien n'est mémoïsé).
    private Guid? _cached;

    public Guid TenantId
    {
        get
        {
            if (_cached is { } cached)
            {
                return cached;
            }

            var raw = accessor.HttpContext?.User.FindFirstValue("tenant");
            if (!Guid.TryParse(raw, out var id) || id == Guid.Empty)
            {
                // Fail-closed. Un contexte sans tenant valide ne doit jamais permettre
                // de lire ou d'écrire silencieusement avec un tenant vide. L'accès aux
                // routes tenant-scoped est déjà bloqué en amont par l'autorisation.
                // Arriver ici révèle donc un bug de programmation, pas une fuite de données.
                throw new InvalidOperationException(
                    "Aucun tenant dans le contexte — claim 'tenant' absent ou invalide (fail-closed).");
            }

            _cached = id;
            return id;
        }
    }
}
