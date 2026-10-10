using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;
using Saas.Infrastructure.Persistence;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Members;

// Liste des membres, avec les policies de l'application : lecture pour l'Owner et l'Admin.
[Collection("api")]
public sealed class MemberManagementTests(PostgresApiFactory factory)
{
    private static readonly DateTimeOffset _createdAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] _exposedFields = ["email", "joinedAt", "role", "userId"];

    // Une autre organisation, avec ses propres membres, existe dans la base : la liste n'en montre
    // rien. L'ordre attendu (Owner, Member, Admin) suit les dates d'arrivée. Il diffère de l'ordre
    // des Id, vérifié d'abord, et de l'ordre d'insertion (Owner, Admin, Member) : une liste triée
    // par Id ne passe pas. Sans ORDER BY, l'ordre des lignes n'est pas défini en SQL ; PostgreSQL
    // les rend dans leur ordre de stockage, qui n'a pas donné l'ordre attendu sur 10 essais.
    [Fact]
    public async Task The_owner_lists_the_members_with_their_id_email_role_and_arrival_date_only()
    {
        var ct = TestContext.Current.CancellationToken;
        await OrganizationWithMembersAsync(ct);
        var org = await OrganizationWithMembersAsync(ct);
        (await UserIdsByMembershipIdAsync(org.Id, ct)).ShouldBe([org.Owner.Id, org.Admin.Id, org.Member.Id]);

        var response = await factory.CreateAuthenticatedClient(org.Id, "Owner", org.Owner.Id)
            .GetAsync($"/api/v1/organizations/{org.Id}/members", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var members = (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).EnumerateArray().ToList();
        members.Select(m => m.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)).ShouldAllBe(
            names => names.SequenceEqual(_exposedFields, StringComparer.Ordinal));
        members.Select(Describe).ShouldBe(
        [
            $"{org.Owner.Id} {org.Owner.Email} Owner {_createdAt:O}",
            $"{org.Member.Id} {org.Member.Email} Member {_createdAt.AddDays(1):O}",
            $"{org.Admin.Id} {org.Admin.Email} Admin {_createdAt.AddDays(2):O}",
        ]);
    }

    [Fact]
    public async Task An_admin_lists_the_members()
    {
        var ct = TestContext.Current.CancellationToken;
        await OrganizationWithMembersAsync(ct);
        var org = await OrganizationWithMembersAsync(ct);

        var response = await factory.CreateAuthenticatedClient(org.Id, "Admin", org.Admin.Id)
            .GetAsync($"/api/v1/organizations/{org.Id}/members", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task A_member_cannot_list_the_members()
    {
        var ct = TestContext.Current.CancellationToken;
        var org = await OrganizationWithMembersAsync(ct);

        var response = await factory.CreateAuthenticatedClient(org.Id, "Member", org.Member.Id)
            .GetAsync($"/api/v1/organizations/{org.Id}/members", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private sealed record Person(Guid Id, string Email);

    private sealed record Org(Guid Id, Person Owner, Person Admin, Person Member);

    // Une organisation réelle, avec de vrais comptes : la liste lit l'e-mail du compte Identity.
    // L'Admin est ajouté avant le Member mais arrive un jour après lui. Chaque adhésion est
    // enregistrée à part, quelques millisecondes après la précédente : leurs Id (UUID v7, ordonnés
    // à la milliseconde) suivent l'ordre d'insertion, Owner, Admin, Member.
    private async Task<Org> OrganizationWithMembersAsync(CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var owner = await CreateUserAsync(scope.ServiceProvider, "owner", ct);
        var admin = await CreateUserAsync(scope.ServiceProvider, "admin", ct);
        var member = await CreateUserAsync(scope.ServiceProvider, "member", ct);
        var organization = Organization.Create(
            $"Org-{Guid.NewGuid():N}", Slug.From($"org-{Guid.NewGuid():N}"), owner.Id, _createdAt);
        scope.ServiceProvider.GetRequiredService<IOrganizationRepository>().Add(organization);
        await uow.SaveChangesAsync(ct);
        await Task.Delay(5, ct);
        organization.AddMember(admin.Id, OrganizationRole.Admin, _createdAt.AddDays(2));
        await uow.SaveChangesAsync(ct);
        await Task.Delay(5, ct);
        organization.AddMember(member.Id, OrganizationRole.Member, _createdAt.AddDays(1));
        await uow.SaveChangesAsync(ct);
        return new Org(organization.Id.Value, owner, admin, member);
    }

    private async Task<List<Guid>> UserIdsByMembershipIdAsync(Guid orgId, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.SqlQuery<Guid>($"""
            SELECT "UserId" AS "Value" FROM "OrganizationMemberships"
            WHERE "OrganizationId" = {orgId}
            ORDER BY "Id"
            """).ToListAsync(ct);
    }

    private static async Task<Person> CreateUserAsync(IServiceProvider services, string prefix, CancellationToken ct)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@saas.local";
        var created = await services.GetRequiredService<IIdentityService>().CreateUserAsync(email, "Sup3rSecret!!42", ct);
        created.Succeeded.ShouldBeTrue();
        return new Person(created.UserId, email);
    }

    private static string Describe(JsonElement member) =>
        $"{member.GetProperty("userId").GetGuid()} {member.GetProperty("email").GetString()} "
        + $"{member.GetProperty("role").GetString()} {member.GetProperty("joinedAt").GetDateTimeOffset():O}";
}
