using ErrorOr;
using NSubstitute;
using Saas.Application.Auth;
using Saas.Application.Auth.ResetPassword;
using Saas.Application.Common.Interfaces;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Application;

public sealed class PasswordResetSessionTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IRefreshTokenStore _refresh = Substitute.For<IRefreshTokenStore>();

    [Fact]
    public async Task A_successful_reset_revokes_every_open_session()
    {
        var userId = Guid.CreateVersion7();
        _identity.ResetPasswordAsync(userId, "token", "N3w-password!", Arg.Any<CancellationToken>()).Returns(true);

        var result = await Reset(userId);

        result.IsError.ShouldBeFalse();
        await _refresh.Received(1).RevokeAllAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_invalid_reset_link_changes_no_session()
    {
        var userId = Guid.CreateVersion7();
        _identity.ResetPasswordAsync(userId, "token", "N3w-password!", Arg.Any<CancellationToken>()).Returns(false);

        var result = await Reset(userId);

        result.FirstError.ShouldBe(AuthErrors.InvalidResetLink);
        await _refresh.DidNotReceiveWithAnyArgs().RevokeAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private ValueTask<ErrorOr<Success>> Reset(Guid userId)
        => new ResetPasswordHandler(_identity, _refresh)
            .Handle(new ResetPasswordCommand(userId, "token", "N3w-password!"), TestContext.Current.CancellationToken);
}
