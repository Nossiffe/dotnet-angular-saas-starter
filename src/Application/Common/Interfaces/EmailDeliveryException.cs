namespace Saas.Application.Common.Interfaces;

/// <summary>
/// Échec d'un envoi d'e-mail, levé par <see cref="IEmailSender"/>. <see cref="Reason"/> ne cite
/// jamais le destinataire : elle peut être journalisée et conservée telle quelle.
/// </summary>
public sealed class EmailDeliveryException(string reason) : Exception($"Échec de l'envoi de l'e-mail ({reason}).")
{
    /// <summary>Type de l'erreur et ses codes : code et statut d'un refus du serveur, code d'une erreur réseau.</summary>
    public string Reason { get; } = reason;
}
