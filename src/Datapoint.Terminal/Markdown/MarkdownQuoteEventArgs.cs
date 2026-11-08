using Avalonia.Interactivity;

namespace Datapoint.Terminal.Markdown;

// Text is the selection as shown, each paragraph on its own line.
public sealed class MarkdownQuoteEventArgs(string text) : RoutedEventArgs(MarkdownView.QuoteRequestedEvent)
{
    public string Text { get; } = text;
}
