using Avalonia.Controls;

namespace Datapoint.Terminal.Shell.Attention;

// Asks the operating system to draw the user to a window that isn't in front: the Dock icon bounces once on macOS, the
// taskbar button flashes on Windows, and X11 window managers mark the window as demanding attention. Avalonia has no API for
// this. Failing is harmless: the window's title and tab still show that the agent is waiting.
internal static class WindowAttention
{
    public static void Request(Window window)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                MacAttention.Request();
            }
            else if (OperatingSystem.IsWindows())
            {
                WindowsAttention.Request(Handle(window));
            }
            else if (OperatingSystem.IsLinux())
            {
                X11Attention.Request(Handle(window));
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    private static IntPtr Handle(Window window) => window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
}
