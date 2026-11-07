using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Datapoint.Terminal.Markdown;

namespace Datapoint.Terminal.Editor;

// The editor's keys: Enter starts the next block (the next item, in a list), Backspace at the start joins the block before, the
// arrows at the first and last line move between blocks, and Escape shows the block rendered again.
public sealed partial class DocumentView : UserControl
{
    private DocumentViewModel? _document;

    public DocumentView()
    {
        InitializeComponent();
        BlockList.AddHandler(KeyDownEvent, OnBlockKeyDown, RoutingStrategies.Tunnel);
        BlockList.AddHandler(TextBox.TextChangedEvent, OnBlockTextChanged);
        BlockList.AddHandler(LostFocusEvent, OnBlockLostFocus, handledEventsToo: true);
        BlockList.AddHandler(MarkdownView.TextClickedEvent, OnTextClicked);
        BlockList.AddHandler(PointerReleasedEvent, OnBlockReleased);
        EndArea.PointerPressed += OnEndAreaPressed;
        Source.AddHandler(KeyDownEvent, OnSourceKeyDown, RoutingStrategies.Tunnel);
        Page.AddHandler(LostFocusEvent, OnSkillFieldLeft, handledEventsToo: true);
        Page.AddHandler(KeyDownEvent, OnSkillFieldKeyDown, RoutingStrategies.Tunnel);
    }

    // A skill's settings are written when the user leaves a field or presses Enter in it; Enter in the label box adds the label.
    private static void OnSkillFieldLeft(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not TextBox { DataContext: SkillHeaderViewModel skill } box || !box.Classes.Contains("skillField"))
        {
            return;
        }

