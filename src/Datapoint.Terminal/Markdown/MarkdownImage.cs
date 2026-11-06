using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Markdown;

/// <summary>
/// An image in a reply. It's only downloaded when the user asks, because loading it tells the server the user's
/// address and that they read the reply, and the address can come from untrusted terminal output.
/// </summary>
internal sealed class MarkdownImage : Border
{
    private const int MaxBytes = 10 * 1024 * 1024;

    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = MaxBytes };

    private readonly string _description;

    public MarkdownImage(string? url, string description)
    {
        _description = description;
        HorizontalAlignment = HorizontalAlignment.Left;
        var uri = Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme is "https" or "http" ? parsed : null;
        Child = uri is null ? Description() : LoadButton(uri);
    }

    private SelectableTextBlock Description() =>
        new() { Text = _description, FontStyle = FontStyle.Italic, TextWrapping = TextWrapping.Wrap, ContextFlyout = null };

    private Button LoadButton(Uri uri)
    {
        var content = new StackPanel { Spacing = 2 };
        if (_description.Length > 0)
        {
            content.Children.Add(new TextBlock { Text = _description, FontStyle = FontStyle.Italic, TextWrapping = TextWrapping.Wrap });
        }

        content.Children.Add(new TextBlock { Text = Strings.Format(Strings.MarkdownLoadImage, uri.IdnHost), TextWrapping = TextWrapping.Wrap });
        var button = new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Left };
        button.Classes.Add("plain");
        ToolTip.SetTip(button, Strings.Format(Strings.MarkdownLoadImageHelp, uri.IdnHost));
        AutomationProperties.SetHelpText(button, Strings.Format(Strings.MarkdownLoadImageHelp, uri.IdnHost));
        button.Click += (_, _) => _ = LoadAsync(button, uri);
        return button;
    }

    private async Task LoadAsync(Button button, Uri uri)
    {
        button.IsEnabled = false;
        try
        {
            var bytes = await s_http.GetByteArrayAsync(uri).ConfigureAwait(true);
            using var stream = new MemoryStream(bytes);
            var image = new Image { Source = new Bitmap(stream), MaxHeight = 320, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetName(image, _description);
            Child = image;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            Child = new TextBlock { Text = Strings.MarkdownImageFailed, TextWrapping = TextWrapping.Wrap };
        }
    }
}
