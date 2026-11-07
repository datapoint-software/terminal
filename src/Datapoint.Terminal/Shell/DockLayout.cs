using Avalonia;
using Avalonia.Controls;

namespace Datapoint.Terminal.Shell;

// Arranges a three-cell grid: the main pane, which takes what's left, a hairline with a splitter over it, and the docked
// pane, on either side or below. A hidden docked pane takes no space.
public sealed class DockLayout(Grid body, Control main, Control docked, Control separator, GridSplitter splitter)
{
    public const double MinimumSize = 200;

    public static bool IsBeside(TerminalSide side) => side is TerminalSide.Left or TerminalSide.Right;

    public void Apply(TerminalSide side, bool isHidden, double width, double height)
    {
        var beside = IsBeside(side);
        var dockedIndex = side == TerminalSide.Left ? 0 : 2;
        var size = new GridLength(isHidden ? 0 : beside ? width : height);
        var line = new GridLength(isHidden ? 0 : 1);
        GridLength[] lengths = dockedIndex == 0 ? [size, line, GridLength.Star] : [GridLength.Star, line, size];
        Define(beside, lengths, dockedIndex, isHidden);

        Place(docked, beside, dockedIndex);
        Place(main, beside, 2 - dockedIndex);
        Place(separator, beside, 1);
        Place(splitter, beside, 1);
        splitter.ResizeDirection = beside ? GridResizeDirection.Columns : GridResizeDirection.Rows;
        splitter.Margin = beside ? new Thickness(-3, 0) : new Thickness(0, -3);
        docked.IsVisible = separator.IsVisible = splitter.IsVisible = !isHidden;
    }

    // Both panes keep a usable size while the splitter is dragged; the hairline and a hidden pane need none.
    private void Define(bool beside, GridLength[] lengths, int dockedIndex, bool isHidden)
    {
        double Minimum(int index) => index == 1 || (index == dockedIndex && isHidden) ? 0 : MinimumSize;
        body.ColumnDefinitions.Clear();
        body.RowDefinitions.Clear();
        if (beside)
        {
            body.ColumnDefinitions.AddRange(lengths.Select((length, index) => new ColumnDefinition(length) { MinWidth = Minimum(index) }));
            return;
        }

        body.RowDefinitions.AddRange(lengths.Select((length, index) => new RowDefinition(length) { MinHeight = Minimum(index) }));
    }

    private static void Place(Control control, bool beside, int index)
    {
        Grid.SetColumn(control, beside ? index : 0);
        Grid.SetRow(control, beside ? 0 : index);
    }
}
