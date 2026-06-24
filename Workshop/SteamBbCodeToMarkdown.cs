using System.Text;

namespace STS2WorkshopUploader.Workshop;

internal static class SteamBbCodeToMarkdown
{
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

    public static string Convert(string bbcode)
    {
        if (string.IsNullOrEmpty(bbcode))
            return string.Empty;

        var root = Parse(bbcode.Replace("\r\n", "\n").Replace('\r', '\n'));
        return NormalizeLineEndings(RenderBlocks(root.Children, 0)).Trim();
    }

    private static BbNode Parse(string text)
    {
        var root = BbNode.Element("root");
        List<BbNode> stack = [root];
        var index = 0;

        while (index < text.Length)
        {
            var open = text.IndexOf('[', index);
            if (open < 0)
            {
                AppendText(Current(stack), text[index..]);
                break;
            }

            if (open > index)
                AppendText(Current(stack), text[index..open]);

            var close = text.IndexOf(']', open + 1);
            if (close < 0)
            {
                AppendText(Current(stack), text[open..]);
                break;
            }

            var rawTag = text[(open + 1)..close];
            var tag = ParseTag(rawTag);
            if (tag == null)
            {
                AppendText(Current(stack), text[open..(close + 1)]);
                index = close + 1;
                continue;
            }

            if (tag.Name.Equals("lb", StringComparison.OrdinalIgnoreCase))
            {
                AppendText(Current(stack), "[");
                index = close + 1;
                continue;
            }

            if (tag.Name.Equals("rb", StringComparison.OrdinalIgnoreCase))
            {
                AppendText(Current(stack), "]");
                index = close + 1;
                continue;
            }

            if (tag.IsListItem)
            {
                StartListItem(stack);
                index = close + 1;
                continue;
            }

            if (!KnownTags.Contains(tag.Name))
            {
                AppendText(Current(stack), text[open..(close + 1)]);
                index = close + 1;
                continue;
            }

            if (tag.IsClosing)
            {
                if (!CloseTag(stack, tag.Name))
                    AppendText(Current(stack), text[open..(close + 1)]);

                index = close + 1;
                continue;
            }

            if (tag.Name.Equals("code", StringComparison.OrdinalIgnoreCase))
            {
                var closingStart = IndexOfClosingTag(text, "code", close + 1);
                var contentEnd = closingStart < 0 ? text.Length : closingStart;
                Current(stack).Children.Add(BbNode.Element("code", text[(close + 1)..contentEnd]));
                index = closingStart < 0 ? text.Length : contentEnd + "[/code]".Length;
                continue;
            }

            var node = BbNode.Element(tag.Name, attribute: tag.Attribute);
            Current(stack).Children.Add(node);
            stack.Add(node);
            index = close + 1;
        }

        return root;
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

        var attribute = equals >= 0 ? tag[(equals + 1)..].Trim() : null;
        return new TagToken(name, attribute, isClosing, false);
    }

    private static void StartListItem(List<BbNode> stack)
    {
        var listIndex = -1;
        for (var i = stack.Count - 1; i >= 0; i--)
            if (IsListTag(stack[i].Tag))
            {
                listIndex = i;
                break;
            }

        if (listIndex < 0)
        {
            AppendText(Current(stack), "[*]");
            return;
        }

        while (stack.Count > listIndex + 1)
            stack.RemoveAt(stack.Count - 1);

        var item = BbNode.Element("*");
        Current(stack).Children.Add(item);
        stack.Add(item);
    }

