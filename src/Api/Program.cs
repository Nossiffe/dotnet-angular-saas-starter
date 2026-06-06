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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/db", new HealthCheckOptions
{
    Predicate = check => string.Equals(check.Name, "db", StringComparison.Ordinal),
});

app.MapProductEndpoints();

await app.RunAsync();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program
{
    // Intentionally left empty - used as marker for WebApplicationFactory
    protected Program() { }
}
