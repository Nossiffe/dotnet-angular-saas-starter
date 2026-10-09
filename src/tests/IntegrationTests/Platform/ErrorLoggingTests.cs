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

    // Une requête invalide produisait trois logs d'erreur, dont un « 500 » du journal des requêtes,
    // alors que le client recevait un 400. Les tests de la collection s'exécutent l'un après
    // l'autre : les logs ajoutés pendant ce test viennent de cette requête.
    [Fact]
    public async Task A_validation_error_is_logged_as_a_400_without_any_error_log()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var before = factory.Logs.Entries.Count;

        var response = await client.PostAsJsonAsync("/api/v1/products", new { name = "", price = -1m }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var entries = await WaitForRequestLogAsync(before, ct);
        entries.ShouldContain(e => string.Equals(e.Category, RequestLogCategory, StringComparison.Ordinal)
            && e.Level == LogLevel.Information
            && e.Message.Contains("responded 400", StringComparison.Ordinal));
        entries.Where(e => e.Level >= LogLevel.Error).ShouldBeEmpty();
    }

    // Le journal des requêtes est écrit à la fin du pipeline, parfois juste après la réponse.
    private async Task<List<CapturingLoggerProvider.LogEntry>> WaitForRequestLogAsync(int before, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var entries = factory.Logs.Entries.Skip(before).ToList();
            if (entries.Exists(e => string.Equals(e.Category, RequestLogCategory, StringComparison.Ordinal)))
            {
                return entries;
            }

            await Task.Delay(100, ct);
        }

        return factory.Logs.Entries.Skip(before).ToList();
    }
}
