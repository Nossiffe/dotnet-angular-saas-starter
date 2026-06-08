using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Jobs;
using Saas.Infrastructure.Persistence;
using Saas.IntegrationTests.Auth;
using Saas.IntegrationTests.Invitations;
using Testcontainers.PostgreSql;
using Xunit;

namespace Saas.IntegrationTests;

public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("saas")
        .WithUsername("saas")
        .WithPassword("saas")
        .Build();

    protected virtual bool UseTestAuth => true;

    /// <summary>Fake IEmailSender capturant les emails envoyés — utilisé pour extraire userId/token en test.</summary>
    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        // La factory migre elle-même via InitializeAsync — désactiver l'auto-migration au démarrage
        // pour éviter un double-migrate et un conflit avec le Testcontainer déjà prêt.
        builder.UseSetting("Database:MigrateOnStartup", "false");

        // Neutralise le rate-limiter "auth" (clé par IP "unknown" partagée par tous les appels
        // TestServer → 429 parasites sur les flux auth multi-appels). Flag lu par Program.cs.
        builder.UseSetting("RateLimiting:DisableAuthPolicy", "true");

        // Remplace l'envoi SMTP réel par un fake capturant (recipient + corps/lien).
        // Remplace aussi IPasswordResetEnqueuer par un exécutant synchrone : le job
        // ProcessPasswordResetJob est résolu et exécuté inline, garantissant que
        // CapturingEmailSender a capturé le mail avant la vérification dans le test.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);

            services.AddScoped<ProcessPasswordResetJob>();
            services.RemoveAll<IPasswordResetEnqueuer>();
            services.AddScoped<IPasswordResetEnqueuer, SynchronousPasswordResetEnqueuer>();

            services.AddScoped<ProcessInvitationEmailJob>();
            services.RemoveAll<IInvitationEmailEnqueuer>();
            services.AddScoped<IInvitationEmailEnqueuer, SynchronousInvitationEmailEnqueuer>();
        });

        if (UseTestAuth)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.AddAuthorizationBuilder()
                    .SetFallbackPolicy(
                        new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName).RequireAuthenticatedUser().Build())
                    .SetDefaultPolicy(
                        new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName).RequireAuthenticatedUser().Build())
                    .AddPolicy("HasTenant", p => p.RequireAssertion(ctx =>
                    {
                        var v = ctx.User.FindFirst("tenant")?.Value;
                        return Guid.TryParse(v, out var id) && id != Guid.Empty;
                    }))
                    .AddPolicy("TenantAdmin", p => p.RequireClaim("tenant_role", "Owner"));
            });
        }
    }

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ctx.Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
