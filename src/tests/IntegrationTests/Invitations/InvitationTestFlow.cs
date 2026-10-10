using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;
using Saas.IntegrationTests.Auth;

namespace Saas.IntegrationTests.Invitations;

/// <summary>
/// Helpers pour les flux d'invitation : provisionne une vraie Organization (avec un Owner membre)
/// directement en base via un scope DI. Le client de son Owner vient de
/// <see cref="TestClients.CreateAuthenticatedClient"/>, avec l'organisation et l'utilisateur créés ici.
///
/// Pourquoi une vraie Organization : les endpoints TenantAdmin (invite/accept/change-role) chargent
/// l'organisation par id (`IOrganizationRepository.GetByIdAsync`), avec son nom et son Owner.
/// TestAuthHandler ne fait que fabriquer des claims JWT ; <see cref="TestClients.CreateAuthenticatedClient"/>
/// crée seulement l'adhésion que l'API relit, avec une organisation minimale si le tenant n'en a pas.
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
            ownerUserId,
            DateTimeOffset.UtcNow);
        orgs.Add(org);
        await uow.SaveChangesAsync(cancellationToken);

        return org.Id.Value;
    }
}
