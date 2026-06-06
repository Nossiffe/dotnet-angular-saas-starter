using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Identity;

public sealed class StubCurrentUser : ICurrentUser
{
    public string? UserId => "system";
    public bool IsAuthenticated => false;
}
