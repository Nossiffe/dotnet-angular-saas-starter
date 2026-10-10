using ErrorOr;
using NSubstitute;
using Saas.Application.Common.Interfaces;
using Saas.Application.Invitations.AcceptInvitation;
using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;
using static Saas.UnitTests.Application.TenancyTestData;

namespace Saas.UnitTests.Application;

public sealed class InvitationAcceptanceTests
{
    private readonly IOrganizationRepository _organizations = Substitute.For<IOrganizationRepository>();
    private readonly IInvitationRepository _invitations = Substitute.For<IInvitationRepository>();
    private readonly IInvitationTokenService _tokens = Substitute.For<IInvitationTokenService>();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public InvitationAcceptanceTests()
    {
        _clock.UtcNow.Returns(Now);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()!(CancellationToken.None));
    }

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

    // Statut et expiration sont vérifiés avant le mot de passe : un invité sans compte n'a pas à en
    // saisir un pour apprendre que l'invitation n'est plus valable.
    [Fact]
    public async Task An_expired_invitation_is_refused_before_asking_a_new_user_for_a_password()
    {
        var invitation = PendingInvitation(expiresAt: Now.AddMinutes(-1));
        _invitations.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);
        _tokens.VerifyToken("token", invitation.TokenHash).Returns(true);

        var result = await Accept(invitation, "token", password: null);

        result.FirstError.ShouldBe(InvitationErrors.Expired);
        await _identity.DidNotReceiveWithAnyArgs().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_revoked_invitation_is_refused_before_asking_a_new_user_for_a_password()
    {
        var invitation = PendingInvitation();
        invitation.Revoke();
        _invitations.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);
        _tokens.VerifyToken("token", invitation.TokenHash).Returns(true);

        var result = await Accept(invitation, "token", password: null);

        result.FirstError.ShouldBe(InvitationErrors.NotPendingAccept);
        await _identity.DidNotReceiveWithAnyArgs().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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

    private ValueTask<ErrorOr<AcceptInvitationResult>> Accept(Invitation invitation, string token, string? password)
        => new AcceptInvitationHandler(_invitations, _organizations, _identity, _uow, _tokens, _clock)
            .Handle(new AcceptInvitationCommand(invitation.Id.Value, token, password), TestContext.Current.CancellationToken);
}
