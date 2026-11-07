namespace Datapoint.Terminal.Agent.Web;

public sealed record HtmlPage(string? Title, string Text, IReadOnlyList<Uri> Links);
