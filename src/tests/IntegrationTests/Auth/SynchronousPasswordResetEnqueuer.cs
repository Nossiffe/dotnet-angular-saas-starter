using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Jobs;

namespace Saas.IntegrationTests.Auth;

/// <summary>
/// Remplaçant de test de IPasswordResetEnqueuer : exécute ProcessPasswordResetJob
/// synchronement dans le même scope DI, de sorte que CapturingEmailSender capture
/// le mail avant que le test n'aille lire factory.Emails.For(email).
/// </summary>
internal sealed class SynchronousPasswordResetEnqueuer(IServiceProvider sp) : IPasswordResetEnqueuer
{
    public void Enqueue(string email)
    {
        using var scope = sp.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<ProcessPasswordResetJob>();
        // Exécution synchrone intentionnelle (context de test — pas de deadlock possible).
        job.RunAsync(email).GetAwaiter().GetResult();
    }
}
