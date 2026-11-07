using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;

namespace Datapoint.Terminal.Shell;

// Arranges the terminals shown together in one row or one column, with a hairline and a splitter between each pair and an
// accent line along the edge of the active one. Terminals that aren't shown stay in the grid, hidden, so their shells keep
// running and their scrollback stays put.
public sealed class PaneLayout(Grid host)
{
    public const double MinimumSize = 120;

    private readonly List<Rectangle> _lines = [];
    private readonly List<GridSplitter> _splitters = [];
    private readonly Rectangle _active = new() { IsHitTestVisible = false, ZIndex = 2 };

    public Rectangle ActiveLine => _active;

    // sizes are the panes' relative weights, from Measure, so a group comes back as the user left it.
    public void Apply(IReadOnlyList<Control> all, IReadOnlyList<Control> shown, bool beside, int active, IReadOnlyList<double>? sizes = null)
    {
        Define(shown.Count, beside, sizes);
        foreach (var control in all)
        {
            control.IsVisible = false;
        }

        for (var index = 0; index < shown.Count; index++)
        {
            Place(shown[index], beside, index * 2);
            shown[index].IsVisible = true;
        }

        PlaceDividers(shown.Count, beside);
        PlaceActive(shown.Count, beside, active);
    }

    // The panes' current widths or heights, for Apply to restore.
    public IReadOnlyList<double> Measure()
    {
        var beside = host.ColumnDefinitions.Count > 1;
        IEnumerable<double> lengths = beside
            ? host.ColumnDefinitions.Select(column => column.ActualWidth)
            : host.RowDefinitions.Select(row => row.ActualHeight);
        return [.. lengths.Where((_, index) => index % 2 == 0)];
    }

    private void Define(int count, bool beside, IReadOnlyList<double>? sizes)
    {
        host.ColumnDefinitions.Clear();
        host.RowDefinitions.Clear();
        var useSizes = sizes is not null && sizes.Count == count && sizes.All(size => size > 0);
        for (var index = 0; index < count; index++)
        {
            if (index > 0)
            {
                Add(beside, new GridLength(1), 0);
            }

            Add(beside, new GridLength(useSizes ? sizes![index] : 1, GridUnitType.Star), count > 1 ? MinimumSize : 0);
        }
    }

    private void Add(bool beside, GridLength length, double minimum)
    {
        if (beside)
        {
            host.ColumnDefinitions.Add(new ColumnDefinition(length) { MinWidth = minimum });
            return;
        }

        host.RowDefinitions.Add(new RowDefinition(length) { MinHeight = minimum });
    }

    private void PlaceDividers(int count, bool beside)
    {
        while (_lines.Count < count - 1)
        {
            var line = new Rectangle();
            line.Bind(Shape.FillProperty, line.GetResourceObservable("SeparatorBrush"));
            var splitter = new GridSplitter { ZIndex = 1, Background = Avalonia.Media.Brushes.Transparent };
            _lines.Add(line);
            _splitters.Add(splitter);
            host.Children.Add(line);
            host.Children.Add(splitter);
        }

        for (var index = 0; index < _lines.Count; index++)
        {
            var isUsed = index < count - 1;
            _lines[index].IsVisible = _splitters[index].IsVisible = isUsed;
            if (!isUsed)
            {
                continue;
            }

            Place(_lines[index], beside, (index * 2) + 1);
            Place(_splitters[index], beside, (index * 2) + 1);
            _splitters[index].ResizeDirection = beside ? GridResizeDirection.Columns : GridResizeDirection.Rows;
            _splitters[index].Margin = beside ? new Thickness(-3, 0) : new Thickness(0, -3);
        }
    }

    // Only drawn when there's more than one pane: with one, the focused terminal is plain to see.
    private void PlaceActive(int count, bool beside, int active)
    {
        if (!host.Children.Contains(_active))
        {
            _active.Bind(Shape.FillProperty, _active.GetResourceObservable("AccentBrush"));
            host.Children.Add(_active);
        }

        _active.IsVisible = count > 1 && active >= 0 && active < count;
        if (!_active.IsVisible)
        {
            return;
        }

        Place(_active, beside, active * 2);
        _active.Height = 2;
        _active.Width = double.NaN;
        _active.VerticalAlignment = VerticalAlignment.Top;
        _active.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private static void Place(Control control, bool beside, int index)
    {
        Grid.SetColumn(control, beside ? index : 0);
        Grid.SetRow(control, beside ? 0 : index);
    }
}
