using System.Reflection;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Jobs;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Auth;

// La réinitialisation reste sur Hangfire : la demande n'écrit rien en base, il n'y a pas de
// transaction où accrocher l'outbox.
[Collection("api")]
public sealed class PasswordResetJobTests(PostgresApiFactory factory)
{
    // Le job échoue pour que Hangfire le reprenne. Ni notre log, ni l'exception que Hangfire journalise
    // et conserve ne contiennent l'adresse, même quand le message de l'erreur SMTP la cite.
    [Fact]
    public async Task An_SMTP_failure_fails_the_job_and_the_log_keeps_only_the_user_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = $"reset-smtp-{Guid.NewGuid():N}@saas.local";
        await AuthTestFlow.RegisterAndConfirmAsync(factory.CreateClient(), factory, email, "Reset!User42", ct);
        var (userId, _) = factory.Emails.For(email).ShouldNotBeNull().ExtractLink();
        factory.Emails.FailNext(email);

        await using var scope = factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<ProcessPasswordResetJob>();
        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => job.RunAsync(email));
        thrown.ToString().ShouldNotContain(email, Case.Insensitive);
        thrown.Message.ShouldContain(userId.ToString());

        var logged = factory.Logs.Entries
            .Where(e => e.Category.EndsWith(nameof(ProcessPasswordResetJob), StringComparison.Ordinal)
                && e.Message.Contains(userId.ToString(), StringComparison.Ordinal))
            .ShouldHaveSingleItem();
        logged.Level.ShouldBe(LogLevel.Error);
        logged.Message.ShouldNotContain(email, Case.Insensitive);
    }

    // La raison d'une EmailDeliveryException ne cite pas l'adresse : le log et l'exception la gardent.
    [Fact]
    public async Task The_reason_of_a_delivery_failure_is_kept_in_the_log_and_the_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = $"reset-reason-{Guid.NewGuid():N}@saas.local";
        await AuthTestFlow.RegisterAndConfirmAsync(factory.CreateClient(), factory, email, "Reset!User42", ct);
        var (userId, _) = factory.Emails.For(email).ShouldNotBeNull().ExtractLink();
        const string reason = "SmtpCommandException RecipientNotAccepted, 550 MailboxUnavailable";
        factory.Emails.FailNext(email, error: new EmailDeliveryException(reason));

        await using var scope = factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<ProcessPasswordResetJob>();
        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => job.RunAsync(email));

        thrown.Message.ShouldContain(reason);
        factory.Logs.Entries
            .Where(e => e.Category.EndsWith(nameof(ProcessPasswordResetJob), StringComparison.Ordinal)
                && e.Message.Contains(userId.ToString(), StringComparison.Ordinal))
            .ShouldHaveSingleItem()
            .Message.ShouldContain(reason);
    }

    // Trois nouvelles tentatives après la première, puis l'état Deleted, qui expire après 24 h :
    // l'adresse e-mail, argument du job, ne reste pas indéfiniment dans les tables Hangfire.
    [Fact]
    public void Hangfire_retries_the_job_three_times_then_deletes_it()
    {
        var retry = typeof(ProcessPasswordResetJob)
            .GetMethod(nameof(ProcessPasswordResetJob.RunAsync))!
            .GetCustomAttribute<AutomaticRetryAttribute>()
            .ShouldNotBeNull();

        retry.Attempts.ShouldBe(3);
        retry.OnAttemptsExceeded.ShouldBe(AttemptsExceededAction.Delete);
    }
}
