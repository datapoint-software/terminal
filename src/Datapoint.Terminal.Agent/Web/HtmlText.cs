using System.Globalization;
using System.Net;
using System.Text;

namespace Datapoint.Terminal.Agent.Web;

// Turns a web page into Markdown the model reads well: headings, paragraphs, lists, links, code and tables, without
// scripts, styles, navigation or forms. A hand-written scanner rather than a parser library: reading only needs it to be
// good enough, it stays linear on hostile pages, and it adds no dependency to review.
internal static class HtmlText
{
    // Dropped with everything inside them: not part of what the page says, or (scripts, styles) not text at all.
    private static readonly HashSet<string> s_dropped = new(StringComparer.Ordinal)
    {
        "script", "style", "noscript", "template", "svg", "math", "iframe", "object", "canvas", "head",
        "nav", "footer", "aside", "form", "button", "select", "textarea", "dialog",
    };

    // Their content is raw text, so a "<" inside doesn't open a tag and only their own end tag closes them.
    private static readonly HashSet<string> s_rawText = new(StringComparer.Ordinal) { "script", "style", "textarea", "noscript" };

    // Elements without an end tag, which can't be skipped to their end.
    private static readonly HashSet<string> s_void = new(StringComparer.Ordinal)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr",
    };

    private static readonly HashSet<string> s_blocks = new(StringComparer.Ordinal)
    {
        "p", "div", "section", "article", "main", "header", "blockquote", "figure", "figcaption", "details", "summary",
        "dl", "dt", "dd", "address", "table", "center",
    };

    public static HtmlPage ToMarkdown(string html, Uri address)
    {
        ArgumentNullException.ThrowIfNull(html);
        var writer = new Writer(address);
        writer.Write(Focus(html));
        return new HtmlPage(FindTitle(html), writer.ToString(), writer.Links);
    }

    // Most pages wrap what they say in main or article; reading only that leaves out menus and banners the drop list misses.
    private static string Focus(string html) => Inner(html, "main") ?? Inner(html, "article") ?? Inner(html, "body") ?? html;

    private static string? Inner(string html, string name)
    {
        var open = FindStartTag(html, name, 0);
        var start = open < 0 ? -1 : html.IndexOf('>', open);
        var end = html.LastIndexOf("</" + name, StringComparison.OrdinalIgnoreCase);
        return start < 0 || end <= start ? null : html[(start + 1)..end];
    }

    private static string? FindTitle(string html)
    {
        var open = FindStartTag(html, "title", 0);
        var start = open < 0 ? -1 : html.IndexOf('>', open);
        var end = start < 0 ? -1 : html.IndexOf("</title", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
        {
            return null;
        }

        var title = CollapseSpaces(WebUtility.HtmlDecode(html[(start + 1)..end])).Trim();
        return title.Length == 0 ? null : title;
    }

    private static int FindStartTag(string html, string name, int from)
    {
        var at = from;
        while ((at = html.IndexOf("<" + name, at, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var next = at + name.Length + 1;
            if (next >= html.Length || html[next] is '>' or '/' || char.IsWhiteSpace(html[next]))
            {
                return at;
            }

            at = next;
        }

        return -1;
    }

    private static string CollapseSpaces(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
            else if (builder.Length == 0 || builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString();
    }

    private sealed class Writer(Uri address)
    {
        private readonly StringBuilder _text = new();
        private readonly Stack<(string? Href, int Start)> _links = new();
        private readonly Stack<(bool Ordered, int Count)> _lists = new();
        private readonly List<Uri> _targets = [];
        private int _pre;
        private int _cell;

        // The pages the text links to, which the agent may follow: the page wrote them, so they carry nothing from the terminals.
        public IReadOnlyList<Uri> Links => _targets;

        public void Write(string html)
        {
            var i = 0;
            while (i < html.Length)
            {
                var lt = html.IndexOf('<', i);
                if (lt < 0)
                {
                    Text(html[i..]);
                    return;
                }

                Text(html[i..lt]);
                i = ReadTag(html, lt);
            }
        }

        // Handles the tag at lt and returns where reading goes on.
        private int ReadTag(string html, int lt)
        {
            var next = lt + 1 < html.Length ? html[lt + 1] : ' ';
            if (!char.IsAsciiLetter(next) && next is not ('/' or '!' or '?'))
            {
                Text("<");
                return lt + 1;
            }

            var gt = next == '!' && string.CompareOrdinal(html, lt, "<!--", 0, 4) == 0
                ? CommentEnd(html, lt)
                : html.IndexOf('>', lt);
            if (gt < 0)
            {
                return html.Length;
            }

            return next is '!' or '?' ? gt + 1 : ReadElementTag(html, lt, gt);
        }

        private static int CommentEnd(string html, int lt)
        {
            var end = html.IndexOf("-->", lt + 4, StringComparison.Ordinal);
            return end < 0 ? -1 : end + 2;
        }

        private int ReadElementTag(string html, int lt, int gt)
        {
            var tag = html.AsSpan(lt + 1, gt - lt - 1);
            if (tag[0] == '/')
            {
                End(TagName(tag[1..]));
                return gt + 1;
            }

            var name = TagName(tag);
            if ((s_dropped.Contains(name) || IsHidden(tag)) && !tag.EndsWith("/") && !s_void.Contains(name))
            {
                return SkipElement(html, gt + 1, name);
            }

            Start(name, tag);
            return gt + 1;
        }

        private static string TagName(ReadOnlySpan<char> tag)
        {
            var end = 0;
            while (end < tag.Length && (char.IsAsciiLetterOrDigit(tag[end]) || tag[end] == '-'))
            {
                end++;
            }

            return tag[..end].ToString().ToLowerInvariant();
        }

        // Past the element's end tag, counting elements of the same name nested inside it.
        private static int SkipElement(string html, int from, string name)
        {
            if (s_rawText.Contains(name))
            {
                var close = html.IndexOf("</" + name, from, StringComparison.OrdinalIgnoreCase);
                var closeEnd = close < 0 ? -1 : html.IndexOf('>', close);
                return closeEnd < 0 ? html.Length : closeEnd + 1;
            }

            var depth = 1;
            var at = from;
            while (depth > 0 && (at = html.IndexOf('<', at)) >= 0)
            {
                var gt = html.IndexOf('>', at);
                if (gt < 0)
                {
                    return html.Length;
                }

                depth += Nesting(html.AsSpan(at + 1, gt - at - 1), name);
                at = gt + 1;
            }

            return at < 0 ? html.Length : at;
        }

        // +1 for a start tag of the element, -1 for its end tag, 0 for anything else.
        private static int Nesting(ReadOnlySpan<char> tag, string name)
        {
            var isEnd = tag.StartsWith("/");
            if (!string.Equals(TagName(isEnd ? tag[1..] : tag), name, StringComparison.Ordinal))
            {
                return 0;
            }

            if (isEnd)
            {
                return -1;
            }

            return tag.EndsWith("/") ? 0 : 1;
        }

        private void Start(string name, ReadOnlySpan<char> tag)
        {
            if (name is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
            {
                Block();
                _text.Append('#', name[1] - '0').Append(' ');
                return;
            }

            switch (name)
            {
                case "br":
                    Line();
                    break;
                case "hr":
                    Block();
                    _text.Append("---");
                    Block();
                    break;
                case "ul" or "ol":
                    StartList(isOrdered: string.Equals(name, "ol", StringComparison.Ordinal));
                    break;
                case "li":
                    ListItem();
                    break;
                default:
                    StartInline(name, tag);
                    break;
            }
        }

        private void StartInline(string name, ReadOnlySpan<char> tag)
        {
            switch (name)
            {
                case "pre":
                    Block();
                    _text.Append("```\n");
                    _pre++;
                    break;
                case "code" when _pre == 0:
                    _text.Append('`');
                    break;
                case "a":
                    _links.Push((Link(Attribute(tag, "href")), _text.Length));
                    break;
                case "tr":
                    Line();
                    _cell = 0;
                    break;
                case "td" or "th":
                    _text.Append(_cell++ > 0 ? " | " : string.Empty);
                    break;
                default:
                    BlockIf(name);
                    break;
            }
        }

        private void End(string name)
        {
            switch (name)
            {
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "table":
                    Block();
                    break;
                case "ul" or "ol":
                    EndList();
                    break;
                case "pre" when _pre > 0:
                    _pre--;
                    Line();
                    _text.Append("```");
                    Block();
                    break;
                case "code" when _pre == 0:
                    _text.Append('`');
                    break;
                case "a" when _links.Count > 0:
                    EndLink(_links.Pop());
                    break;
                default:
                    BlockIf(name);
                    break;
            }
        }

        private void BlockIf(string name)
        {
            if (!s_blocks.Contains(name))
            {
                return;
            }

            Block();
        }

        private void StartList(bool isOrdered)
        {
            if (_lists.Count == 0)
            {
                Block();
            }

            _lists.Push((isOrdered, 0));
        }

        private void EndList()
        {
            if (_lists.Count > 0)
            {
                _lists.Pop();
            }

            if (_lists.Count > 0)
            {
                return;
            }

            Block();
        }

        private void ListItem()
        {
            Line();
            if (_lists.Count == 0)
            {
                _text.Append("- ");
                return;
            }

            var (ordered, count) = _lists.Pop();
            _lists.Push((ordered, count + 1));
            _text.Append(' ', (_lists.Count - 1) * 2);
            _text.Append(ordered ? (count + 1).ToString(CultureInfo.InvariantCulture) + ". " : "- ");
        }

        // A link becomes [text](address) when it goes somewhere the agent could read; otherwise only its text stays.
        private void EndLink((string? Href, int Start) link)
        {
            if (link.Href is null || link.Start > _text.Length)
            {
                return;
            }

            var text = _text.ToString(link.Start, _text.Length - link.Start).Trim();
            if (text.Length == 0 || text.Contains('\n', StringComparison.Ordinal))
            {
                return;
            }

            _text.Length = link.Start;
            _text.Append('[').Append(text.Replace("]", "\\]", StringComparison.Ordinal)).Append("](").Append(link.Href).Append(')');
        }

        private string? Link(string? href)
        {
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#'))
            {
                return null;
            }

            if (!Uri.TryCreate(address, WebUtility.HtmlDecode(href.Trim()), out var target) || !WebAddressGuard.IsWebScheme(target.Scheme))
            {
                return null;
            }

            if (_targets.Count < 2_000)
            {
                _targets.Add(target);
            }

            return target.AbsoluteUri.Replace(" ", "%20", StringComparison.Ordinal).Replace(")", "%29", StringComparison.Ordinal);
        }

        // Hidden from readers on the page: the hidden attribute (but not "until-found", which is only collapsed) or aria-hidden.
        private static bool IsHidden(ReadOnlySpan<char> tag)
        {
            if (string.Equals(Attribute(tag, "aria-hidden"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var text = tag.ToString();
            var at = 0;
            while ((at = text.IndexOf("hidden", at, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var end = at + "hidden".Length;
                var isWord = at > 0 && char.IsWhiteSpace(text[at - 1]) && (end == text.Length || char.IsWhiteSpace(text[end]) || text[end] is '=' or '/');
                if (isWord)
                {
                    return !string.Equals(Attribute(tag, "hidden"), "until-found", StringComparison.OrdinalIgnoreCase);
                }

                at = end;
            }

            return false;
        }

        // The value of the named attribute in a start tag, quoted or not.
        private static string? Attribute(ReadOnlySpan<char> tag, string name)
        {
            var text = tag.ToString();
            var at = 0;
            while ((at = text.IndexOf(name, at, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var isWord = at > 0 && char.IsWhiteSpace(text[at - 1]);
                at += name.Length;
                var equals = SkipSpaces(text, at);
                if (isWord && equals < text.Length && text[equals] == '=')
                {
                    return AttributeValue(text, SkipSpaces(text, equals + 1));
                }
            }

            return null;
        }

        private static string AttributeValue(string text, int start)
        {
            if (start < text.Length && text[start] is '"' or '\'')
            {
                var end = text.IndexOf(text[start], start + 1);
                return end < 0 ? text[(start + 1)..] : text[(start + 1)..end];
            }

            var stop = start;
            while (stop < text.Length && !char.IsWhiteSpace(text[stop]) && text[stop] != '/')
            {
                stop++;
            }

            return text[start..stop];
        }

        private static int SkipSpaces(string text, int at)
        {
            while (at < text.Length && char.IsWhiteSpace(text[at]))
            {
                at++;
            }

            return at;
        }

        private void Text(string html)
        {
            if (html.Length == 0)
            {
                return;
            }

            var text = WebUtility.HtmlDecode(html);
            if (_pre > 0)
            {
                _text.Append(text);
                return;
            }

            text = CollapseSpaces(text);
            if (text.StartsWith(' ') && (_text.Length == 0 || char.IsWhiteSpace(_text[^1])))
            {
                text = text[1..];
            }

            _text.Append(text);
        }

        private void Line()
        {
            TrimSpaces();
            if (_text.Length == 0 || _text[^1] == '\n')
            {
                return;
            }

            _text.Append('\n');
        }

        private void Block()
        {
            Line();
            if (_text.Length == 0 || (_text.Length >= 2 && _text[^2] == '\n'))
            {
                return;
            }

            _text.Append('\n');
        }

        private void TrimSpaces()
        {
            while (_text.Length > 0 && _text[^1] == ' ')
            {
                _text.Length--;
            }
        }

        // List items whose content was dropped (menus, icons) would leave a bare marker.
        public override string ToString()
        {
            var lines = _text.ToString().Split('\n').Where(line => !IsBareMarker(line.Trim()));
            var text = string.Join('\n', lines);
            while (text.Contains("\n\n\n", StringComparison.Ordinal))
            {
                text = text.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);
            }

            return text.Trim();
        }

        private static bool IsBareMarker(string line) =>
            line is "-" || (line.Length > 1 && line[^1] == '.' && line[..^1].All(char.IsAsciiDigit));
    }
}
