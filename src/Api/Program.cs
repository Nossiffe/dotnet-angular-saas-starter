using System.Threading.RateLimiting;
using Asp.Versioning;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Saas.Api.Common;
using Saas.Api.Common.OpenApi;
using Saas.Application;
using Saas.Application.Common.Behaviors;
using Saas.Infrastructure;
using Saas.Infrastructure.Persistence;
using Serilog;
using Serilog.Enrichers.Span;

var builder = WebApplication.CreateBuilder(args);

// Serilog écrit sur la console et transmet aussi chaque événement aux providers enregistrés
// (writeToProviders) : sans cela, le provider OpenTelemetry ne reçoit aucun log et l'export OTLP
// des logs reste vide. Les providers par défaut sont retirés pour ne pas doubler la console.
// preserveStaticLogger : chaque hôte garde son propre logger au lieu de passer par Log.Logger,
// statique et donc partagé par tous les hôtes du processus (les tests en lancent plusieurs).
builder.Logging.ClearProviders();
builder.Host.UseSerilog(
    (context, config) =>
        config.ReadFrom.Configuration(context.Configuration)
              .Enrich.FromLogContext()
              .Enrich.WithSpan() // TraceId/SpanId depuis Activity.Current → corrélation logs ↔ traces OTel
              .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture),
    preserveStaticLogger: true,
    writeToProviders: true);

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

// Génération OpenAPI au build (outil GetDocument.Insider) : il lance ce Program.cs avec un
// serveur mock, donc tout le démarrage s'exécute. Sans DB ni config réelle au build, on
// fournit une DSN factice (enregistrement DbContext paresseux, aucune connexion ouverte) et
// on désactive l'auto-migration. No-op en `dotnet run`/`dotnet test` (entry assembly ≠ outil).
var isBuildTimeOpenApi = string.Equals(
    System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name,
    "GetDocument.Insider",
    System.StringComparison.Ordinal);
if (isBuildTimeOpenApi)
{
    // Le générateur de doc exécute le démarrage complet (dont ValidateOnStart). On fournit des
    // valeurs placeholder pour que les options requises valident, sans config ni DB réelles.
    // DSN sans mot de passe : enregistrement DbContext paresseux, aucune connexion ouverte.
    builder.Configuration["ConnectionStrings:Default"] = "Host=build;Database=build;Username=build";
    builder.Configuration["Database:MigrateOnStartup"] = "false";
    builder.Configuration["Jwt:Issuer"] = "build";
    builder.Configuration["Jwt:Audience"] = "build";
    builder.Configuration["Jwt:SigningKey"] = new string('x', 32); // satisfait [MinLength(32)] sans littéral « secret »
    builder.Configuration["App:BaseUrl"] = "https://build.local";
    builder.Configuration["Email:Host"] = "build";
    builder.Configuration["Email:Port"] = "25";
    builder.Configuration["Email:FromAddress"] = "build@build.local";
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpoints();

builder.Services.AddMediator(options =>
{
    options.Assemblies = [typeof(Saas.Application.DependencyInjection).Assembly];
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors =
    [
        typeof(LoggingBehavior<,>),
        typeof(ValidationBehavior<,>),
    ];
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.Configure<Microsoft.AspNetCore.Builder.ExceptionHandlerOptions>(
    // .NET 10 supprime par défaut les diagnostics (log d'erreur, métriques) d'une exception gérée
    // par un IExceptionHandler. On les garde pour les 500, et seulement pour eux : une erreur de
    // validation transformée en 400 n'est pas une erreur serveur.
    options => options.SuppressDiagnosticsCallback = context =>
        context.HttpContext.Response.StatusCode < StatusCodes.Status500InternalServerError);
builder.Services.AddProblemDetails(options =>
    // Injecte traceId dans tous les ProblemDetails (validation, status-code pages, etc.),
    // pas seulement ceux du GlobalExceptionHandler — pour la corrélation logs/traces OTel.
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<BearerSecuritySchemeTransformer>();
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "db", tags: ["ready"]);

// Clés Data Protection (tokens email Identity) : éphémères par défaut en conteneur →
// tout redéploiement invaliderait confirmations/resets en cours. En prod, monter un
// volume et pointer DataProtection__KeysPath dessus (DEPLOYMENT.md).
var dpPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrEmpty(dpPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dpPath))
        .SetApplicationName("Saas");
}

// HSTS 2 ans (recommandation OWASP) : appliqué par le UseHsts() existant hors Development.
// Preload non activé par défaut : engagement quasi irréversible (listes
// navigateurs), à faire opt-in une fois le domaine confirmé stable en HTTPS partout.
builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(730);
    o.IncludeSubDomains = true;
    // o.Preload = true; // opt-in : ne décommenter qu'après avoir soumis le domaine à hstspreload.org
});

