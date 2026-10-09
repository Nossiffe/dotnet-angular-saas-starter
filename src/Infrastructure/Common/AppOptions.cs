using System.ComponentModel.DataAnnotations;

namespace Saas.Infrastructure.Common;

/// <summary>Options applicatives — base URL du front pour construire les liens email (confirm/reset).</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    [Required, Url] public string BaseUrl { get; init; } = string.Empty;
}
