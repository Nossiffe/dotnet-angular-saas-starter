using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Platform;

[Collection("api")]
public sealed class ErrorLoggingTests(PostgresApiFactory factory)
{
    private const string RequestLogCategory = "Serilog.AspNetCore.RequestLoggingMiddleware";

    // Une erreur de validation est journalisée une fois, en 400, sans log d'erreur. Le test ne lit
    // que les logs de sa requête, reconnus à leur trace : une tâche de fond peut écrire en même temps.
    [Fact]
    public async Task A_validation_error_is_logged_as_a_400_without_any_error_log()
    {
        var ct = TestContext.Current.CancellationToken;
        var traceId = ActivityTraceId.CreateRandom();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/products")
        {
            Content = JsonContent.Create(new { name = "", price = -1m }),
        };
        request.Headers.Add("traceparent", $"00-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-01");

        var response = await factory.CreateClient().SendAsync(request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var entries = await WaitForRequestLogAsync(traceId, ct);
        entries.ShouldContain(e => string.Equals(e.Category, RequestLogCategory, StringComparison.Ordinal)
            && e.Level == LogLevel.Information
            && e.Message.Contains("responded 400", StringComparison.Ordinal));
        entries.Where(e => e.Level >= LogLevel.Error).ShouldBeEmpty();
    }

    // Le journal des requêtes est écrit à la fin du pipeline, parfois juste après la réponse.
    private async Task<List<CapturingLoggerProvider.LogEntry>> WaitForRequestLogAsync(
        ActivityTraceId traceId, CancellationToken ct)
    {
        List<CapturingLoggerProvider.LogEntry> entries = [];
        for (var attempt = 0; attempt < 50; attempt++)
        {
            entries = [.. factory.Logs.Entries.Where(e => e.TraceId == traceId)];
            if (entries.Exists(e => string.Equals(e.Category, RequestLogCategory, StringComparison.Ordinal)))
            {
                break;
            }

            await Task.Delay(100, ct);
        }

        return entries;
    }
}
