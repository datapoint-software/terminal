using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Datapoint.Terminal.Shell.Attention;

// Follows the Extended Window Manager Hints: a client message to the root window adds _NET_WM_STATE_DEMANDS_ATTENTION to the
// window's state, which taskbars show and window managers clear when the window is focused. Avalonia's own display connection
// isn't exposed, so this opens a short-lived one; window ids are shared by every connection to the same server.
[SupportedOSPlatform("linux")]
internal static partial class X11Attention
{
    private const string Library = "libX11.so.6";
    private const int ClientMessage = 33;
    private const nint AddState = 1;
    private const nint SourceApplication = 1;
    private const nint SubstructureNotifyMask = 1 << 19;
    private const nint SubstructureRedirectMask = 1 << 20;

    public static void Request(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        var display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var message = new ClientMessageEvent
            {
                Type = ClientMessage,
                SendEvent = 1,
                Display = display,
                Window = window,
                MessageType = XInternAtom(display, "_NET_WM_STATE", onlyIfExists: false),
                Format = 32,
                Data0 = AddState,
                Data1 = XInternAtom(display, "_NET_WM_STATE_DEMANDS_ATTENTION", onlyIfExists: false),
                Data3 = SourceApplication,
            };
            _ = XSendEvent(display, XDefaultRootWindow(display), propagate: 0, SubstructureRedirectMask | SubstructureNotifyMask, ref message);
            _ = XFlush(display);
        }
        finally
        {
            _ = XCloseDisplay(display);
        }
    }

    [LibraryImport(Library)]
    private static partial IntPtr XOpenDisplay(IntPtr name);

    [LibraryImport(Library)]
    private static partial int XCloseDisplay(IntPtr display);

    [LibraryImport(Library)]
    private static partial IntPtr XDefaultRootWindow(IntPtr display);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr XInternAtom(IntPtr display, string name, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

    [LibraryImport(Library)]
    private static partial int XSendEvent(IntPtr display, IntPtr window, int propagate, IntPtr eventMask, ref ClientMessageEvent message);

    [LibraryImport(Library)]
    private static partial int XFlush(IntPtr display);

    // XClientMessageEvent padded to the size of the XEvent union (24 longs), which XSendEvent reads in full. Longs, Atoms and
    // Windows are pointer-sized on the 64-bit platforms we ship.
    [StructLayout(LayoutKind.Explicit, Size = 24 * 8)]
    private struct ClientMessageEvent
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(8)] public IntPtr Serial;
        [FieldOffset(16)] public int SendEvent;
        [FieldOffset(24)] public IntPtr Display;
        [FieldOffset(32)] public IntPtr Window;
        [FieldOffset(40)] public IntPtr MessageType;
        [FieldOffset(48)] public int Format;
        [FieldOffset(56)] public IntPtr Data0;
        [FieldOffset(64)] public IntPtr Data1;
        [FieldOffset(72)] public IntPtr Data2;
        [FieldOffset(80)] public IntPtr Data3;
        [FieldOffset(88)] public IntPtr Data4;
    }
}
