using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;
using Saas.IntegrationTests.Auth;

namespace Saas.IntegrationTests.Invitations;

/// <summary>
/// Helpers pour les flux d'invitation : provisionne une VRAIE Organization (avec un Owner membre)
/// directement en base via un scope DI, et construit un client HTTP piloté par TestAuthHandler dont
/// les claims (tenant + tenant_role + sub) correspondent à cette organisation.
///
/// Pourquoi une vraie Organization : les endpoints TenantAdmin (invite/accept/change-role) chargent
/// l'organisation par id (`IOrganizationRepository.GetByIdAsync`) — TestAuthHandler ne fait que
/// fabriquer des claims JWT, il ne crée AUCUNE ligne. Les tests Products n'en ont pas besoin (ils
/// s'appuient sur le global query filter), mais les invitations OUI.
/// </summary>
internal static class InvitationTestFlow
{
    /// <summary>
    /// Crée une organisation réelle dont l'Owner est <paramref name="ownerUserId"/> et renvoie son Id
    /// (= futur claim "tenant"). Persisté via repository + unit of work dans un scope.
    /// </summary>
    public static async Task<Guid> SeedOrganizationAsync(
        PostgresApiFactory factory, Guid ownerUserId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var orgs = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var org = Organization.Create(
            $"Org-{Guid.NewGuid():N}",
            Slug.From($"org-{Guid.NewGuid():N}"),
            ownerUserId);
        orgs.Add(org);
        await uow.SaveChangesAsync(cancellationToken);

        return org.Id.Value;
    }

    /// <summary>Client HTTP avec les claims TestAuthHandler pour un tenant/user/role donnés.</summary>
    public static HttpClient ClientFor(PostgresApiFactory factory, Guid tenant, Guid userId, string tenantRole = "Owner")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenant.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantRoleHeader, tenantRole);
        return client;
    }
}
