using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Web;
using Datapoint.Terminal.Chat;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class ChatWebTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WebAccessRequest Search => new(WebAccessKind.Search, "rsync archive flag", "Check what -a includes") { Service = "Brave Search" };

    [Fact]
    public async Task Offers_only_the_three_choices_and_maps_each()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());

        var denied = chat.RequestWebAccessAsync(Search, Token);
        Assert.Equal<string>(["1 Don't allow", "2 Allow once", "3 Allow always"], [.. chat.Question!.Rows.Select(row => $"{row.NumberText} {row.Label}")]);
        Assert.Equal("The agent wants to use the web", chat.Question.HeaderText);
        Assert.Equal("Search the web for “rsync archive flag”?", chat.Question.Text);
        Assert.Contains("Brave Search", chat.Question.Details, StringComparison.Ordinal);
        chat.Question.ChooseNumber(1);
        var once = chat.RequestWebAccessAsync(Search, Token);
        chat.Question!.ChooseNumber(2);
        var always = chat.RequestWebAccessAsync(Search, Token);
        chat.Question!.ChooseNumber(3);

        Assert.Equal(WebAccess.Denied, await denied);
        Assert.Equal(WebAccess.Once, await once);
        Assert.Equal(WebAccess.Always, await always);
    }

    [Fact]
    public async Task Skipping_is_not_allowing_and_typed_words_cannot_allow()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());

        var access = chat.RequestWebAccessAsync(Search, Token);
        Assert.False(chat.Question!.ChooseNumber(4));
        Assert.DoesNotContain(chat.Question.Rows, row => row.IsOther);
        chat.Question.SkipCommand.Execute(null);

        Assert.Equal(WebAccess.Denied, await access);
        Assert.Equal("Asked: The agent asked: Search the web for “rsync archive flag”? · Don't allow", $"{chat.Items[^1].Kind}: {chat.Items[^1].Text}");
    }

    [Fact]
    public async Task Saves_allow_always_and_warns_when_the_model_is_local()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread()) { IsModelLocal = true };
        var saved = 0;
        chat.WebAlwaysAllowed += (_, _) => saved++;

        var access = chat.RequestWebAccessAsync(new WebAccessRequest(WebAccessKind.ReadPage, "https://docs.example.org/", "Read the [manual](https://evil.example)"), Token);
        Assert.Contains("goes out to the internet", chat.Question!.Details, StringComparison.Ordinal);
        Assert.Contains("\\[manual\\]", chat.Question.Details, StringComparison.Ordinal);
        chat.Question.ChooseNumber(3);

        Assert.Equal(WebAccess.Always, await access);
        Assert.Equal(1, saved);
    }

    [Fact]
    public void Shows_a_search_with_its_sources_as_links()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());

        chat.Show(new AgentEventArgs(AgentEventKind.WebUsed, "rsync flags")
        {
            WebKind = WebAccessKind.Search,
            Service = "Brave Search",
            Sources = [new WebSearchResult("rsync(1) [man]", new Uri("https://example.org/rsync(1)"), null)],
        });

        var item = Assert.Single(chat.Items);
        Assert.Equal(ChatItemKind.Web, item.Kind);
        Assert.Equal("Searched the web for “rsync flags” · Brave Search", item.Text);
        Assert.Equal("- [rsync\\(1\\) \\[man\\]](https://example.org/rsync%281%29)", item.Detail);
    }

    [AvaloniaFact]
    public void Renders_the_question_and_a_page_read()
    {
        using var chat = new ChatViewModel(new ImmediateUiThread());
        chat.Items.Add(new ChatItemViewModel(ChatItemKind.User, "What does rsync -a include?"));
        chat.Show(new AgentEventArgs(AgentEventKind.WebUsed, "rsync archive flag")
        {
            WebKind = WebAccessKind.Search,
            Service = "Brave Search",
            Sources = [new WebSearchResult("rsync(1) — Linux manual page", new Uri("https://man7.org/linux/man-pages/man1/rsync.1.html"), null), new WebSearchResult("Rsync options explained", new Uri("https://example.org/rsync"), null)],
        });
        _ = chat.RequestWebAccessAsync(new WebAccessRequest(WebAccessKind.ReadPage, "https://man7.org/linux/man-pages/man1/rsync.1.html", "Check what -a includes"), Token);
        var window = new Window { Width = 520, Height = 860, Content = new ChatView { DataContext = chat } };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/chat-web.png", new PngBitmapEncoderOptions());

        Assert.NotNull(frame);
    }
}
