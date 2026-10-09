using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Identity;

public sealed class TenantProvider(IHttpContextAccessor accessor) : ITenantProvider
{
    public Guid TenantId
    {
        get
        {
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

            return id;
        }
    }
}
