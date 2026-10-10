namespace Saas.IntegrationTests.Auth;

/// <summary>Clients HTTP authentifiés par <see cref="TestAuthHandler"/>, qui lit les claims dans les en-têtes.</summary>
public static class TestClients
{
    /// <summary>
    /// Client d'un utilisateur du tenant <paramref name="tenant"/>, avec le rôle donné. Sans tenant, un
    /// tenant neuf, que seul ce test utilise ; aucune organisation n'est créée en base
    /// (<c>InvitationTestFlow.SeedOrganizationAsync</c> le fait pour les routes qui en demandent une).
    /// </summary>
    public static HttpClient CreateAuthenticatedClient(
        this PostgresApiFactory factory, Guid? tenant = null, string tenantRole = "Owner", Guid? userId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, (tenant ?? Guid.CreateVersion7()).ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantRoleHeader, tenantRole);
        if (userId is { } user)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user.ToString());
        }

        return client;
    }
}
