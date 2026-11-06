using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Datapoint.Terminal.Chat;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class MarkdownViewTests
{
    private const string Reply = """
        ## Disk usage

        This PC's hostname is **MacBook-Pro.local**, and the *home* folder uses `42G`.

        1. Run `df -h`
        2. Check ~~the logs~~ the cache

        - One
        - Two, with [the docs](https://example.com/docs)

        > Quoted output

        ```
        Filesystem  Size  Used
        /dev/disk1  460G  42G
        ```

        | Folder | Size |
        | --- | --- |
        | ~/Library | 30G |

        <script>alert(1)</script>
        """;

    private static IEnumerable<string> Texts(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Inlines is { Count: > 0 } inlines ? string.Concat(Runs(inlines).Select(run => run.Text)) : text.Text ?? string.Empty);

    private static IEnumerable<Run> Runs(InlineCollection inlines) =>
        inlines.SelectMany(inline => inline switch
        {
            Run run => [run],
            Span span => Runs(span.Inlines),
            _ => [],
        });

    [AvaloniaFact]
    public void Renders_a_reply_without_its_markup()
    {
        var view = new MarkdownView { Text = Reply };
        var window = new Window { Width = 380, Height = 700, Content = new ScrollViewer { Content = view } };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-markdown.png", new PngBitmapEncoderOptions());

        var texts = Texts(view).ToList();
        Assert.Contains("Disk usage", texts, StringComparer.Ordinal);
        Assert.Contains("This PC's hostname is MacBook-Pro.local, and the home folder uses 42G.", texts, StringComparer.Ordinal);
        Assert.Contains("1.", texts, StringComparer.Ordinal);
        Assert.Contains("Two, with the docs (https://example.com/docs)", texts, StringComparer.Ordinal);
        Assert.Contains(texts, text => text.StartsWith("Filesystem", StringComparison.Ordinal));
        Assert.Contains("~/Library", texts, StringComparer.Ordinal);
        Assert.Contains(texts, text => text.Contains("<script>", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, text => text.Contains("**", StringComparison.Ordinal) || text.Contains("```", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Follows_text_as_it_streams()
    {
        var view = new MarkdownView { Text = "**Hel" };
        view.Text = "**Hello**";

        new Window { Content = view }.Show();

        Assert.Equal(["Hello"], Texts(view), StringComparer.Ordinal);
    }
}
