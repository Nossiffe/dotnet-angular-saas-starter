using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Common;

namespace Saas.Infrastructure.Email;

public sealed class EmailLinkBuilder(IOptions<AppOptions> options) : IEmailLinkBuilder
{
    private readonly string _baseUrl = options.Value.BaseUrl.TrimEnd('/');

    public string ConfirmEmailLink(Guid userId, string token)
        => Build("confirm-email", userId, token);

    public string ResetPasswordLink(Guid userId, string token)
        => Build("reset-password", userId, token);

    private string Build(string path, Guid userId, string token)
    {
        var query = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["userId"] = userId.ToString(),
            ["token"] = token,
        };
        return QueryHelpers.AddQueryString($"{_baseUrl}/{path}", query);
    }
}
