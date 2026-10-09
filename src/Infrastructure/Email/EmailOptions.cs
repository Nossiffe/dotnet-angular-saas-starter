using System.ComponentModel.DataAnnotations;

namespace Saas.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required] public string Host { get; init; } = string.Empty;
    [Range(1, 65535)] public int Port { get; init; }
    [Required] public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "Saas";
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public bool UseStartTls { get; init; }
    public bool UseSslOnConnect { get; init; }
}
