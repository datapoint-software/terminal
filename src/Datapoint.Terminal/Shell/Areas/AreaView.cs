using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;

namespace Datapoint.Terminal.Shell.Areas;

// Builds a layout into nested grids, each split a hairline with a wider invisible handle for dragging. Areas that are hidden
// take no space and wait in a hidden panel, so they keep their state and stay in the window. A collapsed area keeps only the
// size its own control asks for, beside the area it meets, with no divider to drag.
public sealed class AreaView
{
    private readonly Panel _host;
    private readonly IReadOnlyDictionary<AreaKind, Control> _areas;
    private readonly Panel _content = new();
    private readonly Panel _hidden = new() { IsVisible = false };
    private string? _shown;
    private AreaKind? _collapsed;

    public AreaView(Panel host, IReadOnlyDictionary<AreaKind, Control> areas)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _areas = areas;
        host.Children.Add(_content);
        host.Children.Add(_hidden);
    }

    // After the user drags a divider; the split's size has been updated.
    public event EventHandler? Resized;

    // Whether the collapsed area sits beside the one it meets, rather than above or below it, so it's a column, not a row.
    public bool IsCollapsedBeside { get; private set; }

    public void Show(AreaNode root, Func<AreaKind, bool> isShown, AreaKind? collapsed = null)
    {
        ArgumentNullException.ThrowIfNull(isShown);
        var key = new StringBuilder(collapsed is { } kind ? kind + ":" : string.Empty);
        Describe(root, isShown, key);
        if (string.Equals(key.ToString(), _shown, StringComparison.Ordinal))
        {
            return;
        }

        // Moving a control between grids takes the keyboard focus from it, so it's given back after.
        var focused = TopLevel.GetTopLevel(_host)?.FocusManager?.GetFocusedElement() as Control;
        _shown = key.ToString();
        _collapsed = collapsed;
        foreach (var area in _areas.Values)
        {
            (area.Parent as Panel)?.Children.Remove(area);
        }

        ClearSplits(_content);
        _content.Children.Clear();
        if (Build(root, isShown) is { } built)
        {
            _content.Children.Add(built);
        }

        _hidden.Children.AddRange(_areas.Where(area => !isShown(area.Key)).Select(area => area.Value));
        if (focused is not { IsEffectivelyVisible: true })
        {
            return;
        }

        focused.Focus();
    }

    // Forgets what's shown, for when the same layout has to be built again with new sizes.
    public void Invalidate() => _shown = null;

    private static void ClearSplits(Panel panel)
    {
        foreach (var grid in panel.Children.OfType<Grid>().Where(grid => grid.Tag is AreaSplit))
        {
            ClearSplits(grid);
            grid.Children.Clear();
        }
    }

    private static void Describe(AreaNode node, Func<AreaKind, bool> isShown, StringBuilder key)
    {
        switch (node)
        {
            case AreaLeaf leaf:
                key.Append(isShown(leaf.Kind) ? leaf.Kind.ToString() : "-");
                return;
            case AreaSplit split:
                key.Append(split.IsRow ? "row(" : "column(").Append(split.Size).Append(';');
                Describe(split.First, isShown, key);
                key.Append(',');
                Describe(split.Second, isShown, key);
                key.Append(')');
                return;
        }
    }

    private Control? Build(AreaNode node, Func<AreaKind, bool> isShown)
    {
        if (node is AreaLeaf leaf)
        {
            return isShown(leaf.Kind) ? _areas[leaf.Kind] : null;
        }

        if (node is not AreaSplit split)
        {
            return null;
        }

        var first = Build(split.First, isShown);
        var second = Build(split.Second, isShown);
        if (first is null || second is null)
        {
            return first ?? second;
        }

        if (_collapsed is { } kind && (ReferenceEquals(first, _areas[kind]) || ReferenceEquals(second, _areas[kind])))
        {
            IsCollapsedBeside = split.IsRow;
            return Collapsed(split, first, second, ReferenceEquals(first, _areas[kind]));
        }

        return Split(split, first, second, isShown);
    }

    private static Grid Collapsed(AreaSplit split, Control first, Control second, bool isFirstCollapsed)
    {
        var grid = new Grid { Tag = split };
        GridLength[] lengths = isFirstCollapsed ? [GridLength.Auto, new GridLength(1), GridLength.Star] : [GridLength.Star, new GridLength(1), GridLength.Auto];
        if (split.IsRow)
        {
            grid.ColumnDefinitions.AddRange(lengths.Select(length => new ColumnDefinition(length)));
        }
        else
        {
            grid.RowDefinitions.AddRange(lengths.Select(length => new RowDefinition(length)));
        }

        var separator = new Rectangle();
        separator.Classes.Add("areaseparator");
        Control[] cells = [first, separator, second];
        for (var cell = 0; cell < cells.Length; cell++)
        {
            Grid.SetColumn(cells[cell], split.IsRow ? cell : 0);
            Grid.SetRow(cells[cell], split.IsRow ? 0 : cell);
            grid.Children.Add(cells[cell]);
        }

        return grid;
    }

    private Grid Split(AreaSplit split, Control first, Control second, Func<AreaKind, bool> isShown)
    {
        var grid = Define(split, isShown);
        var separator = new Rectangle();
        separator.Classes.Add("areaseparator");
        var splitter = new GridSplitter
        {
            ResizeDirection = split.IsRow ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            Margin = split.IsRow ? new Thickness(-3, 0) : new Thickness(0, -3),
            ZIndex = 1,
            Focusable = false,
        };
        var fixedPart = split.FixesFirst ? first : second;
        splitter.DragCompleted += (_, _) =>
        {
            split.Size = Math.Round(split.IsRow ? fixedPart.Bounds.Width : fixedPart.Bounds.Height);
            Resized?.Invoke(this, EventArgs.Empty);
        };
        Control[] cells = [first, separator, splitter, second];
        int[] indexes = [0, 1, 1, 2];
        for (var cell = 0; cell < cells.Length; cell++)
        {
            Grid.SetColumn(cells[cell], split.IsRow ? indexes[cell] : 0);
            Grid.SetRow(cells[cell], split.IsRow ? 0 : indexes[cell]);
            grid.Children.Add(cells[cell]);
        }

        return grid;
    }

    // Both parts keep a usable size while the divider is dragged; the hairline needs none.
    private static Grid Define(AreaSplit split, Func<AreaKind, bool> isShown)
    {
        var grid = new Grid { Tag = split };
        var size = new GridLength(split.Size);
        GridLength[] lengths = split.FixesFirst ? [size, new GridLength(1), GridLength.Star] : [GridLength.Star, new GridLength(1), size];
        double[] minimums = [AreaLayout.MinimumLength(split.First, split.IsRow, isShown), 0, AreaLayout.MinimumLength(split.Second, split.IsRow, isShown)];
        if (split.IsRow)
        {
            grid.ColumnDefinitions.AddRange(lengths.Select((length, index) => new ColumnDefinition(length) { MinWidth = minimums[index] }));
            return grid;
        }

        grid.RowDefinitions.AddRange(lengths.Select((length, index) => new RowDefinition(length) { MinHeight = minimums[index] }));
        return grid;
    }
}
