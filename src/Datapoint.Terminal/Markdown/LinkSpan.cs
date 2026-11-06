using Avalonia.Controls.Documents;

namespace Datapoint.Terminal.Markdown;

/// <summary>A link the user can open, after confirming. Only http, https and mailto links become one.</summary>
internal sealed class LinkSpan(Uri uri) : Span
{
    public Uri Uri { get; } = uri;

    public static Uri? Openable(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" ? uri : null;
}
