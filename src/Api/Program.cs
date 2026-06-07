using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Saas.Api.Common;
using Saas.Api.Endpoints;
using Saas.Application;
using Saas.Application.Common.Behaviors;
using Saas.Infrastructure;
using Saas.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "db");

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseAuthentication();
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

app.MapAuthEndpoints();
app.MapProductEndpoints();

await app.RunAsync();

// Exposé pour WebApplicationFactory<Program> (tests d'intégration). Le ctor protégé satisfait S1118/CA1052.
public partial class Program
{
    protected Program() { }
}
