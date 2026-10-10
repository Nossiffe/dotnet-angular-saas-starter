using System.Security.Claims;
using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Common;
using Saas.Infrastructure.Email;
using Saas.Infrastructure.Identity;
using Saas.Infrastructure.Jobs;
using Saas.Infrastructure.Persistence;
using Saas.Infrastructure.Persistence.Interceptors;
using Saas.Infrastructure.Persistence.Outbox;
using Saas.Infrastructure.Persistence.Repositories;

namespace Saas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        AddPersistence(services, configuration);
        AddIdentity(services);
        AddJwt(services, configuration);
        AddAuthorization(services);
        AddBackgroundJobs(services, configuration);
        AddEmail(services, configuration);

        return services;
    }

    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailLinkBuilder, EmailLinkBuilder>();
    }

    private static void AddBackgroundJobs(IServiceCollection services, IConfiguration configuration)
    {
        // Hangfire crée ses propres tables Postgres au démarrage (pas de migration EF).
        // API version-sensible (Hangfire.PostgreSql 1.20+) : UsePostgreSqlStorage(c => c.UseNpgsqlConnection(...)).
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(opt =>
                opt.UseNpgsqlConnection(configuration.GetConnectionString("Default"))));
        // Le serveur qui exécute les jobs est ajouté par l'hôte (Api), qui sait s'il doit tourner.

        services.AddScoped<PurgeExpiredRefreshTokensJob>();
        services.AddScoped<PurgeProcessedOutboxMessagesJob>();
        services.AddScoped<ProcessPasswordResetJob>();

        // Queue d'enfilement Hangfire de la réinitialisation (abstraction Application-pure).
        services.AddScoped<IPasswordResetEnqueuer, PasswordResetRequestQueue>();
    }

    // Enregistre les recurring jobs (types internes à l'Infrastructure).
    // Appelé par l'hôte après Build() — IRecurringJobManager n'existe qu'au runtime.
    public static void RegisterRecurringJobs(IServiceProvider services)
    {
        var manager = services.GetRequiredService<IRecurringJobManager>();
        manager.AddOrUpdate<PurgeExpiredRefreshTokensJob>(
            "purge-expired-refresh-tokens",
            job => job.RunAsync(),
            Cron.Daily());
        manager.AddOrUpdate<PurgeProcessedOutboxMessagesJob>(
            "purge-processed-outbox-messages",
            job => job.RunAsync(),
            Cron.Daily());
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default est requis (variable d'environnement ConnectionStrings__Default).");
        }

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<DomainEventsInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null));
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DomainEventsInterceptor>());
        });
        services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();

        // Transmet les domain events de l'outbox à leurs handlers (voir OutboxProcessor).
        services.AddSingleton<OutboxProcessor>();
        services.AddHostedService<OutboxPollingService>();

        // Repositories (write) + Unit of Work — pas de DbSet exposé à l'Application.
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                // Lockout : défauts Identity rendus explicites (contrat testé par LockoutTests).
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders(); // requis pour GenerateEmailConfirmationToken / GeneratePasswordResetToken
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<DummyPasswordCheck>();
        // OWASP Password Storage Cheat Sheet : PBKDF2-SHA512 >= 210k itérations
        // (défaut Identity = 100k — insuffisant). Les hashes existants sont re-hashés
        // automatiquement au prochain login réussi (SuccessRehashNeeded).
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
        // Service stateless de tokens d'invitation (raw base64url + hash SHA-256).
        services.AddSingleton<IInvitationTokenService, InvitationTokenService>();
    }

    private static void AddJwt(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = ClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
    }

    private static void AddAuthorization(IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthorizationPolicies.HasTenant, p => p.RequireAssertion(ctx =>
            {
                var v = ctx.User.FindFirst("tenant")?.Value;
                return Guid.TryParse(v, out var id) && id != Guid.Empty;
            }))
            .AddPolicy(AuthorizationPolicies.TenantAdmin, p => p.RequireClaim("tenant_role", "Owner", "Admin"))
            .AddPolicy(AuthorizationPolicies.TenantOwner, p => p.RequireClaim("tenant_role", "Owner"));
    }
}
