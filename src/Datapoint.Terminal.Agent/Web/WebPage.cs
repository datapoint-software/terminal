namespace Datapoint.Terminal.Agent.Web;

// Problem is set when the page couldn't be read; the text is then empty.
public sealed record WebPage(Uri Address, string? Title, string Text, string? Problem)
{
    // The addresses the page links to.
    public IReadOnlyList<Uri> Links { get; init; } = [];

    public static WebPage Failed(Uri address, string problem) => new(address, null, string.Empty, problem);
}
