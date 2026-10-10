using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Api.Contracts;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;
using Saas.Infrastructure.Persistence;
using Saas.IntegrationTests.Outbox;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Invitations;

// L'e-mail d'invitation part par l'outbox : le test lance les lots lui-même.
[Collection("api")]
public sealed class InvitationEmailTests(PostgresApiFactory factory)
{
    private const string Password = "Invited!User42";

    // Au-delà de ce délai, le premier nouvel essai de l'outbox est dû (1 minute).
    private static readonly TimeSpan _outboxRetryDelay = TimeSpan.FromMinutes(2);

    private readonly Guid _ownerId = Guid.CreateVersion7();

    [Fact]
    public async Task The_email_leaves_with_the_outbox_batch_and_its_link_is_accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, email, _) = await InviteAsync(UniqueEmail("sent"), ct);
        factory.Emails.For(email).ShouldBeNull(); // rien ne part avant le lot

        await OutboxRunner.RunUntilIdleAsync(factory, ct);

        var (invitationId, token) = factory.Emails.For(email).ShouldNotBeNull().ExtractInvitationLink();
        (await AcceptAsync(invitationId, token, Password, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Seul le hash du jeton est écrit : ni l'outbox, ni Hangfire, ni l'invitation ne gardent le jeton
    // envoyé. La recherche porte sur toutes les tables des schémas de l'application et de Hangfire.
    [Fact]
    public async Task No_raw_token_is_stored_in_the_database()
    {
        var ct = TestContext.Current.CancellationToken;
        var (orgId, email, invitationId) = await InviteAsync(UniqueEmail("no-raw-token"), ct);
        await OutboxRunner.RunUntilIdleAsync(factory, ct);
        var (_, firstToken) = factory.Emails.For(email).ShouldNotBeNull().ExtractInvitationLink();
        (await ResendAsync(orgId, invitationId, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await OutboxRunner.RunUntilIdleAsync(factory, ct);
        var (_, secondToken) = factory.Emails.For(email).ShouldNotBeNull().ExtractInvitationLink();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tables = await db.Database.SqlQuery<string>(
                $"""
                SELECT format('%I.%I', table_schema, table_name) AS "Value"
                FROM information_schema.tables
                WHERE table_schema IN ('public', 'hangfire') AND table_type = 'BASE TABLE'
                """)
            .ToListAsync(ct);
        tables.ShouldContain("public.\"OutboxMessages\"", StringComparer.Ordinal);
        tables.ShouldContain("hangfire.job", StringComparer.Ordinal);
        foreach (var token in new[] { firstToken, secondToken })
        {
            foreach (var table in tables)
            {
                // Nom de table lu dans information_schema, déjà échappé par format('%I.%I').
                var sql = "SELECT count(*)::int AS \"Value\" FROM " + table + " t WHERE strpos(t::text, {0}) > 0";
                var rows = await db.Database.SqlQueryRaw<int>(sql, token).SingleAsync(ct);
                rows.ShouldBe(0, $"jeton brut trouvé dans {table}");
            }
        }

        var invitation = await db.Invitations.SingleAsync(i => i.Id == InvitationId.From(invitationId), ct);
        invitation.TokenHash.ShouldBe(scope.ServiceProvider.GetRequiredService<IInvitationTokenService>().HashToken(secondToken));
    }

    // Le handler échoue, l'outbox le reprend au lot suivant ; le lien de l'e-mail qui part est valable.
    [Fact]
    public async Task After_an_SMTP_failure_the_next_batch_sends_a_valid_link()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new TestClock();
        var email = UniqueEmail("smtp-retry");
        factory.Emails.FailNext(email);
        await InviteAsync(email, ct);

        await OutboxRunner.RunUntilIdleAsync(factory, clock, ct);
        factory.Emails.For(email).ShouldBeNull();

        clock.Advance(_outboxRetryDelay);
        await OutboxRunner.RunUntilIdleAsync(factory, clock, ct);

        var (invitationId, token) = factory.Emails.For(email).ShouldNotBeNull().ExtractInvitationLink();
        (await AcceptAsync(invitationId, token, Password, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_invitation_revoked_before_the_batch_gets_no_email()
    {
        var ct = TestContext.Current.CancellationToken;
        var (orgId, email, invitationId) = await InviteAsync(UniqueEmail("revoked-early"), ct);
        (await Owner(orgId).DeleteAsync($"/api/v1/organizations/{orgId}/invitations/{invitationId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await OutboxRunner.RunUntilIdleAsync(factory, ct);

        factory.Emails.For(email).ShouldBeNull();
    }

    // Le renvoi efface le hash : l'ancien lien est refusé aussitôt, avant même le lot, avec l'erreur
    // neutre et non une erreur serveur.
    [Fact]
    public async Task Right_after_a_resend_the_previous_link_gets_the_neutral_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var (orgId, email, invitationId) = await InviteAsync(UniqueEmail("resend-old-link"), ct);
        await OutboxRunner.RunUntilIdleAsync(factory, ct);
        var (_, previousToken) = factory.Emails.For(email).ShouldNotBeNull().ExtractInvitationLink();

        (await ResendAsync(orgId, invitationId, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var accept = await AcceptAsync(invitationId, previousToken, Password, ct);

        accept.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(accept, ct)).ShouldBe("Invitation.Invalid");
    }

    [Fact]
    public async Task After_a_resend_and_its_batch_the_previous_link_is_refused_and_the_new_one_accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (orgId, email, invitationId) = await InviteAsync(UniqueEmail("resend-new-link"), ct);
        await OutboxRunner.RunUntilIdleAsync(factory, ct);
        var (_, previousToken) = factory.Emails.For(email).ShouldNotBeNull().ExtractInvitationLink();
        (await ResendAsync(orgId, invitationId, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await OutboxRunner.RunUntilIdleAsync(factory, ct);
        var (_, newToken) = factory.Emails.For(email).ShouldNotBeNull().ExtractInvitationLink();

        newToken.ShouldNotBe(previousToken, StringComparer.Ordinal);
        (await AcceptAsync(invitationId, previousToken, Password, ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AcceptAsync(invitationId, newToken, Password, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Statut et expiration sont vérifiés avant le mot de passe : un invité sans compte apprend tout
    // de suite que l'invitation n'est plus valable, sans saisir de mot de passe.
    [Theory]
    [InlineData("expired", "Invitation.Expired")]
    [InlineData("revoked", "Invitation.NotPending")]
    public async Task An_invitation_no_longer_valid_is_refused_without_asking_a_new_user_for_a_password(
        string state, string expectedCode)
    {
        var ct = TestContext.Current.CancellationToken;
        var (orgId, email, invitationId) = await InviteAsync(UniqueEmail($"no-password-{state}"), ct);
        await OutboxRunner.RunUntilIdleAsync(factory, ct);
        var (_, token) = factory.Emails.For(email).ShouldNotBeNull().ExtractInvitationLink();
        if (string.Equals(state, "expired", StringComparison.Ordinal))
        {
            await ExpireAsync(invitationId, ct);
        }
        else
        {
            (await Owner(orgId).DeleteAsync($"/api/v1/organizations/{orgId}/invitations/{invitationId}", ct))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var accept = await AcceptAsync(invitationId, token, password: null, ct);

        accept.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(accept, ct)).ShouldBe(expectedCode);
    }

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@saas.local";

    private HttpClient Owner(Guid orgId) => InvitationTestFlow.ClientFor(factory, orgId, _ownerId);

    // Une organisation, et une invitation vers un e-mail sans compte. L'e-mail ne part qu'avec un lot.
    private async Task<(Guid OrgId, string Email, Guid InvitationId)> InviteAsync(string email, CancellationToken ct)
    {
        var orgId = await InvitationTestFlow.SeedOrganizationAsync(factory, _ownerId, ct);
        var invite = await Owner(orgId).PostAsJsonAsync(
            $"/api/v1/organizations/{orgId}/invitations", new { email, role = "Member" }, ct);
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await invite.Content.ReadFromJsonAsync<InviteResponse>(ct)).ShouldNotBeNull();
        return (orgId, email, created.InvitationId);
    }

    private Task<HttpResponseMessage> ResendAsync(Guid orgId, Guid invitationId, CancellationToken ct)
        => Owner(orgId).PostAsync($"/api/v1/organizations/{orgId}/invitations/{invitationId}/resend", content: null, ct);

    private Task<HttpResponseMessage> AcceptAsync(Guid invitationId, string token, string? password, CancellationToken ct)
        => factory.CreateClient().PostAsJsonAsync("/api/v1/invitations/accept", new { invitationId, token, password }, ct);

    private async Task ExpireAsync(Guid invitationId, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Database.ExecuteSqlAsync(
            $"""UPDATE "Invitations" SET "ExpiresAt" = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE "Id" = {invitationId}""",
            ct);
        rows.ShouldBe(1);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString();
}
