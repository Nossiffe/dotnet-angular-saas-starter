using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Identity;

// Plan 3 remplacera ce stub par une lecture du tenant dans le JWT.
public sealed class StubTenantProvider : ITenantProvider
{
    public static readonly Guid DevTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid TenantId => DevTenantId;
}
