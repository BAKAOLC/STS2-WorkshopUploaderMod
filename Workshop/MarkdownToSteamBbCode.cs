using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace STS2WorkshopUploader.Workshop;

internal static partial class MarkdownToSteamBbCode
{
    public static string Convert(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var output = new StringBuilder(normalized.Length);
        var inCodeBlock = false;
        var inList = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inList)
                {
                    output.AppendLine("[/list]");
                    inList = false;
                }

                output.AppendLine(inCodeBlock ? "[/code]" : "[code]");
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
            {
                output.AppendLine(Escape(line));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (inList)
                {
                    output.AppendLine("[/list]");
                    inList = false;
                }

                output.AppendLine();
                continue;
            }

            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                if (inList)
                {
                    output.AppendLine("[/list]");
                    inList = false;
                }

                output.Append("[h1]");
                output.Append(ConvertInline(heading.Groups["text"].Value.Trim()));
                output.AppendLine("[/h1]");
                continue;
            }

            var listItem = ListRegex().Match(line);
            if (listItem.Success)
            {
                if (!inList)
                {
                    output.AppendLine("[list]");
                    inList = true;
                }

                output.Append("[*]");
                output.AppendLine(ConvertInline(listItem.Groups["text"].Value.Trim()));
                continue;
            }

            if (inList)
            {
                output.AppendLine("[/list]");
                inList = false;
            }

            output.AppendLine(ConvertInline(line.Trim()));
        }

        if (inCodeBlock)
            output.AppendLine("[/code]");
        if (inList)
            output.AppendLine("[/list]");

        return output.ToString().Trim();
    }

    private static string ConvertInline(string text)
    {
        var escaped = ImageRegex().Replace(text, match =>
        {
            var url = Escape(match.Groups["url"].Value);
            return $"[img]{url}[/img]";
        });
        escaped = LinkRegex().Replace(escaped, match =>
        {
            var label = Escape(match.Groups["label"].Value);
            var url = Escape(match.Groups["url"].Value);
            return $"[url={url}]{label}[/url]";
        });
        escaped = BoldRegex().Replace(escaped, match => $"[b]{Escape(match.Groups["text"].Value)}[/b]");
        escaped = ItalicRegex().Replace(escaped, match => $"[i]{Escape(match.Groups["text"].Value)}[/i]");
        escaped = StrikeRegex().Replace(escaped, match => $"[strike]{Escape(match.Groups["text"].Value)}[/strike]");
        escaped = InlineCodeRegex().Replace(escaped, match => $"[code]{Escape(match.Groups["text"].Value)}[/code]");
        escaped = EscapeUnconvertedSegments(escaped);
        return escaped;
    }

    private static string EscapeUnconvertedSegments(string text)
    {
        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '[')
            {
                var close = text.IndexOf(']', i);
                if (close >= 0)
                {
                    builder.Append(text, i, close - i + 1);
                    i = close + 1;
                    continue;
                }
            }

            builder.Append(Escape(text[i].ToString()));
            i++;
        }

        return builder.ToString();
    }

    private static string Escape(string text)
    {
        return WebUtility.HtmlDecode(text)
            .Replace("[", "[lb]", StringComparison.Ordinal)
            .Replace("]", "[rb]", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^(?<marks>#{1,6})\s+(?<text>.+)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*[-*+]\s+(?<text>.+)$")]
    private static partial Regex ListRegex();

    [GeneratedRegex(@"!\[(?<label>[^\]]*)\]\((?<url>[^)]+)\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[(?<label>[^\]]+)\]\((?<url>[^)]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\*\*(?<text>.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*(?<text>[^*]+)\*(?!\*)")]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"~~(?<text>.+?)~~")]
    private static partial Regex StrikeRegex();

    [GeneratedRegex(@"`(?<text>[^`]+)`")]
    private static partial Regex InlineCodeRegex();
}