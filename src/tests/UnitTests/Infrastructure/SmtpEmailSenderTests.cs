using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Email;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Infrastructure;

public sealed class SmtpEmailSenderTests
{
    private const string Recipient = "invitee@example.com";

    // Le serveur refuse le destinataire en citant son adresse, comme beaucoup de serveurs SMTP.
    // L'exception relancée remonte jusqu'à OutboxMessages.Error et aux logs : aucune trace de l'adresse.
    [Fact]
    public async Task A_rejection_quoting_the_address_leaves_no_trace_of_it_in_the_rethrown_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = RejectRecipientAsync(listener, ct);
        var sender = new SmtpEmailSender(Options.Create(new EmailOptions
        {
            Host = IPAddress.Loopback.ToString(),
            Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            FromAddress = "no-reply@example.com",
        }));

        var thrown = await Should.ThrowAsync<EmailDeliveryException>(
            () => sender.SendAsync(Recipient, "Invitation", "<p>Bonjour</p>", ct));

        thrown.ToString().ShouldNotContain(Recipient, Case.Insensitive);
        thrown.InnerException.ShouldBeNull();
        thrown.Reason.ShouldBe("SmtpCommandException RecipientNotAccepted, 550 MailboxUnavailable");
        thrown.Message.ShouldBe("Échec de l'envoi de l'e-mail (SmtpCommandException RecipientNotAccepted, 550 MailboxUnavailable).");
        await server;
    }

    private static async Task RejectRecipientAsync(TcpListener listener, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync(ct);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        await writer.WriteLineAsync("220 smtp.test ESMTP");
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var command = line.Split(' ', 2)[0].ToUpperInvariant();
            var reply = command switch
            {
                "EHLO" or "HELO" => "250 smtp.test",
                "MAIL" => "250 2.1.0 Ok",
                "RCPT" => $"550 5.1.1 <{Recipient}>: Recipient address rejected: User unknown",
                "RSET" => "250 2.0.0 Ok",
                "QUIT" => "221 2.0.0 Bye",
                _ => "502 5.5.2 Command not recognized",
            };
            await writer.WriteLineAsync(reply);
            if (string.Equals(command, "QUIT", StringComparison.Ordinal))
            {
                break;
            }
        }
    }
}