// Request timeouts : filet global (config RequestTimeouts:DefaultSeconds, défaut 30 s).
// Ne se déclenche pas sous debugger. Un endpoint long légitime s'exempte avec
// [DisableRequestTimeout] ou une policy nommée.
var timeoutSeconds = builder.Configuration.GetValue("RequestTimeouts:DefaultSeconds", 30);
builder.Services.AddRequestTimeouts(options =>
    options.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(timeoutSeconds),
    });

// Output cache : câblé sans policy par défaut — opt-in strict par endpoint
// (.CacheOutput(...)). Multi-tenant : ne jamais l'appliquer à un endpoint
// authentifié/tenant-scoped sans VaryBy explicite sur le tenant — le défaut
// (les requêtes authentifiées ne sont pas cachées) protège, ne pas le contourner.
builder.Services.AddOutputCache();

AddCors(builder);
AddRateLimiter(builder);
AddOpenTelemetry(builder);

var app = builder.Build();

// Auto-migration au démarrage uniquement en Development (confort dotnet run).
// En prod : utiliser le migration bundle (pas d'auto-migrate en multi-instance).
// Guard double : IsDevelopment() + Database:MigrateOnStartup (les tests le désactivent via UseSetting).
if (app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var strategy = db.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () => await db.Database.MigrateAsync());
}

// Journal des requêtes avant le gestionnaire d'exceptions : il voit le statut final (400, 409…)
// et non l'exception en transit, qu'il journaliserait comme un 500. Il écrit dans le logger de
// cet hôte plutôt que dans le logger statique de Serilog, partagé entre hôtes (tests).
app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());
app.UseExceptionHandler();
app.UseSecurityHeaders(new HeaderPolicyCollection().AddDefaultApiSecurityHeaders());

// Derrière un reverse-proxy (nginx/Traefik/ALB), Kestrel ne voit que la connexion interne
// (souvent HTTP, IP du proxy) : sans réécriture, UseHsts (redirection interne) et le
// `Secure` du cookie refresh évalueraient le mauvais scheme. UseForwardedHeaders doit donc
// s'exécuter avant UseHsts/UseHttpsRedirection pour que HttpContext.Request.Scheme/
// Connection.RemoteIpAddress reflètent le client réel. Config-gated (faux par défaut) :
// à activer uniquement quand un proxy de confiance est effectivement devant l'API (sinon
// un client pourrait usurper X-Forwarded-* directement).
if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };
    // Les défauts (KnownIPNetworks/KnownProxies = loopback uniquement) ignorent silencieusement
    // X-Forwarded-* dès que le proxy n'est pas sur la même machine (cas courant : nginx dans un
    // conteneur séparé, IP interne du réseau docker) — UseForwardedHeaders ne ferait alors rien,
    // sans erreur.
    // On vide donc ces listes : la validation est déléguée au fait que ce bloc est déjà
    // config-gated (ForwardedHeaders:Enabled, alias ASPNETCORE_FORWARDEDHEADERS_ENABLED) —
    // à n'activer que derrière un proxy de confiance qui écrase ces en-têtes pour les requêtes
    // externes. Conséquence d'un mauvais déploiement (flag actif + port API joignable en
    // direct) : X-Forwarded-For forgeable → partitionnement IP du rate limiting faussé.
    // Le port de l'API ne doit alors être joignable que depuis le réseau du proxy (DEPLOYMENT.md).
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    forwardedHeadersOptions.ForwardLimit = 1;
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseRouting();
// Ordre officiel (learn.microsoft.com/aspnet/core/performance/timeouts) : après UseRouting,
// avant l'exécution des endpoints — pour couvrir le temps passé dans le endpoint lui-même.
app.UseRequestTimeouts();
app.UseCors("spa");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
// Après UseRouting, UseCors, UseAuthentication et UseAuthorization : sinon le cache peut servir
// à un utilisateur une réponse mise en cache pour un autre
// (learn.microsoft.com/aspnet/core/performance/caching/output).
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Health probes : /health/live (liveness — process up, aucun check) + /health/ready (readiness — DB)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // aucun check : vivant si le process répond
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"), // vérifie les dépendances (DB)
}).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous(); // tous les checks (commodité)

