using Datapoint.Terminal.Agent.Web;

namespace Datapoint.Terminal.Agent.Tests.Web;

public sealed class HtmlTextTests
{
    private static readonly Uri s_page = new("https://docs.example.org/guide/start.html");

    [Fact]
    public void Keeps_headings_paragraphs_and_lists()
    {
        var text = Convert("<h2>Steps</h2><p>First   read\n this.</p><ol><li>Install</li><li>Run <code>tool --help</code></li></ol><ul><li>One<ul><li>Inner</li></ul></li></ul>");

        Assert.Equal("## Steps\n\nFirst read this.\n\n1. Install\n2. Run `tool --help`\n\n- One\n  - Inner", text);
    }

    [Fact]
    public void Keeps_code_blocks_as_written()
    {
        var text = Convert("<p>Example:</p><pre><code>if (a &lt; b)\n    return;</code></pre>");

        Assert.Equal("Example:\n\n```\nif (a < b)\n    return;\n```", text);
    }

    [Fact]
    public void Makes_links_absolute_and_drops_ones_the_agent_cannot_follow()
    {
        var text = Convert("<p>See <a href=\"../api/\">the API</a>, <a href='#top'>top</a> and <a href=\"javascript:void(0)\">this</a>.</p>");

        Assert.Equal("See [the API](https://docs.example.org/api/), top and this.", text);
    }

    [Fact]
    public void Drops_scripts_styles_navigation_and_forms()
    {
        var text = Convert("<nav><ul><li>Home</li></ul><nav>inner</nav>still nav</nav><script>var a = '<p>';</script><style>p{}</style><form><input name=q></form><!-- hidden --><p>Body</p><footer>Footer</footer>");

        Assert.Equal("Body", text);
    }

    [Fact]
    public void Reads_only_the_main_content_when_the_page_marks_it()
    {
        var (title, text, _) = HtmlText.ToMarkdown("<html><head><title> A &amp; B </title></head><body><div>Banner</div><main><p>Content</p></main><div>Sidebar</div></body></html>", s_page);

        Assert.Equal("A & B", title);
        Assert.Equal("Content", text);
    }

    [Fact]
    public void Keeps_table_cells_apart()
    {
        var text = Convert("<table><tr><th>Name</th><th>Value</th></tr><tr><td>a</td><td>1</td></tr></table>");

        Assert.Equal("Name | Value\na | 1", text);
    }

    [Fact]
    public void Leaves_out_what_the_page_hides_from_readers()
    {
        var text = Convert("<div data-x hidden><p>Sign in</p></div><p>Shown<span aria-hidden=\"true\">★</span><img hidden src=a.png> here</p><details hidden=\"until-found\">Collapsed</details><p data-hidden-note=\"x\">Kept</p>");

        Assert.Equal("Shown here\n\nCollapsed\n\nKept", text);
    }

    [Fact]
    public void Leaves_out_list_items_with_nothing_left_in_them()
    {
        Assert.Equal("Intro\n\n- Real", Convert("<p>Intro</p><ul><li><button>Menu</button></li><li>Real</li><li><svg></svg></li></ul>"));
    }

    [Fact]
    public void Treats_a_stray_less_than_sign_as_text()
    {
        Assert.Equal("a < b and c", Convert("<p>a < b and c</p>"));
    }

    [Fact]
    public void Survives_unclosed_tags_and_comments()
    {
        Assert.Equal("Text", Convert("<p>Text<!-- never closed"));
        Assert.Equal("Text", Convert("<p>Text<div class=\"x"));
        Assert.Equal(string.Empty, Convert("<nav><p>never closed"));
    }

    private static string Convert(string html) => HtmlText.ToMarkdown(html, s_page).Text;
}
