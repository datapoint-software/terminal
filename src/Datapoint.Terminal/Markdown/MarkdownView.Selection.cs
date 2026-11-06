using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Datapoint.Terminal.Markdown;

/// <summary>
/// Selection across the reply's text blocks: the view takes the pointer before the blocks do, works out which block
/// and character it's over, and sets each block's own selection, so a drag can run from one paragraph into the next.
/// </summary>
public sealed partial class MarkdownView
{
    private const double DragThreshold = 3;

    private static readonly Cursor s_textCursor = new(StandardCursorType.Ibeam);
    private static readonly Cursor s_linkCursor = new(StandardCursorType.Hand);
    private static WeakReference<MarkdownView>? s_selecting;

    private (int Block, int Index)? _anchor;
    private Point _pressedAt;
    private bool _dragged;
    private LinkSpan? _pressedLink;
    private List<SelectableTextBlock> _blocks = [];

    private List<SelectableTextBlock> TextBlocks() => [.. this.GetVisualDescendants().OfType<SelectableTextBlock>()];

    public void SelectAll()
    {
        _blocks = TextBlocks();
        foreach (var block in _blocks)
        {
            block.SelectionStart = 0;
            block.SelectionEnd = InlineText.Of(block).Length;
        }
    }

    public void ClearSelection()
    {
        foreach (var block in TextBlocks())
        {
            block.ClearSelection();
        }
    }

    private static SelectableTextBlock? BlockOf(object? source) => (source as Visual)?.FindAncestorOfType<SelectableTextBlock>(includeSelf: true);

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || BlockOf(e.Source) is not { } block)
        {
            return;
        }

        TakeSelection();
        Focus();
        _blocks = TextBlocks();
        _anchor = Locate(e);
        _pressedAt = e.GetPosition(this);
        _dragged = false;
        _pressedLink = LinkUnder(block, e);
        SelectByClicks(_anchor.Value, e.ClickCount);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMovedTunnel(object? sender, PointerEventArgs e)
    {
        if (_anchor is not { } anchor)
        {
            if (BlockOf(e.Source) is { } block)
            {
                block.Cursor = LinkUnder(block, e) is null ? s_textCursor : s_linkCursor;
            }

            return;
        }

        var distance = e.GetPosition(this) - _pressedAt;
        _dragged |= Math.Abs(distance.X) > DragThreshold || Math.Abs(distance.Y) > DragThreshold;
        if (_dragged)
        {
            Select(anchor, Locate(e));
        }

        e.Handled = true;
    }

    private void OnPointerReleasedTunnel(object? sender, PointerReleasedEventArgs e)
    {
        if (_anchor is null)
        {
            return;
        }

        _anchor = null;
        e.Pointer.Capture(null);
        e.Handled = true;
        if (_dragged || _pressedLink is not { } link)
        {
            return;
        }

        ClearSelection();
        OfferLink(link.Uri);
    }

    /// <summary>Clears a selection left in another reply, so only one is selected at a time.</summary>
    private void TakeSelection()
    {
        if (s_selecting?.TryGetTarget(out var previous) == true && previous != this)
        {
            previous.ClearSelection();
        }

        s_selecting = new WeakReference<MarkdownView>(this);
    }

    private static LinkSpan? LinkUnder(SelectableTextBlock block, PointerEventArgs e)
    {
        var hit = block.TextLayout.HitTestPoint(e.GetPosition(block));
        return hit.IsInside ? InlineText.LinkAt(block, hit.CharacterHit.FirstCharacterIndex) : null;
    }

    /// <summary>The block and character under the pointer; above a block means its start, past the last one its end.</summary>
    private (int Block, int Index) Locate(PointerEventArgs e)
    {
        for (var index = 0; index < _blocks.Count; index++)
        {
            var block = _blocks[index];
            var point = e.GetPosition(block);
            if (point.Y < 0)
            {
                return (index, 0);
            }

            if (point.Y <= block.Bounds.Height && point.X <= block.Bounds.Width)
            {
                return (index, block.TextLayout.HitTestPoint(point).TextPosition);
            }
        }

        return _blocks.Count == 0 ? (0, 0) : (_blocks.Count - 1, InlineText.Of(_blocks[^1]).Length);
    }

    private void SelectByClicks((int Block, int Index) at, int clicks)
    {
        if (clicks >= 3)
        {
            Select((at.Block, 0), (at.Block, InlineText.Of(_blocks[at.Block]).Length));
            return;
        }

        if (clicks != 2)
        {
            Select(at, at);
            return;
        }

        var text = InlineText.Of(_blocks[at.Block]);
        var start = at.Index;
        var end = at.Index;
        while (start > 0 && char.IsLetterOrDigit(text[start - 1]))
        {
            start--;
        }

        while (end < text.Length && char.IsLetterOrDigit(text[end]))
        {
            end++;
        }

        Select((at.Block, start), (at.Block, end));
    }

    private void Select((int Block, int Index) from, (int Block, int Index) to)
    {
        var forward = from.Block < to.Block || (from.Block == to.Block && from.Index <= to.Index);
        var (start, end) = forward ? (from, to) : (to, from);
        for (var index = 0; index < _blocks.Count; index++)
        {
            var block = _blocks[index];
            if (index < start.Block || index > end.Block)
            {
                block.ClearSelection();
                continue;
            }

            block.SelectionStart = index == start.Block ? start.Index : 0;
            block.SelectionEnd = index == end.Block ? end.Index : InlineText.Of(block).Length;
        }
    }
}