app.MapEndpoints();

// Dashboard Hangfire — servi par Hangfire (navigateur), auth via filtre dédié (pas la fallback JWT).
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new Saas.Api.Common.HangfireDashboardAuthFilter(app.Environment)],
});

// Jobs récurrents (purges quotidiennes des refresh tokens et de l'outbox). Hangfire crée ses tables au 1er run.
// Sauté pendant la génération OpenAPI au build (sinon AddOrUpdate ouvre une connexion Postgres).
if (!isBuildTimeOpenApi)
{
    Saas.Infrastructure.DependencyInjection.RegisterRecurringJobs(app.Services);
}

await app.RunAsync();

static void AddCors(WebApplicationBuilder b)
{
    b.Services.AddCors(options =>
        options.AddPolicy("spa", policy =>
            policy.WithOrigins(b.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                  .AllowAnyHeader().AllowAnyMethod().AllowCredentials()
                  .SetPreflightMaxAge(TimeSpan.FromHours(1))));
}

static void AddRateLimiter(WebApplicationBuilder b)
{
    b.Services.AddRateLimiter(options =>
    {
        options.OnRejected = (context, _) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return ValueTask.CompletedTask;
        };
        // En tests d'intégration, tous les appels passent par TestServer avec une IP nulle (non loopback)
        // → ils tombent tous dans la même partition globale et la fenêtre de 100/min finit par déclencher
        // des 429 parasites quand la suite grossit. RateLimiting:DisableGlobalLimiter (posé uniquement par
        // la factory de test) neutralise le limiteur global.
        var disableGlobalLimit = b.Environment.IsDevelopment()
            && b.Configuration.GetValue<bool>("RateLimiting:DisableGlobalLimiter");
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        {
            if (disableGlobalLimit
                || (http.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip)))
            {
                return RateLimitPartition.GetNoLimiter("loopback");
            }

            var key = http.User.Identity?.IsAuthenticated == true
                ? $"user:{http.User.Identity!.Name}"
                : $"ip:{http.Connection.RemoteIpAddress}";
            return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
            });
        });
        // En tests d'intégration, tous les appels partagent la même IP ("unknown") → la fenêtre fixe
        // de 10/30s déclenche des 429 parasites sur les flux auth multi-appels. RateLimiting:DisableAuthPolicy
        // (posé uniquement par la factory de test) neutralise la politique "auth" uniquement.
        var disableAuthLimit = b.Environment.IsDevelopment()
            && b.Configuration.GetValue<bool>("RateLimiting:DisableAuthPolicy");
        options.AddPolicy("auth", http =>
            disableAuthLimit
                ? RateLimitPartition.GetNoLimiter("auth-disabled")
                : RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromSeconds(30), QueueLimit = 0 }));
    });
}

static void AddOpenTelemetry(WebApplicationBuilder b)
{
    var serviceName = b.Configuration["OTEL_SERVICE_NAME"] ?? "saas-api";
    var otlpEnabled = !string.IsNullOrWhiteSpace(b.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    IOpenTelemetryBuilder otelBuilder = b.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddNpgsql()) // spans SQL via l'instrumentation native Npgsql (Npgsql.OpenTelemetry)
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation());

    // Active le signal "logging" uniquement quand l'export OTLP est configuré : sinon UseOtlpExporter
    // n'exporterait aucun log (seul un signal activé est exporté). En dev sans OTLP, on garde Serilog/console.
    if (otlpEnabled)
    {
        otelBuilder.WithLogging();
        otelBuilder.UseOtlpExporter();
    }
}

// Exposé pour WebApplicationFactory<Program> (tests d'intégration). Le ctor protégé satisfait S1118/CA1052.
public partial class Program
{
    protected Program() { }
}
