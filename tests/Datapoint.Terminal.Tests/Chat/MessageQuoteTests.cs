using Datapoint.Terminal.Chat;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class MessageQuoteTests
{
    [Fact]
    public void Quotes_every_line_into_an_empty_message()
    {
        var (draft, caret) = MessageQuote.Insert(string.Empty, 0, "Disk use on / is 30%.\n\nThe journal is 198.1M.\n");

        Assert.Equal("> Disk use on / is 30%.\n>\n> The journal is 198.1M.\n\n", draft);
        Assert.Equal(draft.Length, caret);
    }

    [Fact]
    public void Puts_the_quote_on_lines_of_its_own_where_the_user_was_writing()
    {
        const string Written = "Why is this?\nThanks";

        var (draft, caret) = MessageQuote.Insert(Written, "Why is this?".Length, "2.96 GB reclaimable");

        Assert.Equal("Why is this?\n\n> 2.96 GB reclaimable\n\nThanks", draft);
        Assert.Equal("Why is this?\n\n> 2.96 GB reclaimable\n\n".Length, caret);
    }

    [Fact]
    public void Adds_a_second_quote_after_the_first()
    {
        var (first, caret) = MessageQuote.Insert(null, 0, "One");

        var (draft, _) = MessageQuote.Insert(first, caret, "Two\r\nThree");

        Assert.Equal("> One\n\n> Two\n> Three\n\n", draft);
    }
}
