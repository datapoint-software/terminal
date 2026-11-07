using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Datapoint.Terminal.Shell.Motion;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class OverlayMotionTests
{
    // Headless frames stop after a few, so this relies on each animation being cut short once it should have ended.
    private static void Wait()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 400)
        {
            Thread.Sleep(5);
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Fades_a_menu_in_and_puts_off_its_closing_until_it_has_faded_out()
    {
        OverlayMotion.Start();
        MotionPreference.Apply(reduce: false);
        try
        {
            var button = new Button { Content = "Open" };
            var window = new Window { Width = 300, Height = 300, Content = button };
            window.Show();
            var menu = new MenuFlyout { Items = { new MenuItem { Header = "One" } } };

            menu.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            var presenter = menu.Popup.Child!;
            Assert.True(presenter.Opacity < 1);
            Wait();
            Assert.Equal(1, presenter.Opacity);

            menu.Hide();
            Assert.True(menu.IsOpen);
            Wait();
            Assert.False(menu.IsOpen);

            MotionPreference.Apply(reduce: true);
            menu.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, presenter.Opacity);
            menu.Hide();
            Assert.False(menu.IsOpen);
        }
        finally
        {
            MotionPreference.Apply(reduce: true);
        }
    }
}
