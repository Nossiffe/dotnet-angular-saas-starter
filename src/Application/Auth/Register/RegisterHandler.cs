using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Auth.Register;

public sealed class RegisterHandler(
    IOrganizationRepository organizations,
    IUnitOfWork uow,
    IIdentityService identity,
    IEmailSender email,
    IEmailLinkBuilder links,
    ILogger<RegisterHandler> logger)
    : ICommandHandler<RegisterCommand, ErrorOr<Guid>>
{
    private static readonly Action<ILogger, Guid, Exception?> _logConfirmEmailFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(1, "ConfirmEmailSendFailed"),
            "Échec de l'envoi de l'email de confirmation pour {UserId}");

    public async ValueTask<ErrorOr<Guid>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Guid userId = Guid.Empty;

        // Choix délibéré : user Identity + Organization + membership Owner dans UNE seule transaction
        // (all-or-nothing → pas d'utilisateur orphelin sans organisation). En cas de perte d'ACK très rare
        // pendant le commit, l'execution strategy peut rejouer le délégué et l'email déjà pris renvoie une
        // erreur de validation trompeuse — mais les données restent cohérentes (compte fonctionnel). C'est
        // l'« Option 1 » documentée par EF sur l'idempotence au commit, acceptable ici (cf. ADR persistance).
        await uow.ExecuteInTransactionAsync(async ct2 =>
        {
            var result = await identity.CreateUserAsync(command.Email, command.Password, ct2);
            if (!result.Succeeded)
            {
                var failures = result.Errors
                    .SelectMany(kv => kv.Value.Select(v => new ValidationFailure(kv.Key, v)));
                throw new ValidationException(failures);
            }

            userId = result.UserId;

            var organization = Organization.Create(
                command.Email,
                Slug.From($"org-{userId:N}"),
                userId);
            organizations.Add(organization);

            await uow.SaveChangesAsync(ct2);
        }, cancellationToken);

        // Hors transaction : génère le token de confirmation + envoie l'email.
        // Ne doit jamais faire échouer l'inscription si SMTP est indisponible (try/catch + log).
        await SendConfirmationEmailAsync(userId, command.Email, cancellationToken);

        return userId;
    }

    private async Task SendConfirmationEmailAsync(Guid userId, string recipient, CancellationToken cancellationToken)
    {
        try
        {
            var token = await identity.GenerateEmailConfirmationTokenAsync(userId, cancellationToken);
            if (token is null)
            {
                return;
            }

            var link = links.ConfirmEmailLink(userId, token);
            var body = $"""
                <p>Bienvenue ! Merci de confirmer votre adresse email pour activer votre compte.</p>
                <p><a href="{link}">Confirmer mon email</a></p>
                """;
            await email.SendAsync(recipient, "Confirmez votre adresse email", body, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logConfirmEmailFailed(logger, userId, ex);
        }
    }
}
