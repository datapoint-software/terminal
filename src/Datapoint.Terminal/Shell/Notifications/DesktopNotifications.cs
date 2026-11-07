using System.Diagnostics;

namespace Datapoint.Terminal.Shell.Notifications;

// Product decision 44: a notification when the agent finishes or waits, for a window that isn't in front. The text names the
// project and what happened, never terminal content or the agent's words, since notifications can show on a locked screen.
// The system's own tools are used, with the text passed as arguments rather than through a shell: osascript on macOS, which
// shows it under Script Editor's name until the application is bundled, and notify-send on Linux. Windows comes later.
public static class DesktopNotifications
{
    public static void Show(string title, string message)
    {
        var start = Command(title, message);
        if (start is null)
        {
            return;
        }

        try
        {
            using var process = Process.Start(start);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No notification tool: the title and the window's attention request still show it.
        }
    }

    public static ProcessStartInfo? Command(string title, string message)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        if (OperatingSystem.IsMacOS())
        {
            return Start("/usr/bin/osascript", "-e", "on run argv", "-e", "display notification (item 2 of argv) with title (item 1 of argv)", "-e", "end run", title, message);
        }

        return OperatingSystem.IsLinux() ? Start("notify-send", "--app-name=Datapoint Terminal", "--", title, message) : null;
    }

    private static ProcessStartInfo Start(string file, params string[] arguments)
    {
        var start = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }
}
