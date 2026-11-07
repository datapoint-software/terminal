using Avalonia;
using Avalonia.Controls;

namespace Datapoint.Terminal.Shell;

// Arranges a three-cell grid: the chat pane, a hairline with a splitter over it, and the terminals, in either order and
// direction. A hidden pane takes no space.
public sealed class ChatLayout(Grid body, Control chat, Control terminals, Control separator, GridSplitter splitter)
{
    public const double MinimumSize = 200;

    public static bool IsBeside(ChatSide side) => side is ChatSide.Left or ChatSide.Right;

    public void Apply(ChatSide side, bool isHidden, double width, double height)
    {
        var beside = IsBeside(side);
        var chatIndex = side is ChatSide.Left or ChatSide.Top ? 0 : 2;
        var size = new GridLength(isHidden ? 0 : beside ? width : height);
        var line = new GridLength(isHidden ? 0 : 1);
        GridLength[] lengths = chatIndex == 0 ? [size, line, GridLength.Star] : [GridLength.Star, line, size];
        Define(beside, lengths, chatIndex, isHidden ? 0 : MinimumSize);

        Place(chat, beside, chatIndex);
        Place(terminals, beside, 2 - chatIndex);
        Place(separator, beside, 1);
        Place(splitter, beside, 1);
        splitter.ResizeDirection = beside ? GridResizeDirection.Columns : GridResizeDirection.Rows;
        splitter.Margin = beside ? new Thickness(-3, 0) : new Thickness(0, -3);
        chat.IsVisible = separator.IsVisible = splitter.IsVisible = !isHidden;
    }

    private void Define(bool beside, GridLength[] lengths, int chatIndex, double minimum)
    {
        body.ColumnDefinitions.Clear();
        body.RowDefinitions.Clear();
        if (beside)
        {
            body.ColumnDefinitions.AddRange(lengths.Select((length, index) => new ColumnDefinition(length) { MinWidth = index == chatIndex ? minimum : 0 }));
            return;
        }

        body.RowDefinitions.AddRange(lengths.Select((length, index) => new RowDefinition(length) { MinHeight = index == chatIndex ? minimum : 0 }));
    }

    private static void Place(Control control, bool beside, int index)
    {
        Grid.SetColumn(control, beside ? index : 0);
        Grid.SetRow(control, beside ? 0 : index);
    }
}
