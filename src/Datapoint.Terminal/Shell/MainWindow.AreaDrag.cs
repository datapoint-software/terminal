using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Datapoint.Terminal.Shell.Areas;

namespace Datapoint.Terminal.Shell;

// Product decision 109: an area moves by dragging its header onto an edge of another area, or of the window, with the place
// it would take shown until it's dropped. Escape cancels. The View menu does the same from the keyboard.
public sealed partial class MainWindow
{
    // How far the pointer moves before a press on a header becomes a drag, so clicks and small slips don't.
    private const double DragThreshold = 4;

    private AreaKind? _pressedArea;
    private Point _pressedAt;
    private bool _isDraggingArea;
    private AreaDrop? _areaDrop;

    private void SetUpAreaDragging()
    {
        foreach (var (kind, header) in new[]
        {
            (AreaKind.Agent, Chat.DragHandle),
            (AreaKind.Terminals, (Control)TerminalsHeader),
            (AreaKind.Documents, DocumentsHeader),
            (AreaKind.Plan, PlanHeader),
        })
        {
            header.PointerPressed += (_, e) => OnHeaderPressed(kind, header, e);
            header.PointerMoved += (_, e) => OnHeaderMoved(e);
            header.PointerReleased += (_, e) => OnHeaderReleased(e);
            header.PointerCaptureLost += (_, _) => EndAreaDrag();
        }

        AddHandler(KeyDownEvent, OnAreaDragKeyDown, RoutingStrategies.Tunnel);
    }

    // Presses on the header's buttons stay theirs.
    private void OnHeaderPressed(AreaKind kind, Control header, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(header).Properties.IsLeftButtonPressed || (e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        _pressedArea = kind;
        _pressedAt = e.GetPosition(Body);
        e.Pointer.Capture(header);
    }

    private void OnHeaderMoved(PointerEventArgs e)
    {
        if (_pressedArea is not { } kind)
        {
            return;
        }

        var point = e.GetPosition(Body);
        if (!_isDraggingArea && Point.Distance(point, _pressedAt) < DragThreshold)
        {
            return;
        }

        if (!_isDraggingArea)
        {
            _isDraggingArea = true;
            AreaControl(kind).Opacity = 0.5;
            Cursor = new Cursor(StandardCursorType.DragMove);
        }

        ShowAreaDrop(AreaDrop.Find(point, new Rect(Body.Bounds.Size), ShownAreaBounds(), kind));
    }

    private void OnHeaderReleased(PointerReleasedEventArgs e)
    {
        var drop = _isDraggingArea ? _areaDrop : null;
        var moved = _pressedArea;
        e.Pointer.Capture(null);
        EndAreaDrag();
        if (drop is null || moved is not { } kind)
        {
            return;
        }

        MoveArea(kind, drop.Target, drop.Edge);
    }

    private void OnAreaDragKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isDraggingArea || e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        EndAreaDrag();
    }

    private void EndAreaDrag()
    {
        if (_pressedArea is { } kind)
        {
            AreaControl(kind).Opacity = 1;
        }

        _pressedArea = null;
        _isDraggingArea = false;
        Cursor = null;
        ShowAreaDrop(null);
    }

    private void ShowAreaDrop(AreaDrop? drop)
    {
        _areaDrop = drop;
        AreaOverlay.IsVisible = drop is not null;
        if (drop is null)
        {
            return;
        }

        Canvas.SetLeft(AreaPreview, drop.Preview.X);
        Canvas.SetTop(AreaPreview, drop.Preview.Y);
        AreaPreview.Width = drop.Preview.Width;
        AreaPreview.Height = drop.Preview.Height;
    }

    private Dictionary<AreaKind, Rect> ShownAreaBounds() => Enum.GetValues<AreaKind>()
        .Where(kind => AreaControl(kind).IsEffectivelyVisible)
        .Select(kind => (kind, area: AreaControl(kind)))
        .Where(item => item.area.TranslatePoint(default, Body) is not null)
        .ToDictionary(item => item.kind, item => new Rect(item.area.TranslatePoint(default, Body)!.Value, item.area.Bounds.Size));
}
