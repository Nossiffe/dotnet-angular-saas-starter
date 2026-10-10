using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Saas.Application.Common.Interfaces;
using Saas.Application.Members.RemoveMember;
using Saas.Application.Tenancy.MemberRemoved;
using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;
using static Saas.UnitTests.Application.TenancyTestData;

namespace Saas.UnitTests.Application;

public sealed class MemberRemovalTests
{
    private readonly IOrganizationRepository _organizations = Substitute.For<IOrganizationRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task A_member_is_removed_and_the_change_is_saved()
    {
        var (organization, _) = OrganizationWithOwner();
        var memberId = Guid.CreateVersion7();
        organization.AddMember(memberId, OrganizationRole.Member, Now);
        _organizations.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);

        var result = await Remove(organization.Id.Value, memberId);

        result.IsError.ShouldBeFalse();
        organization.Memberships.ShouldNotContain(m => m.UserId == memberId);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Removing_an_owner_is_refused_and_nothing_is_saved()
    {
        var (organization, ownerId) = OrganizationWithOwner();
        _organizations.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);

        var result = await Remove(organization.Id.Value, ownerId);

        result.FirstError.ShouldBe(OrganizationErrors.CannotRemoveOwner);
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_organization_returns_not_found()
    {
        var result = await Remove(Guid.CreateVersion7(), Guid.CreateVersion7());

        result.FirstError.ShouldBe(OrganizationErrors.NotFound);
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // Après le retrait, l'outbox révoque les refresh tokens du membre sur cette organisation, et
    // seulement sur celle-ci.
    [Fact]
    public async Task The_removal_event_revokes_the_member_sessions_on_this_organization()
    {
        var refreshTokens = Substitute.For<IRefreshTokenStore>();
        var organizationId = OrganizationId.From(Guid.CreateVersion7());
        var memberId = Guid.CreateVersion7();

        await new MemberRemovedHandler(refreshTokens, NullLogger<MemberRemovedHandler>.Instance).HandleAsync(
            new MemberRemovedDomainEvent(organizationId, memberId, OrganizationRole.Member),
            TestContext.Current.CancellationToken);

        await refreshTokens.Received(1).RevokeTenantSessionsAsync(memberId, organizationId.Value, Arg.Any<CancellationToken>());
        await refreshTokens.DidNotReceiveWithAnyArgs().RevokeAllAsync(Guid.Empty, CancellationToken.None);
    }

    private ValueTask<ErrorOr<Deleted>> Remove(Guid organizationId, Guid memberId)
        => new RemoveMemberHandler(_organizations, _uow)
            .Handle(new RemoveMemberCommand(organizationId, memberId), TestContext.Current.CancellationToken);
}
