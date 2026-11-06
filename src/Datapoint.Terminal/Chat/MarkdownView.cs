using Avalonia;
using Avalonia.Controls;

namespace Datapoint.Terminal.Chat;

/// <summary>
/// Shows text written in Markdown, as models write their replies. Links aren't clickable and images aren't fetched:
/// a reply can repeat text from the terminal, which is untrusted, so we show where a link points instead of following it.
/// </summary>
public sealed class MarkdownView : StackPanel
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<MarkdownView, string?>(nameof(Text));

    public MarkdownView() => Spacing = 8;

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty)
        {
            return;
        }

        Children.Clear();
        Children.AddRange(new MarkdownBuilder(this).Build(Text ?? string.Empty));
    }
}
