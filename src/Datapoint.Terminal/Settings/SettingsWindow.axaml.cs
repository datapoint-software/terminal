using Avalonia.Controls;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Settings;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Pages.SelectionChanged += (_, _) => ShowPage();
        DataContextChanged += (_, _) => AppearancePage.DataContext = DataContext is SettingsViewModel settings ? new AppearanceViewModel(settings) : null;
    }

    // The workspace whose history the History page shows: the window Settings was opened from.
    public IHistoryAccess? History
    {
        get => HistoryPage.Access;
        set => HistoryPage.Access = value;
    }

    public void ShowPage(int page) => Pages.SelectedIndex = page;

    private void ShowPage()
    {
        ProvidersPage.IsVisible = Pages.SelectedIndex == 0;
        AppearanceScroller.IsVisible = Pages.SelectedIndex == 1;
        HistoryScroller.IsVisible = Pages.SelectedIndex == 2;
    }
}
