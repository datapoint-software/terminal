using Avalonia.Controls;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Settings;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Pages.SelectionChanged += (_, _) => ShowPage();
        DataContextChanged += (_, _) =>
        {
            var settings = DataContext as SettingsViewModel;
            AppearancePage.DataContext = settings is null ? null : new AppearanceViewModel(settings);
            WebPage.DataContext = settings is null ? null : new WebViewModel(settings);
        };
    }

    // The workspace whose history the History page shows: the window Settings was opened from.
    public IHistoryAccess? History
    {
        get => HistoryPage.Access;
        set => HistoryPage.Access = value;
    }

    public const int MemoryPageIndex = 3;

    // The memory the Memory page shows: the user's own and that of the workspace Settings was opened from.
    public MemoryPlaces? Memory
    {
        set => MemoryPage.Model.Load(value);
    }

    public void ShowPage(int page) => Pages.SelectedIndex = page;

    public void ReloadMemory() => MemoryPage.Model.Reload();

    private void ShowPage()
    {
        ProvidersPage.IsVisible = Pages.SelectedIndex == 0;
        AppearanceScroller.IsVisible = Pages.SelectedIndex == 1;
        HistoryScroller.IsVisible = Pages.SelectedIndex == 2;
        MemoryScroller.IsVisible = Pages.SelectedIndex == MemoryPageIndex;
        WebScroller.IsVisible = Pages.SelectedIndex == MemoryPageIndex + 1;
        if (!MemoryScroller.IsVisible)
        {
            return;
        }

        MemoryPage.Model.Reload();
    }
}
