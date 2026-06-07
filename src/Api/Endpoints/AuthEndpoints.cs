using Microsoft.AspNetCore.Http.HttpResults;
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

        group.MapPost("/register", RegisterHandler)
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/login", LoginHandler)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshHandler)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<Results<Created<RegisterResponse>, ValidationProblem>> RegisterHandler(
        RegisterRequest req,
        UserManager<ApplicationUser> users,
        IAppDbContext db,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        Results<Created<RegisterResponse>, ValidationProblem>? outcome = null;

        await db.ExecuteInTransactionAsync(async ct2 =>
        {
            var user = new ApplicationUser { UserName = req.Email, Email = req.Email };
            var created = await users.CreateAsync(user, req.Password);
            if (!created.Succeeded)
            {
                outcome = TypedResults.ValidationProblem(
                    created.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }, StringComparer.Ordinal));
                return;
            }

            var org = new Organization { Name = req.Email, Slug = $"org-{user.Id:N}" };
            db.Organizations.Add(org);
            db.Memberships.Add(new OrganizationMembership { OrganizationId = org.Id, UserId = user.Id, Role = "Owner" });
            await db.SaveChangesAsync(ct2);

            outcome = TypedResults.Created($"/users/{user.Id}", new RegisterResponse(user.Id));
        }, ct);

        return outcome!;
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> LoginHandler(
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
            return TypedResults.Unauthorized();
        }

        var check = await signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        var memberships = await db.Memberships.Where(m => m.UserId == user.Id).ToListAsync(ct);
        if (memberships.Count == 0)
        {
            return TypedResults.Unauthorized();
        }

        var membership = req.TenantId is { } requested && memberships.Exists(m => m.OrganizationId == requested)
            ? memberships.First(m => m.OrganizationId == requested)
            : memberships[0];

        var tenantId = membership.OrganizationId;
        var roles = await users.GetRolesAsync(user);
        var (access, accessExp) = tokens.CreateAccessToken(user.Id, user.Email!, tenantId, roles, membership.Role);
        var refreshToken = await refresh.IssueAsync(user.Id, tenantId, ct);

        return TypedResults.Ok(new AuthResponse(access, accessExp, refreshToken));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> RefreshHandler(
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
            return TypedResults.Unauthorized();
        }

        var user = await users.FindByIdAsync(rotated.UserId.ToString());
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var membershipRole = await db.Memberships
            .Where(m => m.UserId == rotated.UserId && m.OrganizationId == rotated.TenantId)
            .Select(m => m.Role)
            .FirstOrDefaultAsync(ct) ?? "Member";

        var roles = await users.GetRolesAsync(user);
        var (access, accessExp) = tokens.CreateAccessToken(user.Id, user.Email!, rotated.TenantId, roles, membershipRole);
        return TypedResults.Ok(new AuthResponse(access, accessExp, rotated.NewRefreshToken));
    }

    public sealed record RegisterRequest(string Email, string Password);
    public sealed record LoginRequest(string Email, string Password, Guid? TenantId);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);
    public sealed record RegisterResponse(Guid Id);
}
