using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Datapoint.Terminal.Shell.Attention;

[SupportedOSPlatform("windows")]
internal static partial class WindowsAttention
{
    // FLASHW_TRAY | FLASHW_TIMERNOFG: flash the taskbar button until the window comes to the foreground, without flashing its
    // caption.
    private const uint FlashTrayUntilForeground = 0x2 | 0xC;

    public static void Request(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Window = window,
            Flags = FlashTrayUntilForeground,
        };
        _ = FlashWindowEx(ref info);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlashWindowEx(ref FlashInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }
}
