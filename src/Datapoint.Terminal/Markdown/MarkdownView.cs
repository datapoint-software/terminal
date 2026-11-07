using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Markdown;

// Links open only after the user confirms the full address, and images load only when asked: a reply can repeat
// text from the terminal, which is untrusted.
public sealed partial class MarkdownView : StackPanel
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<MarkdownView, string?>(nameof(Text));

    // A document, rather than a chat reply: larger type, matching the editor's.
    public static readonly StyledProperty<bool> IsDocumentProperty = AvaloniaProperty.Register<MarkdownView, bool>(nameof(IsDocument));

    public MarkdownView()
    {
        Spacing = 8;
        Focusable = true;
        FocusAdorner = null;
        Background = Brushes.Transparent;
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMovedTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleasedTunnel, RoutingStrategies.Tunnel);
        ContextFlyout = Menu();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsDocument
    {
        get => GetValue(IsDocumentProperty);
        set => SetValue(IsDocumentProperty, value);
    }

    // A click that neither selected text nor followed a link, e.g. to start editing there.
    public static readonly RoutedEvent<MarkdownClickEventArgs> TextClickedEvent = RoutedEvent.Register<MarkdownView, MarkdownClickEventArgs>(nameof(TextClicked), RoutingStrategies.Bubble);

    public event EventHandler<MarkdownClickEventArgs>? TextClicked
    {
        add => AddHandler(TextClickedEvent, value);
        remove => RemoveHandler(TextClickedEvent, value);
    }

    private static KeyModifiers CommandModifier => OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty && change.Property != IsDocumentProperty)
        {
            return;
        }

        _anchor = null;
        Children.Clear();
        Children.AddRange(new MarkdownBuilder(this).Build(Text ?? string.Empty));
        if (LinkRow() is not { } row)
        {
            return;
        }

        Children.Add(row);
    }

    // Links sit inside text, where the keyboard can't reach them, so each also gets a button below the reply. It's named with
    // its label and address for screen readers, and opens through the same confirmation as a click.
    private WrapPanel? LinkRow()
    {
        var links = this.GetLogicalDescendants().OfType<SelectableTextBlock>()
            .Where(block => block.Inlines is not null)
            .SelectMany(block => InlineText.Runs(block.Inlines!))
            .Where(item => item.Link is not null)
            .GroupBy(item => item.Link!)
            .Select(group => (Link: group.Key, Label: string.Concat(group.Select(item => item.Run.Text)).Trim()))
            .ToList();
        if (links.Count == 0)
        {
            return null;
        }

        var row = new WrapPanel { ItemSpacing = 6, LineSpacing = 4 };
        row.Children.Add(new TextBlock { Text = Strings.MarkdownLinks, Classes = { "secondary" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        foreach (var (link, label) in links)
        {
            var text = label.Length == 0 ? link.Uri.AbsoluteUri : label;
            var button = new Button { Content = text, Classes = { "plain" } };
            ToolTip.SetTip(button, link.Uri.AbsoluteUri);
            Avalonia.Automation.AutomationProperties.SetName(button, Strings.Format(Strings.MarkdownLinkName, text, link.Uri.AbsoluteUri));
            button.Click += (_, _) => OfferLink(link.Uri, button);
            row.Children.Add(button);
        }

        return row;
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

    private MenuFlyout Menu()
    {
        var copy = new MenuItem { Header = Strings.MarkdownCopy, InputGesture = new KeyGesture(Key.C, CommandModifier) };
        copy.Click += (_, _) => _ = CopyAsync();
        var all = new MenuItem { Header = Strings.MarkdownSelectAll, InputGesture = new KeyGesture(Key.A, CommandModifier) };
        all.Click += (_, _) => SelectAll();
        return new MenuFlyout { Items = { copy, all } };
    }

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

    private void OfferLink(Uri uri, Control? anchor = null)
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
        if (anchor is not null)
        {
            flyout.Opened += (_, _) => cancel.Focus();
            flyout.ShowAt(anchor);
            return;
        }

        flyout.ShowAt(this, showAtPointer: true);
    }
}
