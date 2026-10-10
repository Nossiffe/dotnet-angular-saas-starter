using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Saas.Api.Hosting;
using Saas.Application.Common.Events;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Identity;
using Saas.Infrastructure.Jobs;
using Saas.Infrastructure.Persistence;
using Saas.Infrastructure.Persistence.Outbox;
using Saas.IntegrationTests.Auth;
using Saas.IntegrationTests.Outbox;
using Saas.IntegrationTests.Platform;
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

    /// <summary>Logs émis par l'API pendant les tests de la collection.</summary>
    public CapturingLoggerProvider Logs { get; } = new();

    /// <summary>Mots de passe vérifiés par le hasher de l'application, dans l'ordre.</summary>
    public ConcurrentQueue<string> VerifiedPasswords { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        // La factory migre elle-même via InitializeAsync — désactiver l'auto-migration au démarrage
        // pour éviter un double-migrate et un conflit avec le Testcontainer déjà prêt.
        builder.UseSetting("Database:MigrateOnStartup", "false");

        // Sans limite de débit : tous les appels de TestServer arrivent sans IP, donc dans la même partition.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
            services.Configure<RateLimiterOptions>(options =>
                options.AddPolicy(RateLimitingExtensions.AuthPolicy, _ => RateLimitPartition.GetNoLimiter("tests")));
        });

        // Remplace l'envoi SMTP réel par un fake capturant (recipient + corps/lien).
        // Remplace aussi IPasswordResetEnqueuer par un exécutant synchrone : le job
        // ProcessPasswordResetJob est résolu et exécuté inline, garantissant que
        // CapturingEmailSender a capturé le mail avant la vérification dans le test.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.AddSingleton<ILoggerProvider>(Logs);
            services.RemoveAll<IPasswordHasher<ApplicationUser>>();
            services.AddScoped<IPasswordHasher<ApplicationUser>>(sp => new RecordingPasswordHasher(
                VerifiedPasswords, sp.GetRequiredService<IOptions<PasswordHasherOptions>>()));

            services.AddScoped<ProcessPasswordResetJob>();
            services.RemoveAll<IPasswordResetEnqueuer>();
            services.AddScoped<IPasswordResetEnqueuer, SynchronousPasswordResetEnqueuer>();

            // Pas de lots de l'outbox en arrière-plan : ils prendraient les messages des tests au
            // milieu d'une vérification. Les tests de l'outbox lancent les lots eux-mêmes.
            services.Remove(services.Single(d => d.ImplementationType == typeof(OutboxPollingService)));
            services.AddScoped<IDomainEventHandler<OutboxProbeEvent>, CountingProbeHandler>();
            services.AddScoped<IDomainEventHandler<OutboxProbeEvent>, FailingProbeHandler>();
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(new UnsavableProbeInterceptor()));
        });

        if (UseTestAuth)
        {
            // Le schéma de test devient le schéma par défaut : les politiques de l'application
            // (DependencyInjection.AddAuthorization) s'appliquent telles quelles, sans copie ici.
            builder.ConfigureTestServices(services =>
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
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
