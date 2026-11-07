using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Datapoint.Terminal.Shell.Attention;

[SupportedOSPlatform("macos")]
internal static partial class MacAttention
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    // NSInformationalRequest bounces the Dock icon once; NSCriticalRequest would keep bouncing, which is too insistent for a
    // question that can wait.
    private const nint InformationalRequest = 10;

    public static void Request()
    {
        var application = Send(Class("NSApplication"), Selector("sharedApplication"));
        if (application == IntPtr.Zero)
        {
            return;
        }

        _ = Send(application, Selector("requestUserAttention:"), InformationalRequest);
    }

    [LibraryImport(ObjectiveC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr Class(string name);

    [LibraryImport(ObjectiveC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr Selector(string name);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr Send(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial nint Send(IntPtr receiver, IntPtr selector, nint argument);
}
