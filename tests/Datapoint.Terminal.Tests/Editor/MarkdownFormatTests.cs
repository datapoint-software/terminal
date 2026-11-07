using Datapoint.Terminal.Editor;

namespace Datapoint.Terminal.Tests.Editor;

public sealed class MarkdownFormatTests
{
    [Fact]
    public void Wraps_the_selection_and_unwraps_it_again()
    {
        var bold = MarkdownFormat.Wrap("Ask Rita first.", 4, 8, "**");
        Assert.Equal(new FormatEdit("Ask **Rita** first.", 6, 10), bold);

        Assert.Equal(new FormatEdit("Ask Rita first.", 4, 8), MarkdownFormat.Wrap(bold.Text, bold.SelectionStart, bold.SelectionEnd, "**"));
        Assert.Equal(new FormatEdit("Ask Rita first.", 4, 8), MarkdownFormat.Wrap(bold.Text, 4, 12, "**"));
    }

    [Fact]
    public void Puts_the_caret_between_the_markers_with_nothing_selected()
    {
        Assert.Equal(new FormatEdit("Run `` now", 5, 5), MarkdownFormat.Wrap("Run  now", 4, 4, "`"));
    }

    [Fact]
    public void Doesnt_take_italic_for_part_of_bold()
    {
        Assert.Equal(new FormatEdit("**R*it*a**", 4, 6), MarkdownFormat.Wrap("**Rita**", 3, 5, "*"));
        Assert.Equal(new FormatEdit("***Rita***", 3, 7), MarkdownFormat.Wrap("**Rita**", 2, 6, "*"));
    }

    [Fact]
    public void Makes_a_link_with_the_address_selected()
    {
        Assert.Equal(new FormatEdit("See [the docs](https://).", 15, 23), MarkdownFormat.Link("See the docs.", 4, 12));
        Assert.Equal(new FormatEdit("See [](https://)", 5, 5), MarkdownFormat.Link("See ", 4, 4));
    }

    [Theory]
    [InlineData("- item", "- ")]
    [InlineData("  * [x] done", "  * [x] ")]
    [InlineData("12) twelve", "12) ")]
    [InlineData("plain", null)]
    [InlineData("-not a list", null)]
    public void Finds_a_list_items_marker(string body, string? marker) => Assert.Equal(marker, ListMarker.Of(body));

    [Theory]
    [InlineData("- ", "- ")]
    [InlineData("- [x] ", "- [ ] ")]
    [InlineData("9. ", "10. ")]
    [InlineData("  3) [ ] ", "  4) [ ] ")]
    public void Starts_the_next_item_the_same_way(string marker, string next) => Assert.Equal(next, ListMarker.Next(marker));

    [Theory]
    [InlineData("Ask **Rita** first.", "Ask Ri", 8)]
    [InlineData("Ask **Rita** first.", "Ask Rita ", 13)]
    [InlineData("# Overview", "", 2)]
    [InlineData("# Overview", "Over", 6)]
    [InlineData("- [ ] Rotate the logs", "", 6)]
    [InlineData("See [the docs](https://example.com) now.", "See the docs", 13)]
    [InlineData("See [the docs](https://example.com) now.", "See the docs n", 37)]
    public void Finds_where_a_click_lands_in_the_markdown(string markdown, string rendered, int offset) =>
        Assert.Equal(offset, SourceOffset.Of(markdown, rendered));
}
