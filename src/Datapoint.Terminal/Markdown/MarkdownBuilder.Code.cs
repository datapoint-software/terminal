using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Datapoint.Terminal.Resources;
using Markdig.Syntax;

namespace Datapoint.Terminal.Markdown;

internal sealed partial class MarkdownBuilder
{
    private Border Code(CodeBlock code)
    {
        var text = code.Lines.ToString().TrimEnd('\r', '\n');
        var info = (code as FencedCodeBlock)?.Info;
        var body = new SelectableTextBlock { Inlines = Highlighted(text, info), TextWrapping = TextWrapping.NoWrap, ContextFlyout = null };
        body.Classes.Add("mono");
        var language = Brush(new TextBlock { Text = info?.Split(' ')[0] ?? string.Empty, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, TextBlock.ForegroundProperty, "SecondaryLabelBrush");
        var copy = CopyButton(text);
        Grid.SetColumn(copy, 1);
        var border = new Border
        {
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, -4, -6, 0), Children = { language, copy } },
                    new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled },
                },
            },
        };
        border.Classes.Add("code");
        return border;
    }

    private InlineCollection Highlighted(string code, string? info)
    {
        var inlines = new InlineCollection();
        foreach (var (text, kind) in SyntaxHighlighter.Highlight(code, info))
        {
            var run = new Run(text);
            inlines.Add(kind == SyntaxKind.Plain ? run : Brush(run, TextElement.ForegroundProperty, $"Syntax{kind}Brush"));
        }

        return inlines;
    }

    private static Button CopyButton(string text)
    {
        var button = new Button { Content = Strings.MarkdownCopy, FontSize = 11, Padding = new Thickness(6, 1), MinHeight = 0 };
        button.Classes.Add("plain");
        AutomationProperties.SetName(button, Strings.MarkdownCopyCode);
        button.Click += (_, _) => _ = CopyAsync(button, text);
        return button;
    }

    private static async Task CopyAsync(Button button, string text)
    {
        if (TopLevel.GetTopLevel(button)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(text).ConfigureAwait(true);
        button.Content = Strings.MarkdownCopied;
        DispatcherTimer.RunOnce(() => button.Content = Strings.MarkdownCopy, TimeSpan.FromSeconds(1.5));
    }

    private SelectableTextBlock DisplayMath(string tex)
    {
        var text = NewText(MathText.ToUnicode(tex));
        text.FontStyle = FontStyle.Italic;
        text.FontSize = 15;
        text.TextAlignment = TextAlignment.Center;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.LineHeight = double.NaN;
        return text;
    }
}
