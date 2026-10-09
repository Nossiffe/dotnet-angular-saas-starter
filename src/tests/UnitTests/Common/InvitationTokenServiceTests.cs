using Saas.Infrastructure.Identity;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Common;

public sealed class InvitationTokenServiceTests
{
    private readonly InvitationTokenService _tokens = new();

    [Fact]
    public void The_token_matches_its_own_hash()
    {
        var raw = _tokens.GenerateRawToken();

        _tokens.VerifyToken(raw, _tokens.HashToken(raw)).ShouldBeTrue();
    }

    [Fact]
    public void Another_token_does_not_match()
    {
        var hash = _tokens.HashToken(_tokens.GenerateRawToken());

        _tokens.VerifyToken(_tokens.GenerateRawToken(), hash).ShouldBeFalse();
    }

    // Aucun hash stocké (avant le premier envoi, ou après un renvoi) : aucun lien n'est valable,
    // et la vérification ne lève pas d'exception.
    [Fact]
    public void No_token_matches_a_missing_hash()
        => _tokens.VerifyToken(_tokens.GenerateRawToken(), storedHash: null).ShouldBeFalse();
}
