using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

internal static class WindowTitles
{
    // The waiting note comes first so it survives the truncation of taskbars, window lists and the window switcher.
    public static string For(string project, string? shellTitle, bool needsUser)
    {
        var name = string.IsNullOrEmpty(shellTitle) ? project : Strings.Format(Strings.WindowTitleWithShell, project, shellTitle);
        var title = Strings.Format(Strings.WindowTitle, name);
        return needsUser ? Strings.Format(Strings.WindowTitleNeedsUser, title) : title;
    }
}
