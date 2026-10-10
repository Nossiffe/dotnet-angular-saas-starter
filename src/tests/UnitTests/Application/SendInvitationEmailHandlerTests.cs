using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Saas.Application.Common.Interfaces;
using Saas.Application.Invitations.SendInvitationEmail;
using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Application;

public sealed class SendInvitationEmailHandlerTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly IInvitationRepository _invitations = Substitute.For<IInvitationRepository>();
    private readonly IInvitationTokenService _tokens = Substitute.For<IInvitationTokenService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEmailLinkBuilder _links = Substitute.For<IEmailLinkBuilder>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public SendInvitationEmailHandlerTests()
    {
        _clock.UtcNow.Returns(_now);
        _tokens.GenerateRawToken().Returns("raw-token");
        _tokens.HashToken("raw-token").Returns("token-hash");
        _links.AcceptInvitationLink(Arg.Any<Guid>(), "raw-token").Returns("https://app.test/accept-invitation?token=raw-token");
    }

    // Le hash est enregistré avant l'envoi : un lien qui part est valable au moment où il part.
    [Fact]
    public async Task The_token_hash_is_saved_before_the_email_leaves()
    {
        var invitation = Pending();

        await Handler().HandleAsync(Invited(invitation), TestContext.Current.CancellationToken);

        invitation.TokenHash.ShouldBe("token-hash");
        Received.InOrder(() =>
        {
            _ = _uow.SaveChangesAsync(Arg.Any<CancellationToken>());
            _ = _email.SendAsync(
                invitation.InviteeEmail,
                Arg.Any<string>(),
                Arg.Is<string>(body => body.Contains("https://app.test/accept-invitation?token=raw-token", StringComparison.Ordinal)),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task A_resent_invitation_gets_a_new_link_the_same_way()
    {
        var invitation = Pending();
        invitation.Refresh(_now.AddDays(7));

        await Handler().HandleAsync(
            new InvitationResentDomainEvent(invitation.Id, invitation.OrganizationId), TestContext.Current.CancellationToken);

        invitation.TokenHash.ShouldBe("token-hash");
        await _email.Received(1).SendAsync(
            invitation.InviteeEmail, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_revoked_invitation_gets_no_email()
    {
        var invitation = Pending();
        invitation.Revoke();

        await Handler().HandleAsync(Invited(invitation), TestContext.Current.CancellationToken);

        invitation.TokenHash.ShouldBeNull();
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_expired_invitation_gets_no_email()
    {
        var invitation = Pending(expiresAt: _now);

        await Handler().HandleAsync(Invited(invitation), TestContext.Current.CancellationToken);

        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_deleted_invitation_gets_no_email()
    {
        var invitation = Pending();
        _invitations.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns((Invitation?)null);

        await Handler().HandleAsync(Invited(invitation), TestContext.Current.CancellationToken);

        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // L'échec remonte à l'outbox, qui reprendra le message : rien ne l'avale ici.
    [Fact]
    public async Task An_SMTP_failure_fails_the_handler()
    {
        var invitation = Pending();
        _email.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP indisponible")));

        await Should.ThrowAsync<InvalidOperationException>(
            () => Handler().HandleAsync(Invited(invitation), TestContext.Current.CancellationToken).AsTask());
    }

    // Un conflit de concurrence à l'enregistrement du hash : aucun e-mail ne part avec un lien
    // qui ne serait pas valable.
    [Fact]
    public async Task A_failed_save_sends_no_email()
    {
        var invitation = Pending();
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("conflit"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => Handler().HandleAsync(Invited(invitation), TestContext.Current.CancellationToken).AsTask());

        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private SendInvitationEmailHandler Handler()
        => new(_invitations, _tokens, _uow, _links, _email, _clock, NullLogger<SendInvitationEmailHandler>.Instance);

    private static MemberInvitedDomainEvent Invited(Invitation invitation)
        => new(invitation.Id, invitation.OrganizationId, invitation.InviteeEmail);

    private Invitation Pending(DateTimeOffset? expiresAt = null)
    {
        var invitation = Invitation.Create(
            OrganizationId.From(Guid.CreateVersion7()),
            "invitee@saas.local",
            OrganizationRole.Member,
            Guid.CreateVersion7(),
            _now.AddDays(-1),
            expiresAt ?? _now.AddDays(6)).Value;
        _invitations.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);
        return invitation;
    }
}
