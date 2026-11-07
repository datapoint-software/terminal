using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

internal static class WindowTitles
{
    // The waiting note comes first so it survives the truncation of taskbars, window lists and the window switcher.
    public static string For(string workspace, string? shellTitle, bool needsUser)
    {
        var name = string.IsNullOrEmpty(shellTitle) ? workspace : Strings.Format(Strings.WindowTitleWithShell, workspace, shellTitle);
        var title = Strings.Format(Strings.WindowTitle, name);
        return needsUser ? Strings.Format(Strings.WindowTitleNeedsUser, title) : title;
    }
}
