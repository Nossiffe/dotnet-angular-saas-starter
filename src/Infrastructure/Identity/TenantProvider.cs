using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Identity;

public sealed class TenantProvider(IHttpContextAccessor accessor) : ITenantProvider
{
    public Guid TenantId
    {
        get
        {
            var raw = accessor.HttpContext?.User.FindFirstValue("tenant");
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }
}