    private static bool CloseTag(List<BbNode> stack, string tag)
    {
        for (var i = stack.Count - 1; i > 0; i--)
        {
            if (!stack[i].Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
                continue;

            while (stack.Count > i)
                stack.RemoveAt(stack.Count - 1);
            return true;
        }

        return false;
    }

    private static int IndexOfClosingTag(string text, string tag, int start)
    {
        return text.IndexOf($"[/{tag}]", start, StringComparison.OrdinalIgnoreCase);
    }

    private static BbNode Current(List<BbNode> stack)
    {
        return stack[^1];
    }

    private static void AppendText(BbNode node, string text)
    {
        if (text.Length == 0)
            return;

        if (node.Children.LastOrDefault() is { Tag: "text", Text: not null } last)
            last.Text += text;
        else
            node.Children.Add(BbNode.TextNode(text));
    }

    private static string RenderBlocks(IReadOnlyList<BbNode> nodes, int indent)
    {
        List<string> blocks = [];
        var inline = new StringBuilder();

        foreach (var node in nodes)
        {
            if (!IsBlockNode(node))
            {
                inline.Append(RenderInline(node));
                continue;
            }

            FlushInline();
            var block = RenderBlock(node, indent).Trim();
            if (block.Length > 0)
                blocks.Add(block);
        }

        FlushInline();
        return string.Join("\n\n", blocks);

        void FlushInline()
        {
            var text = NormalizeInlineWhitespace(inline.ToString()).Trim();
            if (text.Length > 0)
                blocks.Add(text);
            inline.Clear();
        }
    }

    private static bool IsBlockNode(BbNode node)
    {
        return CodeLooksBlock(node) || node.Tag is "quote" or "list" or "olist" or "table" ||
               IsHeadingTag(node.Tag);
    }

    private static string RenderBlock(BbNode node, int indent)
    {
        return node.Tag switch
        {
            "code" => CodeLooksBlock(node)
                ? RenderCodeBlock(node.Text ?? RenderInlineChildren(node))
                : RenderInline(node),
            "quote" => RenderQuote(node, indent),
            "list" => RenderList(node, indent, false),
            "olist" => RenderList(node, indent, true),
            "table" => RenderTable(node),
            _ when IsHeadingTag(node.Tag) => RenderHeading(node),
            _ => RenderInline(node)
        };
    }

    private static string RenderHeading(BbNode node)
    {
        var level = int.TryParse(node.Tag.AsSpan(1), out var value) ? Math.Clamp(value, 1, 6) : 1;
        var content = RenderInlineChildren(node).Trim();
        if (content.Length == 0)
            return string.Empty;

        if (!content.Contains('\n'))
            return $"{new string('#', level)} {content}";

        if (level <= 2)
        {
            var underline = level == 1 ? "=" : "-";
            return $"{content}\n{new string(underline[0], Math.Max(3, LongestLineLength(content)))}";
        }

        return $"{new string('#', level)} {content.Replace("\n", " ", StringComparison.Ordinal)}";
    }

    private static string RenderQuote(BbNode node, int indent)
    {
        var content = RenderBlocks(node.Children, indent).Trim();
        if (content.Length == 0)
            return string.Empty;

        return PrefixLines(content, "> ");
    }

    private static string RenderList(BbNode node, int indent, bool ordered)
    {
        var builder = new StringBuilder();
        var index = 1;

        foreach (var item in node.Children.Where(static child => child.Tag == "*"))
        {
            var marker = ordered ? $"{index}. " : "- ";
            var itemText = RenderListItem(item, indent + marker.Length);
            if (itemText.Length == 0)
                continue;

            var lines = itemText.Split('\n');
            builder.Append(new string(' ', indent)).Append(marker).AppendLine(lines[0]);
            for (var i = 1; i < lines.Length; i++)
                builder.Append(new string(' ', indent + marker.Length)).AppendLine(lines[i]);

            index++;
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderListItem(BbNode item, int indent)
    {
        List<string> parts = [];
        var inline = new StringBuilder();

        foreach (var child in item.Children)
        {
            if (!IsBlockNode(child))
            {
                inline.Append(RenderInline(child));
                continue;
            }

            FlushInline();
            var block = RenderBlock(child, indent).Trim();
            if (block.Length > 0)
                parts.Add(block);
        }

        FlushInline();
        return string.Join("\n", parts);

        void FlushInline()
        {
            var text = NormalizeInlineWhitespace(inline.ToString()).Trim();
            if (text.Length > 0)
                parts.Add(text);
            inline.Clear();
        }
    }

    private static string RenderTable(BbNode table)
    {
        var rows = table.Children.Where(static child => child.Tag == "tr").ToList();
        if (rows.Count == 0)
            return RenderInlineChildren(table).Trim();

        var cells = rows.Select(row => row.Children
                .Where(static child => child.Tag is "th" or "td")
                .Select(cell => RenderInlineChildren(cell).Replace("\n", "<br>", StringComparison.Ordinal).Trim())
                .ToList())
            .ToList();

        var columnCount = cells.Count == 0 ? 0 : cells.Max(static row => row.Count);
        if (columnCount == 0)
            return string.Empty;

        var builder = new StringBuilder();
        AppendTableRow(builder, cells[0], columnCount);
        builder.Append('|');
        for (var i = 0; i < columnCount; i++)
            builder.Append(" --- |");
        builder.AppendLine();

        foreach (var row in cells.Skip(1))
            AppendTableRow(builder, row, columnCount);

        return builder.ToString().TrimEnd();
    }

    private static void AppendTableRow(StringBuilder builder, IReadOnlyList<string> row, int columnCount)
    {
        builder.Append('|');
        for (var i = 0; i < columnCount; i++)
            builder.Append(' ').Append(EscapeTableCell(i < row.Count ? row[i] : string.Empty)).Append(" |");
        builder.AppendLine();
    }

    private static string RenderCodeBlock(string content)
    {
        var normalized = DecodeEscapes(content).Replace("\r\n", "\n").Replace('\r', '\n').Trim('\n');
        var fence = "```";
        while (normalized.Contains(fence, StringComparison.Ordinal))
            fence += "`";

        return $"{fence}\n{normalized}\n{fence}";
    }

    private static string RenderInline(BbNode node)
    {
        if (node is { Tag: "text", Text: not null } textNode)
            return DecodeEscapes(textNode.Text);

        return node.Tag switch
        {
            "b" => WrapInline("**", RenderInlineChildren(node)),
            "i" => WrapInline("*", RenderInlineChildren(node)),
            "u" => $"<u>{RenderInlineChildren(node)}</u>",
            "strike" => WrapInline("~~", RenderInlineChildren(node)),
            "code" => RenderInlineCode(node.Text ?? RenderInlineChildren(node)),
            "url" => RenderUrl(node),
            "img" => RenderImage(node),
            "tr" or "th" or "td" => RenderInlineChildren(node),
            _ when IsBlockNode(node) => RenderBlock(node, 0),
            _ => RenderInlineChildren(node)
        };
    }

    private static string RenderInlineChildren(BbNode node)
    {
        var builder = new StringBuilder();
        foreach (var child in node.Children)
            builder.Append(RenderInline(child));
        return builder.ToString();
    }

    private static string RenderUrl(BbNode node)
    {
        var label = RenderInlineChildren(node).Trim();
        var url = DecodeEscapes(node.Attribute ?? label).Trim();
        if (url.Length == 0)
            return label;

        return label.Length == 0 || string.Equals(label, url, StringComparison.Ordinal)
            ? $"<{url}>"
            : $"[{EscapeLinkLabel(label)}]({url})";
    }

    private static string RenderImage(BbNode node)
    {
        var url = RenderInlineChildren(node).Trim();
        return url.Length == 0 ? string.Empty : $"![]({url})";
    }

    private static string WrapInline(string wrapper, string content)
    {
        return content.Length == 0 ? string.Empty : $"{wrapper}{content}{wrapper}";
    }

    private static string RenderInlineCode(string content)
    {
        var decoded = DecodeEscapes(content).Replace("\n", " ", StringComparison.Ordinal);
        var marker = "`";
        while (decoded.Contains(marker, StringComparison.Ordinal))
            marker += "`";

        return $"{marker}{decoded}{marker}";
    }

    private static string DecodeEscapes(string text)
    {
        return text
            .Replace("[lb]", "[", StringComparison.OrdinalIgnoreCase)
            .Replace("[rb]", "]", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeInlineWhitespace(string text)
    {
        return text.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static string PrefixLines(string text, string prefix)
    {
        return string.Join("\n", text.Split('\n').Select(line => prefix + line));
    }

    private static string EscapeLinkLabel(string label)
    {
        return label.Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal);
    }

    private static string EscapeTableCell(string text)
    {
        return text.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static int LongestLineLength(string text)
    {
        return text.Split('\n').Max(static line => line.Length);
    }

    private static bool IsHeadingTag(string tag)
    {
        return tag is "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    }

    private static bool CodeLooksBlock(BbNode node)
    {
        if (node.Tag != "code")
            return false;

        var content = node.Text ?? RenderInlineChildren(node);
        return content.Contains('\n') || content.Length > 80;
    }

    private static bool IsListTag(string tag)
    {
        return tag is "list" or "olist";
    }

    private sealed class BbNode
    {
        private BbNode(string tag, string? attribute = null, string? text = null)
        {
            Tag = tag;
            Attribute = attribute;
            Text = text;
        }

        public string Tag { get; }
        public string? Attribute { get; }
        public string? Text { get; set; }
        public List<BbNode> Children { get; } = [];

        public static BbNode Element(string tag, string? text = null, string? attribute = null)
        {
            return new BbNode(tag, attribute, text);
        }

        public static BbNode TextNode(string text)
        {
            return new BbNode("text", text: text);
        }
    }

    private sealed record TagToken(string Name, string? Attribute, bool IsClosing, bool IsListItem)
    {
        public static TagToken ListItem()
        {
            return new TagToken("*", null, false, true);
        }
    }
}