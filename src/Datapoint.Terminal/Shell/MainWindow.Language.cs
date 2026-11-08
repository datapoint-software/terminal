using Avalonia.Controls;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 150: when the language changes, what the window built in code is built again in it. Views follow on
// their own; the conversation, its cards and the names already given to tabs stay as they are.
public sealed partial class MainWindow
{
    public void OnLanguageChanged()
    {
        UpdateTitle();
        NativeMenu.SetMenu(this, BuildMenu());
        UpdateLayoutMenu();
        _toggleSidebarItem?.Header = _isSidebarHidden ? Strings.MenuShowSidebar : Strings.MenuHideSidebar;
        SetUpPlanButtons();
        ShowPlanSummary();
    }
}