        skill.Commit();
    }

    private static void OnSkillFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.Source is not TextBox { DataContext: SkillHeaderViewModel skill } box)
        {
            return;
        }

        if (box.Classes.Contains("label"))
        {
            skill.AddLabelCommand.Execute(null);
            e.Handled = true;
        }
        else if (box.Classes.Contains("skillField"))
        {
            skill.Commit();
            e.Handled = true;
        }
    }

    private static bool IsMac => OperatingSystem.IsMacOS();

    // The format menu's actions, for the text being edited; false when no text is, e.g. while a terminal has the keyboard.
    public bool Format(string marker) => Apply(box => MarkdownFormat.Wrap(box.Text ?? string.Empty, box.SelectionStart, box.SelectionEnd, marker));

    public bool FormatLink() => Apply(box => MarkdownFormat.Link(box.Text ?? string.Empty, box.SelectionStart, box.SelectionEnd));

    // Where typing should go when the document is shown: the block being edited, or the source.
    public void FocusText()
    {
        if (_document is { IsSourceMode: true })
        {
            Source.Focus();
            return;
        }

        if (_document?.EditingBlock is { } block && BoxFor(block) is { } box)
        {
            box.Focus();
            return;
        }

        Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _document?.FocusRequested = null;
        _document = DataContext as DocumentViewModel;
        _document?.FocusRequested = OnFocusRequested;
    }

    private bool Apply(Func<TextBox, FormatEdit> format)
    {
        var box = FocusedBox();
        if (box is null)
        {
            return false;
        }

        var edit = format(box);
        box.Text = edit.Text;
        box.SelectionStart = edit.SelectionStart;
        box.SelectionEnd = edit.SelectionEnd;
        return true;
    }

    private TextBox? FocusedBox()
    {
        if (Source.IsFocused)
        {
            return Source;
        }

        return _document?.EditingBlock is { } block && BoxFor(block) is { IsFocused: true } box ? box : null;
    }

    private TextBox? BoxFor(BlockViewModel block) =>
        BlockList.ContainerFromItem(block)?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();

    private static TextBox? BlockBox(object? source) =>
        (source as Visual)?.FindAncestorOfType<TextBox>(includeSelf: true) is { DataContext: BlockViewModel } box ? box : null;

    private void OnFocusRequested(BlockViewModel block, int caret) => Dispatcher.UIThread.Post(
        () =>
        {
            if (BoxFor(block) is not { } box)
            {
                return;
            }

            box.Focus();
            var at = Math.Clamp(caret, 0, box.Text?.Length ?? 0);
            box.SelectionStart = at;
            box.SelectionEnd = at;
            box.CaretIndex = at;
            box.BringIntoView();
        },
        DispatcherPriority.Background);

    private void OnTextClicked(object? sender, MarkdownClickEventArgs e)
    {
        if (_document is null || (e.Source as Control)?.DataContext is not BlockViewModel block)
        {
            return;
        }

        e.Handled = true;
        _document.Edit(block, SourceOffset.Of(block.Body, e.RenderedPrefix));
    }

    // A click on a rendered block away from its text, e.g. on a list's bullet, edits it from the end.
    private void OnBlockReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_document is null || BlockBox(e.Source) is not null || (e.Source as Control)?.DataContext is not BlockViewModel { IsEditing: false } block)
        {
            return;
        }

        e.Handled = true;
        _document.Edit(block);
    }

    private void OnEndAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        _document?.AddAtEnd();
    }

    private void OnBlockTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_document is null || BlockBox(e.Source) is not { IsFocused: true, DataContext: BlockViewModel { IsEditing: true } block })
        {
            return;
        }

        _document.OnBodyEdited(block);
    }

    // Leaving the document shows the block rendered again; moving to another block has done so already.
    private void OnBlockLostFocus(object? sender, RoutedEventArgs e)
    {
        if (BlockBox(e.Source) is not { DataContext: BlockViewModel block })
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                if (_document is null || !ReferenceEquals(_document.EditingBlock, block) || BlockList.IsKeyboardFocusWithin || TopLevel.GetTopLevel(this) is Window { IsActive: false })
                {
                    return;
                }

                _document.Commit();
            },
            DispatcherPriority.Background);
    }

    private void OnBlockKeyDown(object? sender, KeyEventArgs e)
    {
        if (_document is null || BlockBox(e.Source) is not { DataContext: BlockViewModel block } box)
        {
            return;
        }

        if (FormatKey(e) is { } format)
        {
            e.Handled = true;
            _ = format();
            return;
        }

        e.Handled = e.KeyModifiers == KeyModifiers.None && e.Key switch
        {
            Key.Enter when !block.AcceptsReturn => Split(box, block),
            Key.Back => Backspace(box, block),
            Key.Escape => Done(),
            Key.Up or Key.Down => MoveAtEdge(box, block, e.Key == Key.Up),
            _ => false,
        };
    }

    private void OnSourceKeyDown(object? sender, KeyEventArgs e)
    {
        if (FormatKey(e) is not { } format)
        {
            return;
        }

        e.Handled = true;
        _ = format();
    }

    // On macOS the Format menu has these keys; elsewhere the menu is drawn in the window, has no keys of its own, and Ctrl with a
    // letter means something to the terminal, so they're only taken here, while a document has the keyboard.
    private Func<bool>? FormatKey(KeyEventArgs e)
    {
        if (IsMac || e.KeyModifiers != KeyModifiers.Control)
        {
            return null;
        }

        return e.Key switch
        {
            Key.B => () => Format("**"),
            Key.I => () => Format("*"),
            Key.E => () => Format("`"),
            Key.K => FormatLink,
            _ => null,
        };
    }

    private bool Split(TextBox box, BlockViewModel block)
    {
        DeleteSelection(box);
        _document!.Split(block, box.CaretIndex);
        return true;
    }

    private bool Backspace(TextBox box, BlockViewModel block)
    {
        if (box.SelectionStart != box.SelectionEnd)
        {
            return false;
        }

        if (box.CaretIndex == 0)
        {
            _document!.JoinWithPrevious(block);
            return true;
        }

        // Just after a list's marker, Backspace takes the marker away, leaving a plain paragraph.
        if (!block.IsListItem || ListMarker.Of(block.Body) is not { } marker || box.CaretIndex != marker.Length)
        {
            return false;
        }

        box.Text = block.Body[marker.Length..];
        box.CaretIndex = 0;
        return true;
    }

    private bool Done()
    {
        _document!.Commit();
        Focus();
        return true;
    }

    // The text box moves the caret first; if it couldn't, the caret was on the first or last line and goes to the next block.
    private bool MoveAtEdge(TextBox box, BlockViewModel block, bool up)
    {
        var caret = box.CaretIndex;
        Dispatcher.UIThread.Post(
            () =>
            {
                if (box.CaretIndex != caret || _document is null || !ReferenceEquals(_document.EditingBlock, block))
                {
                    return;
                }

                if (up)
                {
                    _document.MoveToPrevious(block);
                    return;
                }

                _document.MoveToNext(block);
            },
            DispatcherPriority.Background);
        return false;
    }

    private static void DeleteSelection(TextBox box)
    {
        var start = Math.Min(box.SelectionStart, box.SelectionEnd);
        var end = Math.Max(box.SelectionStart, box.SelectionEnd);
        if (start == end || box.Text is not { } text)
        {
            return;
        }

        box.Text = text[..start] + text[end..];
        box.CaretIndex = start;
    }

    private void OnBoldClicked(object? sender, RoutedEventArgs e) => FormatFromMenu(sender, () => Format("**"));

    private void OnItalicClicked(object? sender, RoutedEventArgs e) => FormatFromMenu(sender, () => Format("*"));

    private void OnCodeClicked(object? sender, RoutedEventArgs e) => FormatFromMenu(sender, () => Format("`"));

    private void OnLinkClicked(object? sender, RoutedEventArgs e) => FormatFromMenu(sender, FormatLink);

    // The menu took the keyboard; the text box gets it back, with its selection, before the format is applied.
    private void FormatFromMenu(object? sender, Func<bool> format)
    {
        if (_document?.EditingBlock is not { } block || BoxFor(block) is not { } box)
        {
            return;
        }

        box.Focus();
        _ = format();
    }
}
