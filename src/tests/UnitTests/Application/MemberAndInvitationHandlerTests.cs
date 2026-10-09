using ErrorOr;
using NSubstitute;
using Saas.Application.Common.Interfaces;
using Saas.Application.Invitations.AcceptInvitation;
using Saas.Application.Invitations.ResendInvitation;
using Saas.Application.Invitations.RevokeInvitation;
using Saas.Application.Members.ChangeMemberRole;
using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Application;

public sealed class MemberAndInvitationHandlerTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly IOrganizationRepository _organizations = Substitute.For<IOrganizationRepository>();
    private readonly IInvitationRepository _invitations = Substitute.For<IInvitationRepository>();
    private readonly IInvitationTokenService _tokens = Substitute.For<IInvitationTokenService>();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public MemberAndInvitationHandlerTests()
    {
        _clock.UtcNow.Returns(_now);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()!(CancellationToken.None));
    }

    // ── ChangeMemberRole ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Changing_ones_own_role_is_refused_before_loading_the_organization()
    {
        var userId = Guid.CreateVersion7();
        _currentUser.UserId.Returns(userId.ToString());

        var result = await ChangeRole(Guid.CreateVersion7(), userId, OrganizationRole.Admin);

        result.FirstError.ShouldBe(OrganizationErrors.CannotChangeOwnRole);
        _organizations.ReceivedCalls().ShouldBeEmpty();
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_member_is_promoted_and_the_change_is_saved()
    {
        var (organization, _) = OrganizationWithOwner();
        var memberId = Guid.CreateVersion7();
        organization.AddMember(memberId, OrganizationRole.Member);
        _organizations.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);

        var result = await ChangeRole(organization.Id.Value, memberId, OrganizationRole.Admin);

        result.IsError.ShouldBeFalse();
        organization.Memberships.Single(m => m.UserId == memberId).Role.ShouldBe(OrganizationRole.Admin);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Demoting_the_last_owner_is_refused_and_nothing_is_saved()
    {
        var (organization, ownerId) = OrganizationWithOwner();
        _organizations.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);

        var result = await ChangeRole(organization.Id.Value, ownerId, OrganizationRole.Member);

        result.FirstError.ShouldBe(OrganizationErrors.LastOwner);
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Revoke / Resend ──────────────────────────────────────────────────────────

    // L'organisation de la route est celle du token : une invitation d'une autre organisation est
    // introuvable, même si son identifiant est connu.
    [Fact]
    public async Task An_invitation_of_another_organization_is_not_found()
    {
        var invitation = PendingInvitation();
        _invitations.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);
        var otherOrganization = Guid.CreateVersion7();

        var revoke = await new RevokeInvitationHandler(_invitations, _uow)
            .Handle(new RevokeInvitationCommand(otherOrganization, invitation.Id.Value), TestContext.Current.CancellationToken);
        var resend = await NewResendHandler()
            .Handle(new ResendInvitationCommand(otherOrganization, invitation.Id.Value), TestContext.Current.CancellationToken);

        revoke.FirstError.ShouldBe(InvitationErrors.NotFound);
        resend.FirstError.ShouldBe(InvitationErrors.NotFound);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        invitation.DomainEvents.OfType<InvitationResentDomainEvent>().ShouldBeEmpty();
    }

    // Le nouveau lien part par l'outbox (InvitationResentDomainEvent) ; l'ancien cesse aussitôt
    // d'être valable.
    [Fact]
    public async Task Resending_renews_the_validity_and_drops_the_previous_link()
    {
        var invitation = PendingInvitation();
        _invitations.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);

        var result = await NewResendHandler()
            .Handle(new ResendInvitationCommand(invitation.OrganizationId.Value, invitation.Id.Value), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        invitation.TokenHash.ShouldBeNull();
        invitation.ExpiresAt.ShouldBe(_now + Invitation.Lifetime);
        invitation.DomainEvents.OfType<InvitationResentDomainEvent>().ShouldHaveSingleItem();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_revoked_invitation_cannot_be_revoked_again()
    {
        var invitation = PendingInvitation();
        invitation.Revoke();
        _invitations.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);

        var result = await new RevokeInvitationHandler(_invitations, _uow)
            .Handle(new RevokeInvitationCommand(invitation.OrganizationId.Value, invitation.Id.Value), TestContext.Current.CancellationToken);

        result.FirstError.ShouldBe(InvitationErrors.NotPendingRevoke);
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Accept ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_wrong_token_gets_the_neutral_error_and_changes_nothing()
    {
        var invitation = PendingInvitation();
        _invitations.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);
        _tokens.VerifyToken("wrong", invitation.TokenHash).Returns(false);

        var result = await Accept(invitation, "wrong", password: null);

        result.FirstError.ShouldBe(InvitationErrors.Invalid);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
        await _uow.DidNotReceiveWithAnyArgs().ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_new_user_without_password_is_refused_before_any_change()
    {
        var invitation = PendingInvitation();
        _invitations.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);
        _tokens.VerifyToken("token", invitation.TokenHash).Returns(true);
        _identity.FindByEmailAsync(invitation.InviteeEmail, Arg.Any<CancellationToken>()).Returns((AppUser?)null);

        var result = await Accept(invitation, "token", password: null);

        result.FirstError.ShouldBe(InvitationErrors.PasswordRequired);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
        await _identity.DidNotReceiveWithAnyArgs().CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_existing_user_joins_the_organization_without_a_password()
    {
        var (organization, _) = OrganizationWithOwner();
        var invitation = PendingInvitation(organization.Id);
        var invitee = new AppUser(Guid.CreateVersion7(), invitation.InviteeEmail, EmailConfirmed: true);
        _invitations.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);
        _organizations.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);
        _tokens.VerifyToken("token", invitation.TokenHash).Returns(true);
        _identity.FindByEmailAsync(invitation.InviteeEmail, Arg.Any<CancellationToken>()).Returns(invitee);

        var result = await Accept(invitation, "token", password: null);

        result.Value.IsNewUser.ShouldBeFalse();
        invitation.Status.ShouldBe(InvitationStatus.Accepted);
        organization.Memberships.ShouldContain(m => m.UserId == invitee.Id && m.Role == invitation.Role);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private ValueTask<ErrorOr<Updated>> ChangeRole(Guid organizationId, Guid memberId, OrganizationRole role)
        => new ChangeMemberRoleHandler(_organizations, _uow, _currentUser)
            .Handle(new ChangeMemberRoleCommand(organizationId, memberId, role), TestContext.Current.CancellationToken);

    private ResendInvitationHandler NewResendHandler()
        => new(_invitations, _uow, _clock);

    private ValueTask<ErrorOr<AcceptInvitationResult>> Accept(Invitation invitation, string token, string? password)
        => new AcceptInvitationHandler(_invitations, _organizations, _identity, _uow, _tokens, _clock)
            .Handle(new AcceptInvitationCommand(invitation.Id.Value, token, password), TestContext.Current.CancellationToken);

    private static (Organization Organization, Guid OwnerId) OrganizationWithOwner()
    {
        var ownerId = Guid.CreateVersion7();
        return (Organization.Create("Acme", Slug.From("acme"), ownerId), ownerId);
    }

    // Invitation en attente dont le lien est parti (hash posé par SendInvitationEmailHandler).
    private static Invitation PendingInvitation(OrganizationId? organizationId = null, DateTimeOffset? expiresAt = null)
    {
        var invitation = Invitation.Create(
            organizationId ?? OrganizationId.From(Guid.CreateVersion7()),
            "invitee@saas.local",
            OrganizationRole.Member,
            Guid.CreateVersion7(),
            _now.AddDays(-1),
            expiresAt ?? _now.AddDays(6)).Value;
        invitation.IssueToken("stored-hash", _now.AddDays(-1)).IsError.ShouldBeFalse();
        return invitation;
    }
}
