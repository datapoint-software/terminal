using Datapoint.Terminal.Editor;

namespace Datapoint.Terminal.Tests.Editor;

public sealed class MarkdownBlocksTests
{
    private const string HostDocument = """


        # web1

        <!-- datapoint:agent:start overview -->
        ## Overview

        Public web server for the shop:
        nginx in front of three app containers.
        <!-- datapoint:agent:end overview -->

        ## My notes

        - Maintenance window is Sundays.
        - Ask **Rita** first.
          - nested detail
        - [ ] Rotate the logs
        - [x] Renew the certificate

        1. Drain
        2. Update

        > A quote
        > over two lines

        ```bash
        df -h

        du -sh /var/log
        ```

        | Item | Value |
        | --- | --- |
        | OS | Debian 12 |

        ---
        Last line without a newline
        """;

    public static TheoryData<string> Samples => new()
    {
        HostDocument,
        HostDocument.Replace("\n", "\r\n", StringComparison.Ordinal),
        string.Empty,
        "\n\n\n",
        "just text",
        "# Title\n",
        "- a\n- b\n\n\n- c\n   \n",
        "Para one\nstill one\n\nPara two  \n",
        "<!-- only a comment -->\n",
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Joins_back_to_exactly_the_original(string text)
    {
        var blocks = MarkdownBlocks.Split(text);

        Assert.Equal(text, string.Concat(blocks.Select(block => block.Body + block.Trailing)));
    }

    [Fact]
    public void Splits_lists_into_items_and_hides_comments_and_leading_blank_lines()
    {
        var blocks = MarkdownBlocks.Split(HostDocument);

        Assert.Equal(
            [
                BlockKind.Gap, BlockKind.Heading1, BlockKind.Hidden, BlockKind.Heading2, BlockKind.Paragraph, BlockKind.Hidden,
                BlockKind.Heading2, BlockKind.Bullet, BlockKind.Bullet, BlockKind.Task, BlockKind.Task, BlockKind.Numbered, BlockKind.Numbered,
                BlockKind.Quote, BlockKind.Code, BlockKind.Table, BlockKind.Other, BlockKind.Paragraph,
            ],
            blocks.Select(block => block.Kind));
        Assert.Equal("- Ask **Rita** first.\n  - nested detail", blocks[8].Body);
        Assert.Equal("Para two  ", MarkdownBlocks.Split("Para one\n\nPara two  \n")[1].Body);
        Assert.Equal("\n\n", blocks[1].Trailing);
        Assert.Equal("\n", blocks[7].Trailing);
    }

    [Theory]
    [InlineData("", BlockKind.Gap)]
    [InlineData("## Notes", BlockKind.Heading2)]
    [InlineData("- [ ] Do it", BlockKind.Task)]
    [InlineData("3. Third", BlockKind.Numbered)]
    [InlineData("Plain words", BlockKind.Paragraph)]
    public void Tells_what_edited_text_has_become(string body, BlockKind expected) => Assert.Equal(expected, MarkdownBlocks.KindOf(body));
}
