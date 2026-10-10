using System.Globalization;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;

namespace Saas.Api.Hosting;

/// <summary>Logs Serilog, traces et métriques OpenTelemetry (ARCHITECTURE.md § Résilience &amp; observabilité).</summary>
internal static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Host.UseSerilog(
            (context, config) =>
                config.ReadFrom.Configuration(context.Configuration)
                      .Enrich.FromLogContext()
                      .Enrich.WithSpan()
                      .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture),
            preserveStaticLogger: true,
            writeToProviders: true);

        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "saas-api"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        // Seul un signal activé est exporté : les logs partent par OTLP quand un endpoint est configuré.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            openTelemetry.WithLogging().UseOtlpExporter();
        }

        return builder;
    }
}
