using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Identity;
using Saas.Domain.Tenancy;

namespace Saas.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/auth").WithTags("Auth").AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/register", RegisterHandler);
        group.MapPost("/login", LoginHandler);
        group.MapPost("/refresh", RefreshHandler);

        return app;
    }

    private static async Task<IResult> RegisterHandler(
        RegisterRequest req,
        UserManager<ApplicationUser> users,
        IAppDbContext db,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        await using var tx = await db.BeginTransactionAsync(ct);

        var user = new ApplicationUser { UserName = req.Email, Email = req.Email };
        var created = await users.CreateAsync(user, req.Password);
        if (!created.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return Results.ValidationProblem(
                created.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }, StringComparer.Ordinal));
        }

        var org = new Organization { Name = req.Email, Slug = $"org-{user.Id:N}" };
        db.Organizations.Add(org);
        db.Memberships.Add(new OrganizationMembership { OrganizationId = org.Id, UserId = user.Id, Role = "Owner" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Created($"/users/{user.Id}", new { user.Id });
    }

    private static async Task<IResult> LoginHandler(
        LoginRequest req,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IAppDbContext db,
        ITokenService tokens,
        IRefreshTokenStore refresh,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var user = await users.FindByEmailAsync(req.Email);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var check = await signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            return Results.Unauthorized();
        }

        var memberships = await db.Memberships.Where(m => m.UserId == user.Id).ToListAsync(ct);
        if (memberships.Count == 0)
        {
            return Results.Unauthorized();
        }

        var membership = req.TenantId is { } requested && memberships.Exists(m => m.OrganizationId == requested)
            ? memberships.First(m => m.OrganizationId == requested)
            : memberships[0];

        var tenantId = membership.OrganizationId;
        var roles = await users.GetRolesAsync(user);
        var (access, accessExp) = tokens.CreateAccessToken(user.Id, user.Email!, tenantId, roles, membership.Role);
        var refreshToken = await refresh.IssueAsync(user.Id, tenantId, ct);

        return Results.Ok(new AuthResponse(access, accessExp, refreshToken));
    }

    private static async Task<IResult> RefreshHandler(
        RefreshRequest req,
        UserManager<ApplicationUser> users,
        IAppDbContext db,
        ITokenService tokens,
        IRefreshTokenStore refresh,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var rotated = await refresh.ValidateAndRotateAsync(req.RefreshToken, ct);
        if (rotated is null)
        {
            return Results.Unauthorized();
        }

        var user = await users.FindByIdAsync(rotated.UserId.ToString());
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var membershipRole = await db.Memberships
            .Where(m => m.UserId == rotated.UserId && m.OrganizationId == rotated.TenantId)
            .Select(m => m.Role)
            .FirstOrDefaultAsync(ct) ?? "Member";

        var roles = await users.GetRolesAsync(user);
        var (access, accessExp) = tokens.CreateAccessToken(user.Id, user.Email!, rotated.TenantId, roles, membershipRole);
        return Results.Ok(new AuthResponse(access, accessExp, rotated.NewRefreshToken));
    }

    public sealed record RegisterRequest(string Email, string Password);
    public sealed record LoginRequest(string Email, string Password, Guid? TenantId);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);
}
