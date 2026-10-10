using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

namespace Saas.Api.Hosting;

/// <summary>Middlewares HTTP dans leur ordre d'exécution, les services qu'ils utilisent et les endpoints d'exploitation.</summary>
internal static class HttpPipelineExtensions
{
    private const string SpaCorsPolicy = "spa";

    public static WebApplicationBuilder AddHttpPipeline(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

        builder.Services.AddCors(options =>
            options.AddPolicy(SpaCorsPolicy, policy =>
                policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                      .AllowAnyHeader().AllowAnyMethod().AllowCredentials()
                      .SetPreflightMaxAge(TimeSpan.FromHours(1))));

        // Deux ans, hors Development ; le preload reste un choix explicite (DEPLOYMENT.md § HTTPS / TLS).
        builder.Services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(730);
            options.IncludeSubDomains = true;
        });

        // Filet global ; un endpoint long s'en exempte avec [DisableRequestTimeout].
        var timeoutSeconds = builder.Configuration.GetValue("RequestTimeouts:DefaultSeconds", 30);
        builder.Services.AddRequestTimeouts(options =>
            options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(timeoutSeconds) });

        return builder;
    }

    public static WebApplication UseHttpPipeline(this WebApplication app)
    {
        // Avant le gestionnaire d'exceptions : le journal voit le statut final, pas l'exception en transit.
        app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());
        app.UseExceptionHandler();
        app.UseSecurityHeaders(new HeaderPolicyCollection().AddDefaultApiSecurityHeaders());

        // Avant HSTS et la redirection HTTPS, qui doivent voir le scheme du client (DEPLOYMENT.md § ForwardedHeaders).
        if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
        {
            app.UseForwardedHeaders(ProxyForwardedHeaders());
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseRouting();
        app.UseRequestTimeouts();
        app.UseCors(SpaCorsPolicy);
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        return app;
    }

    /// <summary>Document OpenAPI en Development et sondes de santé (DEPLOYMENT.md § Health probes).</summary>
    public static WebApplication MapOperationalEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();
        app.MapHealthChecks("/health").AllowAnonymous();
        return app;
    }

    // Le proxy n'est pas forcément en loopback : la confiance repose sur ForwardedHeaders:Enabled (DEPLOYMENT.md).
    private static ForwardedHeadersOptions ProxyForwardedHeaders()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
        };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        return options;
    }
}
