using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Datapoint.Terminal.Shell.Motion;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class MotionTests
{
    // Headless frames stop after a few, so this relies on each animation being cut short once it should have ended. It waits
    // for the outcome, up to two seconds, as a busy machine runs those deadlines late.
    private static bool Until(Func<bool> done)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!done() && watch.ElapsedMilliseconds < 2000)
        {
            Thread.Sleep(5);
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        return done();
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
            Assert.True(Until(() => presenter.Opacity >= 1));
            Assert.Equal(1, presenter.Opacity);

            menu.Hide();
            Assert.True(menu.IsOpen);
            Assert.True(Until(() => !menu.IsOpen));
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

    [AvaloniaFact]
    public void Folds_a_pinned_card_away_with_its_last_content_and_back_in()
    {
        MotionPreference.Apply(reduce: false);
        try
        {
            var card = new ContentControl { Padding = new Avalonia.Thickness(12, 8) };
            Fold.SetIsOpen(card, true);
            card.Content = "Plan";
            var window = new Window { Width = 300, Height = 300, Content = new StackPanel { Children = { card, new TextBlock { Text = "Below" } } } };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            card.Content = null;
            Fold.SetIsOpen(card, false);
            Assert.Equal(("Plan", true), (card.Content as string, card.IsVisible));
            Assert.True(Until(() => !card.IsVisible));
            Assert.Equal((null, false), (card.Content, card.IsVisible));

            card.Content = "Next plan";
            Fold.SetIsOpen(card, true);
            Assert.True(card.IsVisible);
            Assert.True(Until(() => double.IsNaN(card.Height) && card.Opacity >= 1));
            Assert.Equal((double.NaN, 1d), (card.Height, card.Opacity));
        }
        finally
        {
            MotionPreference.Apply(reduce: true);
        }
    }
}
