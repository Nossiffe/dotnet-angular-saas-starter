using System.Threading.RateLimiting;
using Asp.Versioning;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Saas.Api.Common;
using Saas.Application;
using Saas.Application.Common.Behaviors;
using Saas.Infrastructure;
using Saas.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpoints();

builder.Services.AddMediator(options =>
{
    options.Assemblies =
    [
        typeof(Saas.Application.DependencyInjection).Assembly,
        typeof(Saas.Domain.Common.IDomainEvent).Assembly,
    ];
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
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "db");

AddCors(builder);
AddRateLimiter(builder);
AddOpenTelemetry(builder);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSecurityHeaders(new HeaderPolicyCollection().AddDefaultApiSecurityHeaders());
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseCors("spa");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/db", new HealthCheckOptions
{
    Predicate = check => string.Equals(check.Name, "db", StringComparison.Ordinal),
}).AllowAnonymous();

app.MapEndpoints();

// Dashboard Hangfire — servi par Hangfire (navigateur), auth via filtre dédié (pas la fallback JWT).
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new Saas.Api.Common.HangfireDashboardAuthFilter(app.Environment)],
});

// Recurring jobs (purge quotidienne des refresh tokens). Hangfire crée ses tables au 1er run.
Saas.Infrastructure.DependencyInjection.RegisterRecurringJobs(app.Services);

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
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        {
            if (http.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip))
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
        // de 10/30s déclenche des 429 parasites sur les flux auth multi-appels. Un flag de config
        // (RateLimiting:DisableAuthPolicy=true, posé uniquement par la factory de test) neutralise la limite.
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
    IOpenTelemetryBuilder otelBuilder = b.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName))
        .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation());
    if (!string.IsNullOrWhiteSpace(b.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    {
        otelBuilder.UseOtlpExporter();
    }
}

// Exposé pour WebApplicationFactory<Program> (tests d'intégration). Le ctor protégé satisfait S1118/CA1052.
public partial class Program
{
    protected Program() { }
}
