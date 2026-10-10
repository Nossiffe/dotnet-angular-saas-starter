using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Infrastructure.Persistence;

namespace Saas.IntegrationTests.Auth;

/// <summary>Clients HTTP authentifiés par <see cref="TestAuthHandler"/>, qui lit les claims dans les en-têtes.</summary>
public static class TestClients
{
    /// <summary>
    /// Client d'un membre du tenant <paramref name="tenant"/>, avec le rôle donné. Sans tenant, un
    /// tenant neuf, que seul ce test utilise ; sans utilisateur, un utilisateur neuf.
    /// <para>
    /// Les routes rattachées à une organisation relisent l'adhésion en base : le client crée donc
    /// l'adhésion (et une organisation minimale si le tenant n'en a pas encore) avec ce rôle. Une
    /// adhésion qui existe déjà garde son rôle : un en-tête de rôle différent simule alors un token
    /// émis avant un changement de rôle. Avec <paramref name="asMember"/> à <c>false</c>, rien n'est
    /// créé : le token désigne une organisation dont l'utilisateur n'est pas membre.
    /// </para>
    /// <para>
    /// Le rôle par défaut est Owner. Pour l'Owner d'une organisation déjà créée, passer son
    /// <paramref name="userId"/> : un utilisateur neuf en Owner y ajouterait un second Owner, ce que
    /// l'API ne permet pas.
    /// </para>
    /// </summary>
    public static HttpClient CreateAuthenticatedClient(
        this PostgresApiFactory factory,
        Guid? tenant = null,
        string tenantRole = "Owner",
        Guid? userId = null,
        bool asMember = true)
    {
        var tenantId = tenant ?? Guid.CreateVersion7();
        var user = userId ?? Guid.CreateVersion7();
        if (asMember)
        {
            EnsureMembership(factory, tenantId, user, tenantRole);
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenantId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantRoleHeader, tenantRole);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user.ToString());
        return client;
    }

    private static void EnsureMembership(PostgresApiFactory factory, Guid tenantId, Guid userId, string role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = $"test-{tenantId:N}";
        var membershipId = Guid.CreateVersion7();
        db.Database.ExecuteSql($"""
            INSERT INTO "Organizations" ("Id", "Name", "Slug", "CreatedAt")
            VALUES ({tenantId}, {slug}, {slug}, now())
            ON CONFLICT ("Id") DO NOTHING
            """);
        db.Database.ExecuteSql($"""
            INSERT INTO "OrganizationMemberships" ("Id", "OrganizationId", "UserId", "Role", "JoinedAt")
            VALUES ({membershipId}, {tenantId}, {userId}, {role}, now())
            ON CONFLICT ("UserId", "OrganizationId") DO NOTHING
            """);
    }
}
