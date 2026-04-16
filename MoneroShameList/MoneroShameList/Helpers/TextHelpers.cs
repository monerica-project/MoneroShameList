using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace MoneroShameList.Helpers;

public static class TextHelpers
{
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s\)\]""<>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static HtmlString Linkify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return HtmlString.Empty;

        // HTML-encode the raw text first, then replace encoded URLs with links
        var encoded = System.Web.HttpUtility.HtmlEncode(text);

        var result = UrlRegex.Replace(encoded, match =>
        {
            var url = match.Value;
            var display = url.Length > 60 ? url[..57] + "…" : url;
            return $"""<a href="{url}" target="_blank" rel="noopener nofollow">{display}</a>""";
        });

        // Preserve line breaks
        result = result.Replace("\r\n", "<br>").Replace("\n", "<br>");

        return new HtmlString(result);
    }
}