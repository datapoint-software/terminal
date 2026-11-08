using Avalonia.Threading;

namespace Datapoint.Terminal.Shell;

// Product decision 147: the sidebar tips, after setup (or at the next launch, if the last ended before them), or again from
// the Help menu.
public sealed partial class MainWindow
{
    public void ShowTips()
    {
        if (_isSidebarHidden)
        {
            ToggleSidebar();
        }

        // After layout, so each tip finds its part of the sidebar in place.
        Dispatcher.UIThread.Post(() => Sidebar.ShowTips(() => _settings.SetTipsPending(false)), DispatcherPriority.Loaded);
    }
}
