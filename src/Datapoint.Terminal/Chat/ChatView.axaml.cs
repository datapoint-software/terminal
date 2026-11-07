using Avalonia.Controls;

namespace Datapoint.Terminal.Chat;

public sealed partial class ChatView : UserControl
{
    private bool _isAtEnd = true;

    public ChatView()
    {
        InitializeComponent();

        // Follow the conversation as it grows, unless the user has scrolled up to read.
        Scroller.ScrollChanged += (_, e) =>
        {
            if (e.ExtentDelta.Y > 0 && _isAtEnd)
            {
                Scroller.ScrollToEnd();
                return;
            }

            _isAtEnd = Scroller.Offset.Y + Scroller.Viewport.Height >= Scroller.Extent.Height - 4;
        };
    }

    public void FocusInput() => DraftBox.Focus();

    // Scrolls the step into view and outlines it for a moment.
    public void Reveal(ChatItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ItemList.ContainerFromItem(item)?.BringIntoView();
        item.IsHighlighted = true;
        Avalonia.Threading.DispatcherTimer.RunOnce(() => item.IsHighlighted = false, TimeSpan.FromSeconds(2));
    }
}
