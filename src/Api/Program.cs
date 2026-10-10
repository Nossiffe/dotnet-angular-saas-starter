using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Saas.Api.Common;
using Saas.Api.Common.OpenApi;
using Saas.Api.Hosting;
using Saas.Application;
using Saas.Application.Common.Behaviors;
using Saas.Infrastructure;
using Saas.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

if (BuildTimeOpenApi.IsRunning)
{
    builder.Configuration.AddPlaceholderConfiguration();
}

builder.AddObservability();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpoints();
builder.Services.AddBackgroundJobServer();

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
// Token périmé (adhésion ou rôle changé) : 401 au lieu de 403, pour que le client rafraîchisse sa session.
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, StaleTokenAuthorizationResultHandler>();
builder.Services.Configure<ExceptionHandlerOptions>(
    // Log d'erreur et métriques d'une exception gérée : pour les 500 seulement (ARCHITECTURE.md § Gestion des erreurs).
    options => options.SuppressDiagnosticsCallback = context =>
        context.HttpContext.Response.StatusCode < StatusCodes.Status500InternalServerError);
builder.Services.AddProblemDetails(options =>
    // Le traceId relie toute réponse d'erreur à ses logs et à ses traces.
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

// Sans volume persistant, un redéploiement invalide les liens déjà envoyés (DEPLOYMENT.md § Data Protection).
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrEmpty(dataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("Saas");
}

builder.AddHttpPipeline();
builder.Services.AddApiRateLimiting();

var app = builder.Build();

await app.MigrateDatabaseInDevelopmentAsync();

app.UseHttpPipeline();
app.MapOperationalEndpoints();
app.MapEndpoints();
app.UseBackgroundJobs();

await app.RunAsync();

// Exposé pour WebApplicationFactory<Program> (tests d'intégration). Le ctor protégé satisfait S1118/CA1052.
public partial class Program
{
    protected Program() { }
}
