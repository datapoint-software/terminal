using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Markdown;

/// <summary>
/// Shows text written in Markdown, as models write their replies. A selection can run across the whole reply.
/// Links open only after the user confirms the full address, and images load only when asked: a reply can repeat
/// text from the terminal, which is untrusted.
/// </summary>
public sealed partial class MarkdownView : StackPanel
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<MarkdownView, string?>(nameof(Text));

    public MarkdownView()
    {
        Spacing = 8;
        Focusable = true;
        FocusAdorner = null;
        Background = Brushes.Transparent;
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMovedTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleasedTunnel, RoutingStrategies.Tunnel);
        ContextMenu = Menu();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static KeyModifiers CommandModifier => OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty)
        {
            return;
        }

        _anchor = null;
        Children.Clear();
        Children.AddRange(new MarkdownBuilder(this).Build(Text ?? string.Empty));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyModifiers != CommandModifier)
        {
            return;
        }

        if (e.Key == Key.C)
        {
            _ = CopyAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.A)
        {
            SelectAll();
            e.Handled = true;
        }
    }

    private ContextMenu Menu()
    {
        var copy = new MenuItem { Header = Strings.MarkdownCopy, InputGesture = new KeyGesture(Key.C, CommandModifier) };
        copy.Click += (_, _) => _ = CopyAsync();
        var all = new MenuItem { Header = Strings.MarkdownSelectAll, InputGesture = new KeyGesture(Key.A, CommandModifier) };
        all.Click += (_, _) => SelectAll();
        return new ContextMenu { Items = { copy, all } };
    }

    /// <summary>The selected text, block by block, one line apart.</summary>
    public string SelectedText =>
        string.Join('\n', TextBlocks().Where(block => block.SelectionStart != block.SelectionEnd).Select(block => block.SelectedText));

    private async Task CopyAsync()
    {
        var text = SelectedText;
        if (text.Length == 0 || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(text).ConfigureAwait(true);
    }

    private void OfferLink(Uri uri)
    {
        var open = new Button { Content = Strings.MarkdownOpen };
        open.Classes.Add("primary");
        var cancel = new Button { Content = Strings.MarkdownCancel };
        var flyout = new Flyout
        {
            Content = new StackPanel
            {
                Spacing = 8,
                MaxWidth = 320,
                Children =
                {
                    new TextBlock { Text = Strings.MarkdownOpenLinkQuestion, FontWeight = FontWeight.SemiBold },
                    new SelectableTextBlock { Text = uri.AbsoluteUri, TextWrapping = TextWrapping.Wrap },
                    new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Children = { cancel, open } },
                },
            },
        };
        cancel.Click += (_, _) => flyout.Hide();
        open.Click += (_, _) =>
        {
            flyout.Hide();
            _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(uri);
        };
        flyout.ShowAt(this, showAtPointer: true);
    }
}
