using System.Net;
using System.Text.RegularExpressions;

namespace STS2WorkshopUploader.Workshop;

internal static partial class SteamBbCodeToMarkdown
{
    public static string Convert(string bbcode)
    {
        if (string.IsNullOrEmpty(bbcode))
            return string.Empty;

        var text = bbcode.Replace("\r\n", "\n").Replace('\r', '\n');
        text = CodeBlockRegex().Replace(text, "```\n${text}\n```");
        text = HeadingRegex().Replace(text, "# ${text}");
        text = ImageRegex().Replace(text, "![](${url})");
        text = UrlWithLabelRegex().Replace(text, "[${label}](${url})");
        text = UrlRegex().Replace(text, "<${url}>");
        text = BoldRegex().Replace(text, "**${text}**");
        text = ItalicRegex().Replace(text, "*${text}*");
        text = StrikeRegex().Replace(text, "~~${text}~~");
        text = InlineCodeRegex().Replace(text, "`${text}`");
        text = text.Replace("[list]", "", StringComparison.OrdinalIgnoreCase)
            .Replace("[/list]", "", StringComparison.OrdinalIgnoreCase);
        text = ListItemRegex().Replace(text, "- ");
        text = text.Replace("[lb]", "[", StringComparison.OrdinalIgnoreCase)
            .Replace("[rb]", "]", StringComparison.OrdinalIgnoreCase);
        return WebUtility.HtmlDecode(text).Trim();
    }

    [GeneratedRegex(@"\[code\](?<text>.*?)\[/code\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"\[h[1-6]\](?<text>.*?)\[/h[1-6]\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"\[img\](?<url>.*?)\[/img\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[url=(?<url>[^\]]+)\](?<label>.*?)\[/url\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex UrlWithLabelRegex();

    [GeneratedRegex(@"\[url\](?<url>.*?)\[/url\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\[b\](?<text>.*?)\[/b\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"\[i\](?<text>.*?)\[/i\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"\[strike\](?<text>.*?)\[/strike\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StrikeRegex();

    [GeneratedRegex(@"\[code\](?<text>.*?)\[/code\]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\[\*\]")]
    private static partial Regex ListItemRegex();
}