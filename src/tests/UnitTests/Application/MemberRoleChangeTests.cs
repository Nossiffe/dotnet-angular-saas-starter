using ErrorOr;
using NSubstitute;
using Saas.Application.Common.Interfaces;
using Saas.Application.Members.ChangeMemberRole;
using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;
using static Saas.UnitTests.Application.TenancyTestData;

namespace Saas.UnitTests.Application;

public sealed class MemberRoleChangeTests
{
    private readonly IOrganizationRepository _organizations = Substitute.For<IOrganizationRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

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

    private ValueTask<ErrorOr<Updated>> ChangeRole(Guid organizationId, Guid memberId, OrganizationRole role)
        => new ChangeMemberRoleHandler(_organizations, _uow, _currentUser)
            .Handle(new ChangeMemberRoleCommand(organizationId, memberId, role), TestContext.Current.CancellationToken);
}
