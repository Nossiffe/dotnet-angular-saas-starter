using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Email;

/// <summary>Envoi SMTP via MailKit. En dev, pointe Mailpit (host=localhost, port=1025, sans TLS/auth).</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _o = options.Value;

    public async Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        try
        {
            await DeliverAsync(recipient, subject, htmlBody, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Le message d'une erreur SMTP peut citer l'adresse : il finirait dans l'outbox et les logs.
#pragma warning disable MA0054 // exception d'origine volontairement omise : son message peut citer l'adresse
            throw new EmailDeliveryException(Describe(ex));
#pragma warning restore MA0054
        }
    }

    // Le type de l'erreur, avec ses codes quand il en a : jamais son texte.
    private static string Describe(Exception ex) => ex switch
    {
        SmtpCommandException command => $"{nameof(SmtpCommandException)} {command.ErrorCode}, {(int)command.StatusCode} {command.StatusCode}",
        SocketException socket => $"{nameof(SocketException)} {socket.SocketErrorCode}",
        _ => ex.GetType().Name,
    };

    private async Task DeliverAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_o.FromName, _o.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        SecureSocketOptions socketOptions;
        if (_o.UseSslOnConnect)
        {
            socketOptions = SecureSocketOptions.SslOnConnect;
        }
        else if (_o.UseStartTls)
        {
            socketOptions = SecureSocketOptions.StartTls;
        }
        else
        {
            socketOptions = SecureSocketOptions.None;
        }

        await client.ConnectAsync(_o.Host, _o.Port, socketOptions, cancellationToken);
        if (!string.IsNullOrEmpty(_o.UserName))
        {
            await client.AuthenticateAsync(_o.UserName, _o.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
