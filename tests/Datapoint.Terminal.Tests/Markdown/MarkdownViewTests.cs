using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Datapoint.Terminal.Markdown;

namespace Datapoint.Terminal.Tests.Markdown;

public sealed class MarkdownViewTests
{
    private const string Reply = """
        ## Disk usage

        This PC's hostname is **MacBook-Pro.local**, and the *home* folder uses `42G`.

        1. Run `df -h`
        2. Check ~~the logs~~ the cache
           - Nested
             - Deeper

        - [x] Done
        - [ ] To do
        - Two, with [the docs](https://example.com/docs) and [a script](javascript:alert(1))

        > Quoted output

        ```bash
        # Count files
        for f in "$HOME"/*; do echo $f; done
        ```

        | Folder | Size |
        | :--- | ---: |
        | ~/Library | 30G |

        Press <kbd>Ctrl</kbd>+<b>C</b>, not <script>alert(1)</script>. Energy is $E = mc^2$.[^1]

        $$\frac{a}{b} \le \sqrt{x}$$

        ![A chart](https://example.com/chart.png)

        [^1]: Einstein, 1905.
        """;

    private static (Window Window, MarkdownView View) Show(string markdown)
    {
        var view = new MarkdownView { Text = markdown };
        var window = new Window { Width = 380, Height = 1000, Content = new ScrollViewer { Content = view, Padding = new Thickness(16, 8) } };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static List<SelectableTextBlock> Blocks(Control view) => [.. view.GetVisualDescendants().OfType<SelectableTextBlock>()];

    private static List<string> Texts(Control view) =>
        [.. view.GetVisualDescendants().OfType<TextBlock>().Select(text => text is SelectableTextBlock selectable ? InlineText.Of(selectable) : text.Text ?? string.Empty)];

    private static IEnumerable<(Run Run, LinkSpan? Link)> Runs(Control view) =>
        Blocks(view).Where(block => block.Inlines is not null).SelectMany(block => InlineText.Runs(block.Inlines!));

    [AvaloniaFact]
    public void Renders_a_reply_without_its_markup()
    {
        var (window, view) = Show(Reply);
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-markdown.png", new PngBitmapEncoderOptions());

        var texts = Texts(view);
        Assert.Contains("Disk usage", texts, StringComparer.Ordinal);
        Assert.Contains("This PC's hostname is MacBook-Pro.local, and the home folder uses 42G.", texts, StringComparer.Ordinal);
        Assert.Contains("for f in \"$HOME\"/*; do echo $f; done", texts.Single(text => text.StartsWith("# Count", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.DoesNotContain(texts, text => text.Contains("**", StringComparison.Ordinal) || text.Contains("```", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Gives_each_link_a_button_the_keyboard_can_reach()
    {
        var (_, view) = Show("See [the docs](https://example.com/docs), <https://a.example/> and [twice](https://example.com/docs).");

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();

        Assert.Equal(["the docs", "https://a.example/", "twice"], buttons.Select(button => button.Content as string), StringComparer.Ordinal);
        Assert.All(buttons, button => Assert.True(button.Focusable && button.IsTabStop));
        Assert.Equal("Link: the docs, to https://example.com/docs", Avalonia.Automation.AutomationProperties.GetName(buttons[0]));
    }

    [AvaloniaFact]
    public void Adds_no_link_buttons_to_a_reply_without_links()
    {
        var (_, view) = Show("Just text, and a [relative](docs/x.md) link that can't be opened.");

        Assert.Empty(view.GetVisualDescendants().OfType<Button>());
    }

    [AvaloniaFact]
    public void Marks_nested_lists_and_tasks_differently()
    {
        var (_, view) = Show(Reply);

        var markers = view.GetVisualDescendants().OfType<TextBlock>().Where(text => text is not SelectableTextBlock).Select(text => text.Text).ToList();

        Assert.Contains("1.", markers, StringComparer.Ordinal);
        Assert.Contains("◦", markers, StringComparer.Ordinal);
        Assert.Contains("▪", markers, StringComparer.Ordinal);
        Assert.Equal([true, false], view.GetVisualDescendants().OfType<CheckBox>().Select(box => box.IsChecked));
        Assert.Contains("Done", Texts(view), StringComparer.Ordinal);
    }

    [AvaloniaFact]
    public void Aligns_table_columns_as_written()
    {
        var (_, view) = Show(Reply);

        var size = Blocks(view).Single(block => string.Equals(InlineText.Of(block), "30G", StringComparison.Ordinal));

        Assert.Equal(TextAlignment.Right, size.TextAlignment);
    }

    [AvaloniaTheory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Colours_code_and_offers_to_copy_it(string newline)
    {
        var (_, view) = Show(Reply.ReplaceLineEndings(newline));

        var code = Blocks(view).Single(block => InlineText.Of(block).StartsWith("# Count", StringComparison.Ordinal));
        var coloured = InlineText.Runs(code.Inlines!).Select(item => item.Run).Where(run => run.IsSet(TextElement.ForegroundProperty)).Select(run => run.Text).ToList();

        Assert.Contains("# Count files", coloured, StringComparer.Ordinal);
        Assert.Contains("for", coloured, StringComparer.Ordinal);
        Assert.Contains("\"$HOME\"", coloured, StringComparer.Ordinal);
        Assert.DoesNotContain('\r', InlineText.Of(code));
        Assert.Equal(TextWrapping.NoWrap, code.TextWrapping);
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Copy"));
    }

    [AvaloniaFact]
    public void Makes_only_web_links_openable_and_shows_where_they_go()
    {
        var (_, view) = Show(Reply);

        var links = Runs(view).Where(item => item.Link is not null).Select(item => item.Link!.Uri.AbsoluteUri).Distinct(StringComparer.Ordinal).ToList();
        var text = Texts(view).Single(item => item.StartsWith("Two,", StringComparison.Ordinal));

        Assert.Equal(["https://example.com/docs"], links);
        Assert.Contains("the docs (https://example.com/docs)", text, StringComparison.Ordinal);
        Assert.Contains("a script (javascript:alert(1))", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Shows_no_address_after_a_bare_domain()
    {
        var (_, view) = Show("Open www.example.com in a browser.");

        Assert.Contains("Open www.example.com in a browser.", Texts(view), StringComparer.Ordinal);
        Assert.DoesNotContain(Texts(view), text => text.Contains("(http", StringComparison.Ordinal));
        Assert.Contains(Runs(view), item => string.Equals(item.Link?.Uri.AbsoluteUri, "http://www.example.com/", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Formats_safe_html_and_shows_the_rest_as_text()
    {
        var (_, view) = Show(Reply);

        var text = Texts(view).Single(item => item.StartsWith("Press", StringComparison.Ordinal));

        Assert.StartsWith("Press Ctrl+C, not <script>alert(1)</script>.", text, StringComparison.Ordinal);
        Assert.Contains("E = mc²", text, StringComparison.Ordinal);
        Assert.Equal(HorizontalAlignment.Center, Blocks(view).Single(block => string.Equals(InlineText.Of(block), "a/b ≤ √x", StringComparison.Ordinal)).HorizontalAlignment);
        Assert.Contains("Einstein, 1905.", Texts(view), StringComparer.Ordinal);
    }

    [AvaloniaFact]
    public void Asks_before_loading_an_image()
    {
        var (_, view) = Show(Reply);

        var image = Assert.Single(view.GetVisualDescendants().OfType<MarkdownImage>());

        Assert.IsType<Button>(image.Child);
        Assert.Empty(view.GetVisualDescendants().OfType<Image>());
    }

    [AvaloniaFact]
    public void Selects_across_paragraphs_with_one_drag()
    {
        var (window, view) = Show("First paragraph here.\n\nSecond paragraph here.\n\nThird.");
        var blocks = Blocks(view);
        var from = blocks[0].TranslatePoint(new Point(2, 8), window)!.Value;
        var to = blocks[1].TranslatePoint(new Point(blocks[1].Bounds.Width - 1, 8), window)!.Value;

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to);
        window.MouseUp(to, MouseButton.Left);

        Assert.StartsWith("First paragraph here.\nSecond paragraph", view.SelectedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Third", view.SelectedText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Follows_text_as_it_streams()
    {
        var (_, view) = Show("**Hel");
        view.Text = "**Hello**";

        Assert.Equal(["Hello"], Texts(view));
    }
}
