using NSubstitute;
using Saas.Application.Common.Interfaces;
using Saas.Application.Invitations.ResendInvitation;
using Saas.Application.Invitations.RevokeInvitation;
using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;
using static Saas.UnitTests.Application.TenancyTestData;

namespace Saas.UnitTests.Application;

public sealed class InvitationRevocationAndResendTests
{
    private readonly IInvitationRepository _invitations = Substitute.For<IInvitationRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public InvitationRevocationAndResendTests() => _clock.UtcNow.Returns(Now);

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
        invitation.ExpiresAt.ShouldBe(Now + Invitation.Lifetime);
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

    private ResendInvitationHandler NewResendHandler()
        => new(_invitations, _uow, _clock);
}
