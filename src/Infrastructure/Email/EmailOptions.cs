using System.ComponentModel.DataAnnotations;

namespace Saas.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required] public string Host { get; init; } = "localhost";
    [Range(1, 65535)] public int Port { get; init; } = 1025; // Mailpit SMTP en dev
    [Required] public string FromAddress { get; init; } = "no-reply@saas.local";
    public string FromName { get; init; } = "Saas";
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public bool UseStartTls { get; init; }
}
