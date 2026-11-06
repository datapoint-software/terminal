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
}
