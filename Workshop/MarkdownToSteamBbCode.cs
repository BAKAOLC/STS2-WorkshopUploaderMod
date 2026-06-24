using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace STS2WorkshopUploader.Workshop;

internal static class MarkdownToSteamBbCode
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly HashSet<string> KnownTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "b",
        "i",
        "u",
        "strike",
        "code",
        "quote",
        "url",
        "img",
        "h1",
        "h2",
        "h3",
        "h4",
        "h5",
        "h6",
        "list",
        "olist",
        "table",
        "tr",
        "th",
        "td"
    };

    public static string Convert(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        if (ContainsKnownBbCode(markdown))
            return NormalizeLineEndings(markdown).Trim();

        var document = Markdown.Parse(markdown, Pipeline);
        return NormalizeLineEndings(RenderBlocks(document)).Trim();
    }

    private static string RenderBlocks(ContainerBlock container)
    {
        List<string> rendered = [];

        foreach (var block in container)
        {
            var text = RenderBlock(block).Trim();
            if (text.Length > 0)
                rendered.Add(text);
        }

        return string.Join("\n\n", rendered);
    }

    private static string RenderBlock(Block block)
    {
        return block switch
        {
            HeadingBlock heading => RenderHeading(heading),
            ParagraphBlock paragraph => RenderInline(paragraph.Inline),
            ListBlock list => RenderList(list),
            QuoteBlock quote => RenderQuote(quote),
            FencedCodeBlock code => RenderCodeBlock(code),
            CodeBlock code => RenderCodeBlock(code),
            ThematicBreakBlock => "----",
            Table table => RenderTable(table),
            HtmlBlock html => EscapeText(html.Lines.ToString()),
            LeafBlock leaf => EscapeText(leaf.Lines.ToString()),
            ContainerBlock container => RenderBlocks(container),
            _ => string.Empty
        };
    }

    private static string RenderHeading(HeadingBlock heading)
    {
        var tag = heading.Level switch
        {
            1 => "h1",
            2 => "h2",
            _ => "h3"
        };

        return $"[{tag}]{RenderInline(heading.Inline).Trim()}[/{tag}]";
    }

    private static string RenderList(ListBlock list)
    {
        var tag = list.IsOrdered ? "olist" : "list";
        var builder = new StringBuilder();
        builder.Append('[').Append(tag).AppendLine("]");

        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem)
                continue;

            var text = RenderListItem(listItem);
            if (text.Length > 0)
                builder.Append("[*]").AppendLine(text);
        }

        builder.Append("[/").Append(tag).Append(']');
        return builder.ToString();
    }

    private static string RenderListItem(ListItemBlock listItem)
    {
        List<string> parts = [];

        foreach (var child in listItem)
        {
            var text = RenderBlock(child).Trim();
            if (text.Length > 0)
                parts.Add(text);
        }

        return string.Join("\n", parts);
    }

    private static string RenderQuote(QuoteBlock quote)
    {
        var content = RenderBlocks(quote).Trim();
        return content.Length == 0 ? string.Empty : $"[quote]\n{content}\n[/quote]";
    }

    private static string RenderCodeBlock(LeafBlock code)
    {
        return $"[code]\n{EscapeCode(code.Lines.ToString().TrimEnd())}\n[/code]";
    }

    private static string RenderTable(Table table)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[table]");

        foreach (var child in table)
        {
            if (child is not TableRow row)
                continue;

            builder.AppendLine("[tr]");
            foreach (var cellBlock in row)
            {
                var tag = row.IsHeader ? "th" : "td";
                var content = cellBlock is TableCell cell
                    ? RenderTableCell(cell).Trim()
                    : RenderBlock(cellBlock).Trim();
                builder.Append('[').Append(tag).Append(']');
                builder.Append(content);
                builder.Append("[/").Append(tag).AppendLine("]");
            }

            builder.AppendLine("[/tr]");
        }

        builder.Append("[/table]");
        return builder.ToString();
    }

    private static string RenderTableCell(TableCell cell)
    {
        List<string> parts = [];

        foreach (var block in cell)
        {
            var text = RenderBlock(block).Trim();
            if (text.Length > 0)
                parts.Add(text);
        }

        return string.Join("\n", parts);
    }

    private static string RenderInline(ContainerInline? container)
    {
        if (container == null)
            return string.Empty;

        var builder = new StringBuilder();
        var inline = container.FirstChild;

        while (inline != null)
        {
            builder.Append(RenderInline(inline));
            inline = inline.NextSibling;
        }

        return builder.ToString();
    }

    private static string RenderInline(Inline inline)
    {
        return inline switch
        {
            LiteralInline literal => EscapeText(literal.Content.ToString()),
            CodeInline code => $"[code]{EscapeCode(code.Content)}[/code]",
            LineBreakInline => "\n",
            HtmlInline html => EscapeText(html.Tag),
            LinkInline { IsImage: true } image => RenderImage(image),
            LinkInline link => RenderLink(link),
            EmphasisInline emphasis => RenderEmphasis(emphasis),
            ContainerInline nested => RenderInline(nested),
            _ => EscapeText(inline.ToString() ?? string.Empty)
        };
    }

    private static string RenderImage(LinkInline image)
    {
        if (string.IsNullOrWhiteSpace(image.Url))
            return RenderInline(image);

        return $"[img]{EscapeUrl(image.Url)}[/img]";
    }

    private static string RenderLink(LinkInline link)
    {
        var label = RenderInline(link).Trim();
        if (string.IsNullOrWhiteSpace(link.Url))
            return label;

        return $"[url={EscapeUrl(link.Url)}]{label}[/url]";
    }

    private static string RenderEmphasis(EmphasisInline emphasis)
    {
        var content = RenderInline(emphasis);
        return emphasis.DelimiterChar switch
        {
            '*' or '_' when emphasis.DelimiterCount >= 2 => $"[b]{content}[/b]",
            '*' or '_' => $"[i]{content}[/i]",
            '~' => $"[strike]{content}[/strike]",
            _ => content
        };
    }

    private static string EscapeText(string text)
    {
        return EscapeBrackets(text);
    }

    private static string EscapeCode(string text)
    {
        return EscapeBrackets(text);
    }

    private static string EscapeUrl(string url)
    {
        return url
            .Replace("[", "%5B", StringComparison.Ordinal)
            .Replace("]", "%5D", StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal);
    }

    private static string EscapeBrackets(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
            builder.Append(c switch
            {
                '[' => "[lb]",
                ']' => "[rb]",
                _ => c
            });

        return builder.ToString();
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static bool ContainsKnownBbCode(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            var open = text.IndexOf('[', index);
            if (open < 0)
                return false;

            var close = text.IndexOf(']', open + 1);
            if (close < 0)
                return false;

            var tag = ParseTag(text[(open + 1)..close]);
            if (tag is { IsListItem: true } || (tag is { Name: var name } &&
                                                !name.Equals("lb", StringComparison.OrdinalIgnoreCase) &&
                                                !name.Equals("rb", StringComparison.OrdinalIgnoreCase) &&
                                                KnownTags.Contains(name)))
                return true;

            index = close + 1;
        }

        return false;
    }

    private static TagToken? ParseTag(string rawTag)
    {
        var tag = rawTag.Trim();
        if (tag.Length == 0)
            return null;

        if (tag == "*")
            return TagToken.ListItem();

        var isClosing = tag[0] == '/';
        if (isClosing)
            tag = tag[1..].TrimStart();

        var equals = tag.IndexOf('=');
        var nameEnd = equals >= 0 ? equals : tag.IndexOfAny([' ', '\t']);
        if (nameEnd < 0)
            nameEnd = tag.Length;

        var name = tag[..nameEnd].Trim().ToLowerInvariant();
        if (name.Length == 0)
            return null;

        return new TagToken(name, isClosing, false);
    }

    private sealed record TagToken(string Name, bool IsClosing, bool IsListItem)
    {
        public static TagToken ListItem()
        {
            return new TagToken("*", false, true);
        }
    }
}