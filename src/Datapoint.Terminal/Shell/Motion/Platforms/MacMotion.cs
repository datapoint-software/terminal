using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Datapoint.Terminal.Shell.Motion.Platforms;

// System Settings › Accessibility › Display › Reduce motion.
[SupportedOSPlatform("macos")]
internal static partial class MacMotion
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    public static bool IsReduced()
    {
        var workspace = Send(Class("NSWorkspace"), Selector("sharedWorkspace"));
        return workspace != IntPtr.Zero && SendBool(workspace, Selector("accessibilityDisplayShouldReduceMotion")) != 0;
    }

    [LibraryImport(ObjectiveC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr Class(string name);

    [LibraryImport(ObjectiveC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr Selector(string name);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr Send(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial byte SendBool(IntPtr receiver, IntPtr selector);
}
