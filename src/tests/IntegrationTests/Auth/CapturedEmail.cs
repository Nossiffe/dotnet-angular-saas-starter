using System.Text.RegularExpressions;

namespace Saas.IntegrationTests.Auth;

public sealed partial record CapturedEmail(string Recipient, string Subject, string HtmlBody)
{
    /// <summary>Extrait userId + token depuis le lien (href=...?userId=&amp;token=) du corps HTML.</summary>
    public (Guid UserId, string Token) ExtractLink()
    {
        var userId = QueryParamRegex("userId").Match(HtmlBody).Groups[1].Value;
        var token = QueryParamRegex("token").Match(HtmlBody).Groups[1].Value;
        return (Guid.Parse(userId), Uri.UnescapeDataString(token));
    }

    private static Regex QueryParamRegex(string name)
        => new($@"[?&]{name}=([^&""\s]+)", RegexOptions.None, TimeSpan.FromSeconds(1));
}
