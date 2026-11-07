using Avalonia.Interactivity;

namespace Datapoint.Terminal.Markdown;

// RenderedPrefix is the text shown before the click, each paragraph on its own line.
public sealed class MarkdownClickEventArgs(string renderedPrefix) : RoutedEventArgs(MarkdownView.TextClickedEvent)
{
    public string RenderedPrefix { get; } = renderedPrefix;
}
