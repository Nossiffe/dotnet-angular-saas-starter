using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Saas.Infrastructure.Identity;
using Saas.IntegrationTests.Invitations;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

// Les routes rattachées à une organisation (produits, invitations, membres) relisent l'adhésion de
// l'appelant en base : le tenant et le rôle du token ne suffisent plus. TestAuthHandler fabrique
// ici le token ; les tests à vrais JWT sont dans StaleRoleTests.
[Collection("api")]
public sealed class CurrentMembershipTests(PostgresApiFactory factory)
{
    // Une route par politique et par groupe : HasTenant (produits), TenantAdmin (suppression d'un
    // produit, invitations), TenantOwner (rôle d'un membre).
    [Theory]
    [InlineData("GET", "/api/v1/products")]
    [InlineData("POST", "/api/v1/products")]
    [InlineData("DELETE", "/api/v1/products/{id}")]
    [InlineData("GET", "/api/v1/organizations/{org}/invitations")]
    [InlineData("POST", "/api/v1/organizations/{org}/invitations")]
    [InlineData("PUT", "/api/v1/organizations/{org}/members/{id}/role")]
    public async Task A_token_for_an_organization_the_user_is_not_a_member_of_gets_401(string method, string route)
    {
        var ct = TestContext.Current.CancellationToken;
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, Guid.CreateVersion7(), ct);
        var outsider = factory.CreateAuthenticatedClient(orgId, "Owner", Guid.CreateVersion7(), asMember: false);

        using var request = Request(method, route, orgId);
        var response = await outsider.SendAsync(request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(response, ct)).ShouldBe("Auth.SessionExpired");
    }

    // Le token annonce le rôle d'avant une rétrogradation : Admin pour un Member, Owner pour un
    // Admin. Le rôle du token suffirait pour la route ; celui de la base, non.
    [Theory]
    [InlineData("GET", "/api/v1/products", "Member", "Admin")]
    [InlineData("DELETE", "/api/v1/products/{id}", "Member", "Admin")]
    [InlineData("GET", "/api/v1/organizations/{org}/invitations", "Member", "Admin")]
    [InlineData("POST", "/api/v1/organizations/{org}/invitations", "Member", "Admin")]
    [InlineData("PUT", "/api/v1/organizations/{org}/members/{id}/role", "Admin", "Owner")]
    public async Task A_token_whose_role_no_longer_matches_the_membership_gets_401(
        string method, string route, string currentRole, string tokenRole)
    {
        var ct = TestContext.Current.CancellationToken;
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, Guid.CreateVersion7(), ct);
        var memberId = Guid.CreateVersion7();
        _ = factory.CreateAuthenticatedClient(orgId, currentRole, memberId);
        var withStaleRole = factory.CreateAuthenticatedClient(orgId, tokenRole, memberId);

        using var request = Request(method, route, orgId);
        var response = await withStaleRole.SendAsync(request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(response, ct)).ShouldBe("Auth.SessionExpired");
    }

    // Un rôle insuffisant mais à jour reste un 403 : le 401 ne sert qu'à un token périmé.
    [Fact]
    public async Task A_current_member_without_the_required_role_still_gets_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, Guid.CreateVersion7(), ct);

        var response = await factory.CreateAuthenticatedClient(orgId, "Member", Guid.CreateVersion7())
            .GetAsync($"/api/v1/organizations/{orgId}/invitations", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Garde-fou pour les routes à venir : toute route authentifiée passe par l'une des politiques
    // qui relisent l'adhésion (HasTenant, TenantAdmin, TenantOwner). Une route authentifiée qui ne
    // serait rattachée à aucune organisation devrait être ajoutée ici explicitement.
    [Fact]
    public async Task Every_authenticated_route_requires_a_current_membership()
    {
        var policies = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        var withoutMembershipCheck = new List<string>();

        foreach (var endpoint in endpoints.Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null))
        {
            var checksMembership = false;
            foreach (var name in endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).OfType<string>())
            {
                var policy = await policies.GetPolicyAsync(name);
                checksMembership |= policy?.Requirements.Contains(CurrentMembershipRequirement.Instance) == true;
            }

            if (!checksMembership)
            {
                withoutMembershipCheck.Add(endpoint.DisplayName ?? endpoint.RoutePattern.RawText ?? "?");
            }
        }

        endpoints.Count.ShouldBeGreaterThan(10);
        withoutMembershipCheck.ShouldBeEmpty();
    }

    // {id} : un produit ou un membre quelconque, la requête est refusée avant l'endpoint.
    private static HttpRequestMessage Request(string method, string route, Guid orgId)
    {
        var path = route
            .Replace("{org}", orgId.ToString(), StringComparison.Ordinal)
            .Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal);
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Content = method switch
        {
            "POST" when route.Contains("invitations", StringComparison.Ordinal)
                => JsonContent.Create(new { email = $"invitee-{Guid.NewGuid():N}@saas.local", role = "Member" }),
            "POST" => JsonContent.Create(new { name = $"Product-{Guid.NewGuid():N}", price = 1m }),
            "PUT" => JsonContent.Create(new { role = "Member" }),
            _ => null,
        };
        return request;
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response, CancellationToken ct) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString();
}
